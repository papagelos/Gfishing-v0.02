using System;
using TMPro;
using GalacticFishing.Minigames.HexWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [DisallowMultipleComponent]
    public sealed class DungeonExtractionManager : MonoBehaviour
    {
        private const string ExitButtonName = "Btn_ExitDungeon";
        private const string ExtractionLabelName = "Label_ExtractionTimer";
        private const string LootButtonName = "BtnLoot";

        [Header("Extraction")]
        [SerializeField, Min(0.1f)] private float extractionSeconds = 5f;
        [SerializeField] private string hubSceneName = "MainGame";
        [SerializeField] private string fallbackHubSceneName = "HexWorld_Village";

        [Header("Refs")]
        [SerializeField] private Canvas dungeonCanvas;
        [SerializeField] private Button exitDungeonButton;
        [SerializeField] private TMP_Text extractionLabel;
        [SerializeField] private DungeonRunInventory runInventory;
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Auto Setup")]
        [SerializeField] private bool autoCreateUi = true;
        [SerializeField] private bool createPlaceholderHazard = true;

        private PlayerHealth _subscribedHealth;
        private bool _extracting;
        private float _remaining;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapInDungeonScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!IsDungeonScene(scene.name))
                return;

            var existing = FindAnyObjectByType<DungeonExtractionManager>(FindObjectsInactive.Include);
            if (existing != null)
                return;

            var go = new GameObject(nameof(DungeonExtractionManager));
            go.AddComponent<DungeonExtractionManager>();
        }

        private static bool IsDungeonScene(string sceneName)
        {
            return !string.IsNullOrWhiteSpace(sceneName) &&
                   sceneName.IndexOf("DUNGEON", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureUi();
            EnsurePlaceholderHazard();
            SubscribePlayerHealth();
            SetExtractionLabelVisible(false);
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureUi();
            SubscribePlayerHealth();
            WireExitButton();
        }

        private void OnDisable()
        {
            if (exitDungeonButton != null)
                exitDungeonButton.onClick.RemoveListener(StartExtraction);

            if (_subscribedHealth != null)
                _subscribedHealth.OnDamaged -= HandlePlayerDamaged;
            _subscribedHealth = null;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
                StartExtraction();

            if (playerHealth == null)
            {
                ResolvePlayerHealth();
                SubscribePlayerHealth();
            }

            if (!_extracting)
                return;

            _remaining = Mathf.Max(0f, _remaining - Time.deltaTime);
            UpdateExtractionLabel();

            if (_remaining <= 0f)
                CompleteExtraction();
        }

        public void StartExtraction()
        {
            _extracting = true;
            _remaining = extractionSeconds;
            SetExtractionLabelVisible(true);
            UpdateExtractionLabel();
        }

        private void HandlePlayerDamaged()
        {
            if (!_extracting)
                return;

            _remaining = extractionSeconds;
            UpdateExtractionLabel();
        }

        private void CompleteExtraction()
        {
            _extracting = false;
            SetExtractionLabelVisible(false);

            if (runInventory == null)
                runInventory = FindAnyObjectByType<DungeonRunInventory>(FindObjectsInactive.Include);

            if (runInventory != null)
            {
                // Immediate push path where available.
                HexWorldWarehouseInventory warehouse = FindAnyObjectByType<HexWorldWarehouseInventory>(FindObjectsInactive.Include);
                if (warehouse != null)
                    runInventory.PushToWarehouse(warehouse);
            }

            DungeonRunInventory.QueueActiveRunLootForWarehouseTransfer();

            PrepareCameraForMainGame();

            if (Application.CanStreamedLevelBeLoaded(hubSceneName))
            {
                SceneManager.LoadScene(hubSceneName);
                return;
            }

            if (!string.IsNullOrWhiteSpace(fallbackHubSceneName) &&
                Application.CanStreamedLevelBeLoaded(fallbackHubSceneName))
            {
                SceneManager.LoadScene(fallbackHubSceneName);
                return;
            }

            Debug.LogWarning($"[{nameof(DungeonExtractionManager)}] No valid hub scene configured for extraction.");
        }

        private static void PrepareCameraForMainGame()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.transform.localEulerAngles = Vector3.zero;
            cam.orthographic = true;

            var panZoom = cam.GetComponent<HexCameraPanZoom3D>();
            if (panZoom != null)
            {
                panZoom.enabled = false;
                UnityEngine.Object.Destroy(panZoom);
            }
        }

        private void ResolveReferences()
        {
            if (dungeonCanvas == null)
                dungeonCanvas = FindNamedObjectComponent<Canvas>("DungeonLootCanvas") ?? FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);

            if (runInventory == null)
                runInventory = FindAnyObjectByType<DungeonRunInventory>(FindObjectsInactive.Include);

            ResolvePlayerHealth();
        }

        private void ResolvePlayerHealth()
        {
            if (playerHealth != null)
                return;

            playerHealth = FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (playerHealth != null)
                return;

            PlayerController3D controller = FindAnyObjectByType<PlayerController3D>(FindObjectsInactive.Include);
            if (controller != null)
                playerHealth = controller.GetComponent<PlayerHealth>() ?? controller.gameObject.AddComponent<PlayerHealth>();
        }

        private void SubscribePlayerHealth()
        {
            if (_subscribedHealth == playerHealth)
                return;

            if (_subscribedHealth != null)
                _subscribedHealth.OnDamaged -= HandlePlayerDamaged;

            _subscribedHealth = playerHealth;

            if (_subscribedHealth != null)
                _subscribedHealth.OnDamaged += HandlePlayerDamaged;
        }

        private void EnsureUi()
        {
            if (dungeonCanvas == null)
                return;

            if (exitDungeonButton == null)
                exitDungeonButton = FindNamedObjectComponent<Button>(ExitButtonName);

            if (exitDungeonButton == null && autoCreateUi)
                exitDungeonButton = CreateExitButton();

            if (extractionLabel == null)
                extractionLabel = FindNamedObjectComponent<TMP_Text>(ExtractionLabelName);

            if (extractionLabel == null && autoCreateUi)
                extractionLabel = CreateExtractionLabel();

            WireExitButton();
        }

        private void WireExitButton()
        {
            if (exitDungeonButton == null)
                return;

            exitDungeonButton.onClick.RemoveListener(StartExtraction);
            exitDungeonButton.onClick.AddListener(StartExtraction);
        }

        private Button CreateExitButton()
        {
            Button source = FindNamedObjectComponent<Button>(LootButtonName);
            GameObject created;

            if (source != null)
            {
                created = Instantiate(source.gameObject, source.transform.parent);
                created.name = ExitButtonName;

                RectTransform srcRt = source.transform as RectTransform;
                RectTransform dstRt = created.transform as RectTransform;
                if (srcRt != null && dstRt != null)
                {
                    dstRt.anchorMin = srcRt.anchorMin;
                    dstRt.anchorMax = srcRt.anchorMax;
                    dstRt.pivot = srcRt.pivot;
                    dstRt.sizeDelta = new Vector2(170f, srcRt.sizeDelta.y);
                    dstRt.anchoredPosition = srcRt.anchoredPosition + new Vector2(srcRt.sizeDelta.x + 12f, 0f);
                }
            }
            else
            {
                created = new GameObject(ExitButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
                created.transform.SetParent(dungeonCanvas.transform, false);

                RectTransform rt = created.transform as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(128f, -16f);
                    rt.sizeDelta = new Vector2(170f, 36f);
                }

                Image image = created.GetComponent<Image>();
                if (image != null)
                    image.color = new Color(0.24f, 0.24f, 0.24f, 0.95f);

                CreateButtonLabel(created.transform, "EXIT DUNGEON", null);
            }

            TMP_Text text = created.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = "EXIT DUNGEON";

            return created.GetComponent<Button>();
        }

        private TMP_Text CreateExtractionLabel()
        {
            if (dungeonCanvas == null)
                return null;

            GameObject go = new GameObject(ExtractionLabelName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(dungeonCanvas.transform, false);

            RectTransform rt = go.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -12f);
                rt.sizeDelta = new Vector2(760f, 70f);
            }

            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                TMP_Text sourceText = FindNamedObjectComponent<Button>(LootButtonName)?.GetComponentInChildren<TMP_Text>(true);
                if (sourceText != null)
                {
                    label.font = sourceText.font;
                    label.fontSharedMaterial = sourceText.fontSharedMaterial;
                }

                label.fontSize = 28f;
                label.alignment = TextAlignmentOptions.Top;
                label.color = new Color(1f, 0.9f, 0.35f, 1f);
            }

            return label;
        }

        private void UpdateExtractionLabel()
        {
            if (extractionLabel == null)
                return;

            int shownSeconds = Mathf.CeilToInt(_remaining);
            extractionLabel.text = $"TIME TO EXTRACTION: {shownSeconds}s\nDamage resets the extraction timer!";
        }

        private void SetExtractionLabelVisible(bool visible)
        {
            if (extractionLabel == null)
                return;

            extractionLabel.gameObject.SetActive(visible);
        }

        private void EnsurePlaceholderHazard()
        {
            if (!createPlaceholderHazard)
                return;

            var existing = FindAnyObjectByType<DungeonExtractionDamageHazard>(FindObjectsInactive.Include);
            if (existing != null)
                return;

            GameObject hazard = new GameObject("PlaceholderHazard_ExtractionTest");
            hazard.transform.position = new Vector3(1.5f, 0.5f, 0f);

            var collider = hazard.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(2f, 2f, 2f);

            hazard.AddComponent<DungeonExtractionDamageHazard>();
        }

        private static T FindNamedObjectComponent<T>(string name) where T : Component
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                T c = all[i];
                if (c != null && string.Equals(c.gameObject.name, name, StringComparison.Ordinal))
                    return c;
            }

            return null;
        }

        private static void CreateButtonLabel(Transform parent, string text, TMP_FontAsset font)
        {
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(parent, false);

            RectTransform rt = labelGo.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
            if (label == null)
                return;

            if (font != null)
                label.font = font;
            label.text = text;
            label.fontSize = 22f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
        }
    }

    public sealed class DungeonExtractionDamageHazard : MonoBehaviour
    {
        [SerializeField, Min(1)] private int damagePerTouch = 1;

        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
                return;

            PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
            if (health == null)
                return;

            health.TakeDamage(Mathf.Max(1, damagePerTouch));
        }
    }
}
