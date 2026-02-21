using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using GalacticFishing.Minigames.HexWorld;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GalacticFishing.Minigames.Dungeon3D
{
    [DisallowMultipleComponent]
    public sealed class DungeonLootPanelUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject lootPanel;
        [SerializeField] private Button toggleButton;
        [SerializeField] private RectTransform rowsParent;
        [SerializeField] private GameObject rowTemplate;

        [Header("Runtime Sources")]
        [SerializeField] private DungeonRunInventory inventory;
        [SerializeField] private DimensionGenerator dimensionGenerator;
        [SerializeField] private bool autoDiscover = true;

        private readonly List<GameObject> _spawnedRows = new();
        private readonly Dictionary<HexWorldResourceId, DungeonResourceDefinition> _resourceDefByLoot = new();
        private readonly Dictionary<HexWorldResourceId, Sprite> _iconByLoot = new();

        private DungeonRunInventory _subscribedInventory;

        private static readonly BindingFlags InstanceFieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly FieldInfo ResourceDefinitionsField =
            typeof(DimensionGenerator).GetField("resourceDefinitions", InstanceFieldFlags);

        private static readonly FieldInfo RegistryField =
            typeof(DimensionGenerator).GetField("registry", InstanceFieldFlags);

        private void OnEnable()
        {
            if (toggleButton != null)
            {
                toggleButton.onClick.RemoveListener(Toggle);
                toggleButton.onClick.AddListener(Toggle);
            }

            ResolveReferences();
            SubscribeInventory();
            Refresh();
        }

        private void Start()
        {
            if (rowTemplate != null && rowTemplate.activeSelf)
                rowTemplate.SetActive(false);

            if (rowsParent == null && rowTemplate != null)
                rowsParent = rowTemplate.transform.parent as RectTransform;

            ResolveReferences();
            SubscribeInventory();
            Refresh();
        }

        private void OnDisable()
        {
            if (toggleButton != null)
                toggleButton.onClick.RemoveListener(Toggle);

            UnsubscribeInventory();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.iKey.wasPressedThisFrame)
                Toggle();

            if (autoDiscover && (inventory == null || dimensionGenerator == null))
            {
                ResolveReferences();
                SubscribeInventory();
            }
        }

        public void Toggle()
        {
            if (lootPanel == null)
                return;

            bool next = !lootPanel.activeSelf;
            lootPanel.SetActive(next);

            if (next)
                Refresh();
        }

        public void Refresh()
        {
            if (rowsParent == null || rowTemplate == null)
                return;

            if (rowTemplate.activeSelf)
                rowTemplate.SetActive(false);

            ResolveReferences();
            RebuildResourceLookup();
            RebuildRows();

            // Force immediate layout refresh so ContentSizeFitter snaps in the same frame.
            LayoutRebuilder.ForceRebuildLayoutImmediate(rowsParent);
            if (lootPanel != null)
            {
                RectTransform panelRect = lootPanel.transform as RectTransform;
                if (panelRect != null && panelRect != rowsParent)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            }
            Canvas.ForceUpdateCanvases();
        }

        private void ResolveReferences()
        {
            if (inventory == null && autoDiscover)
                inventory = UnityEngine.Object.FindAnyObjectByType<DungeonRunInventory>(FindObjectsInactive.Include);

            if (dimensionGenerator == null && autoDiscover)
                dimensionGenerator = UnityEngine.Object.FindAnyObjectByType<DimensionGenerator>(FindObjectsInactive.Include);

            if (rowsParent == null && rowTemplate != null)
                rowsParent = rowTemplate.transform.parent as RectTransform;
        }

        private void SubscribeInventory()
        {
            if (_subscribedInventory == inventory)
                return;

            UnsubscribeInventory();

            if (inventory == null)
                return;

            inventory.OnChanged += HandleInventoryChanged;
            _subscribedInventory = inventory;
        }

        private void UnsubscribeInventory()
        {
            if (_subscribedInventory != null)
                _subscribedInventory.OnChanged -= HandleInventoryChanged;

            _subscribedInventory = null;
        }

        private void HandleInventoryChanged()
        {
            Refresh();
        }

        private void RebuildRows()
        {
            ClearRowsForRefresh();

            if (inventory == null || inventory.Loot == null)
                return;

            var entries = new List<KeyValuePair<HexWorldResourceId, int>>(inventory.Loot);
            entries.Sort((a, b) => a.Key.CompareTo(b.Key));

            for (int i = 0; i < entries.Count; i++)
            {
                HexWorldResourceId lootId = entries[i].Key;
                int amount = entries[i].Value;
                if (amount <= 0)
                    continue;

                GameObject row = Instantiate(rowTemplate, rowsParent);
                row.name = $"LootRow_{lootId}";
                row.SetActive(true);
                _spawnedRows.Add(row);

                Image icon = FindRowIcon(row);
                TMP_Text text = row.GetComponentInChildren<TMP_Text>(true);

                Sprite iconSprite = ResolveIcon(lootId);
                if (icon != null)
                {
                    icon.sprite = iconSprite;
                    icon.enabled = iconSprite != null;
                }

                if (text != null)
                    text.text = $"{ResolveResourceName(lootId)}: {amount}";
            }
        }

        private void ClearRowsForRefresh()
        {
            if (rowsParent != null)
            {
                for (int i = rowsParent.childCount - 1; i >= 0; i--)
                {
                    Transform child = rowsParent.GetChild(i);
                    if (child == null)
                        continue;

                    // Keep an in-panel template if one is used.
                    if (rowTemplate != null && child == rowTemplate.transform)
                    {
                        if (child.gameObject.activeSelf)
                            child.gameObject.SetActive(false);
                        continue;
                    }

                    Destroy(child.gameObject);
                }
            }

            _spawnedRows.Clear();
        }

        private void RebuildResourceLookup()
        {
            _resourceDefByLoot.Clear();
            _iconByLoot.Clear();

            if (dimensionGenerator == null)
                return;

            List<DungeonResourceDefinition> resourceDefinitions = GetGeneratorResourceDefinitions(dimensionGenerator);
            PropRegistry propRegistry = GetGeneratorRegistry(dimensionGenerator);

            for (int i = 0; i < resourceDefinitions.Count; i++)
            {
                DungeonResourceDefinition def = resourceDefinitions[i];
                if (def == null || def.lootId == HexWorldResourceId.None)
                    continue;

                _resourceDefByLoot[def.lootId] = def;

                if (_iconByLoot.ContainsKey(def.lootId))
                    continue;

                Sprite icon = ResolveIconFromDefinition(def, propRegistry);
                if (icon != null)
                    _iconByLoot[def.lootId] = icon;
            }
        }

        private static List<DungeonResourceDefinition> GetGeneratorResourceDefinitions(DimensionGenerator generator)
        {
            if (generator == null || ResourceDefinitionsField == null)
                return new List<DungeonResourceDefinition>();

            return ResourceDefinitionsField.GetValue(generator) as List<DungeonResourceDefinition>
                   ?? new List<DungeonResourceDefinition>();
        }

        private static PropRegistry GetGeneratorRegistry(DimensionGenerator generator)
        {
            if (generator == null || RegistryField == null)
                return null;

            return RegistryField.GetValue(generator) as PropRegistry;
        }

        private static Sprite ResolveIconFromDefinition(DungeonResourceDefinition definition, PropRegistry registry)
        {
            if (definition == null)
                return null;

            GameObject prefab = definition.prefab;
            if (prefab == null)
                return null;

            if (registry != null && registry.allProps != null)
            {
                for (int i = 0; i < registry.allProps.Count; i++)
                {
                    HexWorldPropDefinition prop = registry.allProps[i];
                    if (prop == null || prop.prefab != prefab)
                        continue;

                    if (prop.thumbnail != null)
                        return prop.thumbnail;

                    Sprite visualSprite = TryGetVisualSprite(prop.prefab);
                    if (visualSprite != null)
                        return visualSprite;
                }
            }

            return TryGetVisualSprite(prefab);
        }

        private static Sprite TryGetVisualSprite(GameObject prefab)
        {
            if (prefab == null)
                return null;

            Transform visual = prefab.transform.Find("Visual");
            if (visual != null)
            {
                SpriteRenderer visualRenderer = visual.GetComponent<SpriteRenderer>();
                if (visualRenderer != null && visualRenderer.sprite != null)
                    return visualRenderer.sprite;
            }

            SpriteRenderer[] allRenderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                SpriteRenderer sr = allRenderers[i];
                if (sr == null || sr.sprite == null)
                    continue;

                if (sr.transform.name.IndexOf("visual", StringComparison.OrdinalIgnoreCase) >= 0)
                    return sr.sprite;
            }

            for (int i = 0; i < allRenderers.Length; i++)
            {
                SpriteRenderer sr = allRenderers[i];
                if (sr == null || sr.sprite == null)
                    continue;

                if (sr.transform.name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                return sr.sprite;
            }

            return null;
        }

        private static Image FindRowIcon(GameObject row)
        {
            if (row == null)
                return null;

            Image[] images = row.GetComponentsInChildren<Image>(true);
            if (images == null || images.Length == 0)
                return null;

            for (int i = 0; i < images.Length; i++)
            {
                Image img = images[i];
                if (img != null && img.name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0)
                    return img;
            }

            return images[0];
        }

        private Sprite ResolveIcon(HexWorldResourceId lootId)
        {
            return _iconByLoot.TryGetValue(lootId, out Sprite sprite) ? sprite : null;
        }

        private string ResolveResourceName(HexWorldResourceId lootId)
        {
            if (_resourceDefByLoot.TryGetValue(lootId, out DungeonResourceDefinition definition) &&
                !string.IsNullOrWhiteSpace(definition.resourceId))
            {
                return HumanizeIdentifier(definition.resourceId);
            }

            return HumanizeIdentifier(lootId.ToString());
        }

        private static string HumanizeIdentifier(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Unknown";

            string normalized = raw.Replace('_', ' ');
            var buffer = new List<char>(normalized.Length * 2);

            for (int i = 0; i < normalized.Length; i++)
            {
                char current = normalized[i];
                if (i > 0 && char.IsUpper(current) && normalized[i - 1] != ' ' && char.IsLower(normalized[i - 1]))
                    buffer.Add(' ');

                buffer.Add(current);
            }

            string spaced = new string(buffer.ToArray());
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced.ToLowerInvariant());
        }
    }
}
