// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldBuildingActivationClickToggle.cs
using UnityEngine;
using UnityEngine.EventSystems;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// MVP input handler:
    /// - In BUILDINGS mode, when you're NOT placing a building, clicking a placed building toggles Active/Dormant.
    /// - Enforces Town Hall Active Slots.
    ///
    /// This is intentionally separate from HexWorld3DController.
    /// </summary>
    public sealed class HexWorldBuildingActivationClickToggle : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private HexWorld3DController controller;
        [SerializeField] private HexWorldCapacityService capacity;
        [SerializeField] private Camera raycastCamera;

        [Header("Tuning")]
        [SerializeField] private LayerMask raycastMask = ~0;

        private void Awake()
        {
            if (!controller) controller = FindObjectOfType<HexWorld3DController>(true);
            if (!capacity) capacity = FindObjectOfType<HexWorldCapacityService>(true);
            if (!raycastCamera) raycastCamera = Camera.main;
        }

        private void Update()
        {
            if (!controller) return;

            // Only in Buildings mode.
            if (controller.CurrentPaletteMode != HexWorld3DController.PaletteMode.Buildings)
                return;

            // Don't toggle while placing a new building.
            if (controller.SelectedBuilding != null)
                return;

            if (!Input.GetMouseButtonDown(0))
                return;

            // Ignore clicks over UI.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (!raycastCamera) raycastCamera = Camera.main;
            if (!raycastCamera) return;

            Ray ray = raycastCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 9999f, raycastMask, QueryTriggerInteraction.Ignore))
                return;

            var inst = hit.collider ? hit.collider.GetComponentInParent<HexWorldBuildingInstance>() : null;
            if (!inst) return;

            var state = inst.GetComponent<HexWorldBuildingActiveState>();
            if (!state) state = inst.gameObject.AddComponent<HexWorldBuildingActiveState>();

            var prod = inst.GetComponent<HexWorldBuildingProductionProfile>();
            if (!prod) prod = inst.gameObject.AddComponent<HexWorldBuildingProductionProfile>();

            bool wantActive = !state.IsActive;

            if (wantActive && prod.consumesActiveSlot)
            {
                int slots = capacity ? capacity.ActiveSlots : int.MaxValue;
                int used = CountUsedActiveSlots();

                // Since this building is currently dormant, it is not counted. Enabling it would consume +1.
                if (used + 1 > slots)
                {
                    Debug.LogWarning($"[HexWorld] Cannot activate building. Active slots full ({used}/{slots}).");
                    return;
                }
            }

            state.SetActive(wantActive);
        }

        private static int CountUsedActiveSlots()
        {
            int used = 0;
            var states = FindObjectsOfType<HexWorldBuildingActiveState>(true);
            for (int i = 0; i < states.Length; i++)
            {
                var s = states[i];
                if (!s || !s.IsActive) continue;

                var prod = s.GetComponent<HexWorldBuildingProductionProfile>();
                if (prod != null && prod.consumesActiveSlot)
                    used++;
            }
            return used;
        }
    }
}
