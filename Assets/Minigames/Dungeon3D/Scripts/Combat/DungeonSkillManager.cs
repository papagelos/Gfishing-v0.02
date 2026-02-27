using UnityEngine;
using UnityEngine.InputSystem;
using GalacticFishing.Data;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [DisallowMultipleComponent]
    public sealed class DungeonSkillManager : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private DungeonHotbarController hotbar;
        [SerializeField] private DungeonGemRegistry gemRegistry;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Camera aimCamera;

        [Header("Casting")]
        [SerializeField, Min(0f)] private float projectileSpawnHeightOffset = 0.2f;
        [SerializeField, Min(0.1f)] private float fallbackTargetDistance = 6f;
        [SerializeField] private bool debugLogs = false;

        private bool _boundHotbar;

        private void Awake()
        {
            ResolveRefs();
        }

        private void Start()
        {
            ResolveRefs();
            TryBindHotbar();
        }

        private void OnEnable()
        {
            ResolveRefs();
            TryBindHotbar();
        }

        private void OnDisable()
        {
            UnbindHotbar();
        }

        private void ResolveRefs()
        {
            if (hotbar == null)
                hotbar = FindAnyObjectByType<DungeonHotbarController>(FindObjectsInactive.Include);

            if (playerTransform == null)
            {
                GameObject tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null)
                    playerTransform = tagged.transform;
            }

            if (playerTransform == null)
            {
                PlayerController3D pc = FindAnyObjectByType<PlayerController3D>(FindObjectsInactive.Include);
                if (pc != null)
                    playerTransform = pc.transform;
            }

            if (aimCamera == null)
                aimCamera = Camera.main;
        }

        private void TryBindHotbar()
        {
            if (_boundHotbar)
                return;

            if (hotbar == null)
                hotbar = FindAnyObjectByType<DungeonHotbarController>(FindObjectsInactive.Include);

            if (hotbar == null)
                return;

            hotbar.CastRequested -= OnCastRequested;
            hotbar.CastRequested += OnCastRequested;
            _boundHotbar = true;
        }

        private void UnbindHotbar()
        {
            if (!_boundHotbar || hotbar == null)
            {
                _boundHotbar = false;
                return;
            }

            hotbar.CastRequested -= OnCastRequested;
            _boundHotbar = false;
        }

        private void OnCastRequested(int slotIndex, GemId gemId)
        {
            ResolveRefs();

            if (gemId == GemId.None)
                return;

            if (!TryResolveSkill(gemId, out DungeonSkillDefinition skill))
            {
                Debug.LogWarning($"[DungeonSkillManager] No skill definition assigned for gem {gemId}.", this);
                return;
            }

            if (skill == null || skill.projectilePrefab == null)
            {
                Debug.LogWarning($"[DungeonSkillManager] Skill '{gemId}' is missing a projectile prefab.", this);
                return;
            }

            if (playerTransform == null)
            {
                Debug.LogWarning("[DungeonSkillManager] No player found for projectile spawn.", this);
                return;
            }

            Vector3 spawnPos = playerTransform.position + (Vector3.up * projectileSpawnHeightOffset);
            Vector3 target = TryGetAimPoint(spawnPos, out Vector3 hitPoint)
                ? hitPoint
                : GetFallbackTarget(spawnPos);
            target = ClampTargetToSkillRange(skill, spawnPos, target);

            GameObject projectileGo = Instantiate(skill.projectilePrefab, spawnPos, Quaternion.identity);
            DungeonProjectile projectile = projectileGo.GetComponent<DungeonProjectile>();
            if (projectile == null)
                projectile = projectileGo.GetComponentInChildren<DungeonProjectile>(true);

            if (projectile == null)
            {
                Debug.LogWarning($"[DungeonSkillManager] Projectile prefab '{projectileGo.name}' has no DungeonProjectile component.", projectileGo);
                Destroy(projectileGo);
                return;
            }

            int rolledDamage = RollDamage(skill);
            projectile.Initialize(
                target,
                rolledDamage,
                Mathf.Max(0.1f, skill.speed),
                skill.hitPrefab,
                Mathf.Max(0f, skill.knockbackForce),
                Mathf.Max(0f, skill.stunDuration));

            if (debugLogs)
                Debug.Log($"[DungeonSkillManager] Cast {gemId} (slot {slotIndex + 1}) -> {target}", this);
        }

        private bool TryResolveSkill(GemId gemId, out DungeonSkillDefinition skill)
        {
            skill = null;
            if (gemRegistry == null || gemRegistry.gems == null)
                return false;

            for (int i = 0; i < gemRegistry.gems.Count; i++)
            {
                DungeonGemRegistry.GemData row = gemRegistry.gems[i];
                if (row == null || row.gemId != gemId)
                    continue;

                skill = row.skillDefinition;
                return skill != null;
            }

            return false;
        }

        private bool TryGetAimPoint(Vector3 spawnPos, out Vector3 targetWorldPoint)
        {
            targetWorldPoint = default;

            Camera cam = aimCamera != null ? aimCamera : Camera.main;
            if (cam == null)
                return false;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return false;

            Vector2 screenPos = mouse.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(screenPos);

            // Cast onto the player's ground plane so projectiles travel level in XZ space.
            Plane plane = new Plane(Vector3.up, new Vector3(0f, spawnPos.y, 0f));
            if (!plane.Raycast(ray, out float enter))
                return false;

            targetWorldPoint = ray.GetPoint(enter);
            targetWorldPoint.y = spawnPos.y;
            return true;
        }

        private Vector3 GetFallbackTarget(Vector3 spawnPos)
        {
            Camera cam = aimCamera != null ? aimCamera : Camera.main;
            if (cam == null)
                return spawnPos + Vector3.forward * fallbackTargetDistance;

            Vector3 dir = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.forward;

            dir.Normalize();
            Vector3 target = spawnPos + (dir * fallbackTargetDistance);
            target.y = spawnPos.y;
            return target;
        }

        private static int RollDamage(DungeonSkillDefinition skill)
        {
            if (skill == null)
                return 1;

            int min = skill.damageRange.x;
            int max = skill.damageRange.y;
            if (min > max)
                (min, max) = (max, min);

            // Support legacy assets where damageRange was not authored yet.
            if (min <= 0 && max <= 0)
                return Mathf.Max(1, skill.damage);

            min = Mathf.Max(1, min);
            max = Mathf.Max(min, max);
            return UnityEngine.Random.Range(min, max + 1);
        }

        private static Vector3 ClampTargetToSkillRange(DungeonSkillDefinition skill, Vector3 origin, Vector3 target)
        {
            if (skill == null || skill.maxRange <= 0f)
                return target;

            Vector3 delta = target - origin;
            float distance = delta.magnitude;
            if (distance <= skill.maxRange || distance <= 0.0001f)
                return target;

            Vector3 clamped = origin + (delta / distance) * skill.maxRange;
            clamped.y = origin.y;
            return clamped;
        }
    }
}
