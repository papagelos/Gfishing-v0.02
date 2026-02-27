using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using GalacticFishing.Data;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GalacticFishing.Minigames.Dungeon3D
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class DungeonHotbarController : MonoBehaviour
    {
        private const string DungeonGemRegistryAssetPath = "Assets/Minigames/Dungeon3D/Definitions/DungeonGemRegistry_Main.asset";
        private const string GemsFolder = "Assets/Sprites/Gems";
        private const string GemPrefix = "gem_";
        private const int SlotCount = 7;
        private const string PlayerPrefsAssignmentsKey = "DungeonHotbar.Assignments.v1";

        [Header("Popup Behavior")]
        [SerializeField, Min(0f)] private float popupYOffset = 10f;
        [SerializeField, Min(0f)] private float popupHorizontalMargin = 12f;
        [SerializeField] private DungeonGemRegistry gemRegistry;
        [SerializeField] private bool rediscoverGemsOnEnable = true;

        [Header("Selection (Debug/Runtime)")]
        [SerializeField] private GemId[] selectedGemIds = new GemId[SlotCount];

        [Serializable]
        private sealed class GemEntry
        {
            public GemId gemId;
            public string idText;
            public string displayName;
            public string description;
            public Sprite sprite;
        }

        [Serializable]
        private sealed class HotbarAssignmentSaveData
        {
            public int[] selectedGemIds = new int[SlotCount];
            public bool[] autocastEnabled = new bool[SlotCount];
        }

        private UIDocument _doc;
        private VisualElement _root;
        private Button[] _slots;
        private Action[] _slotClickHandlers;
        private VisualElement _popupRoot;
        private VisualElement _popupRow;
        private Label _popupTitle;
        private Toggle _popupAutocastToggle;

        private readonly List<GemEntry> _gems = new();
        private readonly Dictionary<int, float> _slotCooldownUntil = new();
        private readonly bool[] _autocastEnabled = new bool[SlotCount];
        private int _activePopupSlot = -1;

        public IReadOnlyList<GemId> SelectedGemIds => selectedGemIds;
        public event Action<int, GemId> CastRequested;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc == null)
            {
                Debug.LogError("[DungeonHotbarController] Missing UIDocument.", this);
                return;
            }

            _root = _doc.rootVisualElement;
            if (_root == null)
                return;

            CacheSlots();
            EnsurePopupUi();
            RegisterCallbacks();

            if (rediscoverGemsOnEnable || _gems.Count == 0)
                LoadGems();

            LoadAssignments();
            ApplySelectionsToSlots();
            HidePopup();
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
        }

        private void Update()
        {
            HandleHotbarCastInput();
        }

        private void CacheSlots()
        {
            _slots = new Button[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                _slots[i] = _root.Q<Button>($"DungeonSlot{i}");
            }
        }

        private void RegisterCallbacks()
        {
            if (_slots == null)
                return;

            if (_slotClickHandlers == null || _slotClickHandlers.Length != SlotCount)
                _slotClickHandlers = new Action[SlotCount];

            for (int i = 0; i < _slots.Length; i++)
            {
                int slotIndex = i;
                Button slot = _slots[i];
                if (slot == null)
                    continue;

                _slotClickHandlers[i] ??= () => TogglePopupForSlot(slotIndex);
                slot.clicked -= _slotClickHandlers[i];
                slot.clicked += _slotClickHandlers[i];
            }

            if (_root != null)
                _root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        }

        private void UnregisterCallbacks()
        {
            if (_slots != null && _slotClickHandlers != null)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    Button slot = _slots[i];
                    if (slot == null)
                        continue;
                    if (i < _slotClickHandlers.Length && _slotClickHandlers[i] != null)
                        slot.clicked -= _slotClickHandlers[i];
                }
            }

            if (_root != null)
                _root.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        }

        private void EnsurePopupUi()
        {
            if (_root == null)
                return;

            _popupRoot ??= _root.Q<VisualElement>("GemPopupRoot");
            if (_popupRoot == null)
            {
                _popupRoot = new VisualElement { name = "GemPopupRoot" };
                _popupRoot.AddToClassList("dungeon-gem-popup");
                _popupRoot.style.position = Position.Absolute;
                _popupRoot.style.display = DisplayStyle.None;
                _root.Add(_popupRoot);
            }

            _popupTitle ??= _popupRoot.Q<Label>("GemPopupTitle");
            if (_popupTitle == null)
            {
                _popupTitle = new Label("SELECT GEM") { name = "GemPopupTitle" };
                _popupTitle.AddToClassList("dungeon-gem-popup-title");
                _popupRoot.Add(_popupTitle);
            }

            _popupRow ??= _popupRoot.Q<VisualElement>("GemPopupRow");
            if (_popupRow == null)
            {
                _popupRow = new VisualElement { name = "GemPopupRow" };
                _popupRow.AddToClassList("dungeon-gem-popup-row");
                _popupRoot.Add(_popupRow);
            }

            _popupAutocastToggle ??= _popupRoot.Q<Toggle>("GemPopupAutocastToggle");
            if (_popupAutocastToggle == null)
            {
                _popupAutocastToggle = new Toggle("Autocast on cooldown")
                {
                    name = "GemPopupAutocastToggle"
                };
                _popupAutocastToggle.AddToClassList("dungeon-gem-popup-toggle");
                _popupAutocastToggle.RegisterValueChangedCallback(evt =>
                {
                    int slot = _activePopupSlot;
                    if (slot < 0 || slot >= SlotCount)
                        return;

                    _autocastEnabled[slot] = evt.newValue;
                    UpdateSlotAutocastVisual(slot);
                    SaveAssignments();
                });

                _popupRoot.Insert(1, _popupAutocastToggle);
            }
        }

        private void LoadGems()
        {
            _gems.Clear();

            if (TryLoadGemsFromRegistry())
                return;

            DiscoverGemsFallback();
        }

        private bool TryLoadGemsFromRegistry()
        {
#if UNITY_EDITOR
            if (gemRegistry == null)
                gemRegistry = AssetDatabase.LoadAssetAtPath<DungeonGemRegistry>(DungeonGemRegistryAssetPath);
#endif
            if (gemRegistry == null || gemRegistry.gems == null)
                return false;

            for (int i = 0; i < gemRegistry.gems.Count; i++)
            {
                var row = gemRegistry.gems[i];
                if (row == null || row.gemId == GemId.None)
                    continue;

                _gems.Add(new GemEntry
                {
                    gemId = row.gemId,
                    idText = row.gemId.ToString(),
                    displayName = ToDisplayName(row.gemId.ToString()),
                    description = row.description,
                    sprite = row.icon
                });
            }

            _gems.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
            return _gems.Count > 0;
        }

        private void DiscoverGemsFallback()
        {
            _gems.Clear();

#if UNITY_EDITOR
            if (!AssetDatabase.IsValidFolder(GemsFolder))
            {
                Debug.LogWarning($"[DungeonHotbarController] Gems folder not found: {GemsFolder}", this);
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { GemsFolder });
            var seenSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                    continue;

                string raw = sprite.name ?? string.Empty;
                if (!raw.StartsWith(GemPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string skillId = ExtractSkillId(raw);
                if (string.IsNullOrWhiteSpace(skillId) || !seenSkills.Add(skillId))
                    continue;

                _gems.Add(new GemEntry
                {
                    gemId = TryParseGemId(skillId),
                    idText = skillId,
                    displayName = ToDisplayName(skillId),
                    sprite = sprite
                });
            }

            _gems.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
#else
            Debug.LogWarning("[DungeonHotbarController] Gem discovery uses AssetDatabase and is only available in the Unity Editor.", this);
#endif
        }

        private static GemId TryParseGemId(string rawId)
        {
            if (string.IsNullOrWhiteSpace(rawId))
                return GemId.None;

            return Enum.TryParse(rawId, true, out GemId parsed) ? parsed : GemId.None;
        }

        private static string ExtractSkillId(string spriteName)
        {
            if (string.IsNullOrWhiteSpace(spriteName))
                return string.Empty;

            string value = spriteName.Trim();
            if (value.StartsWith(GemPrefix, StringComparison.OrdinalIgnoreCase))
                value = value.Substring(GemPrefix.Length);

            return value.Trim('_', ' ');
        }

        private static string ToDisplayName(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return "Gem";

            string value = skillId.Replace('_', ' ').Trim();
            if (string.IsNullOrEmpty(value))
                return "Gem";

            return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
        }

        private void TogglePopupForSlot(int slotIndex)
        {
            if (_slots == null || slotIndex < 0 || slotIndex >= _slots.Length || _slots[slotIndex] == null)
                return;

            if (_activePopupSlot == slotIndex && _popupRoot != null && _popupRoot.resolvedStyle.display != DisplayStyle.None)
            {
                HidePopup();
                return;
            }

            BuildPopupChoices(slotIndex);
            _activePopupSlot = slotIndex;
            PositionPopupOverSlot(slotIndex);
            _popupRoot.style.display = DisplayStyle.Flex;
        }

        private void BuildPopupChoices(int slotIndex)
        {
            EnsurePopupUi();
            if (_popupRoot == null || _popupRow == null)
                return;

            _popupRow.Clear();
            _popupTitle.text = $"SELECT GEM • SLOT {slotIndex + 1}";
            if (_popupAutocastToggle != null)
                _popupAutocastToggle.SetValueWithoutNotify(_autocastEnabled[slotIndex]);

            if (_gems.Count == 0)
            {
                var emptyLabel = new Label("No gems found in Assets/Sprites/Gems (looking for gem_*)");
                emptyLabel.AddToClassList("dungeon-gem-popup-empty");
                _popupRow.Add(emptyLabel);
                return;
            }

            for (int i = 0; i < _gems.Count; i++)
            {
                GemEntry gem = _gems[i];
                if (gem == null)
                    continue;

                var btn = new Button();
                string gemName = !string.IsNullOrWhiteSpace(gem.idText) ? gem.idText : gem.gemId.ToString();
                btn.name = $"GemChoice_{gemName}";
                btn.AddToClassList("card-btn");
                btn.AddToClassList("dungeon-gem-choice");
                btn.text = gem.displayName;
                btn.tooltip = string.IsNullOrWhiteSpace(gem.description) ? gemName : $"{gemName}\n{gem.description}";

                if (gem.sprite != null)
                    btn.style.backgroundImage = new StyleBackground(gem.sprite);

                if (slotIndex < selectedGemIds.Length && selectedGemIds[slotIndex] == gem.gemId)
                    btn.AddToClassList("is-selected");

                int capturedSlot = slotIndex;
                GemEntry capturedGem = gem;
                btn.clicked += () => OnGemPicked(capturedSlot, capturedGem);
                _popupRow.Add(btn);
            }
        }

        private void OnGemPicked(int slotIndex, GemEntry gem)
        {
            if (gem == null || slotIndex < 0 || slotIndex >= SlotCount)
                return;

            if (slotIndex >= selectedGemIds.Length)
                Array.Resize(ref selectedGemIds, SlotCount);
            selectedGemIds[slotIndex] = gem.gemId;
            SaveAssignments();
            UpdateSlotAutocastVisual(slotIndex);

            Button slot = (_slots != null && slotIndex < _slots.Length) ? _slots[slotIndex] : null;
            if (slot != null)
            {
                if (gem.sprite != null)
                    slot.style.backgroundImage = new StyleBackground(gem.sprite);
                slot.text = string.Empty;
                string gemName = !string.IsNullOrWhiteSpace(gem.idText) ? gem.idText : gem.gemId.ToString();
                slot.tooltip = $"{slotIndex + 1}: {gem.displayName} ({gemName})";
                slot.AddToClassList("has-gem");
            }

            HidePopup();
        }

        private void SaveAssignments()
        {
            try
            {
                if (selectedGemIds == null || selectedGemIds.Length != SlotCount)
                {
                    Array.Resize(ref selectedGemIds, SlotCount);
                }

                var data = new HotbarAssignmentSaveData();
                data.selectedGemIds = new int[SlotCount];
                data.autocastEnabled = new bool[SlotCount];

                for (int i = 0; i < SlotCount; i++)
                {
                    data.selectedGemIds[i] = (int)selectedGemIds[i];
                    data.autocastEnabled[i] = _autocastEnabled != null && i < _autocastEnabled.Length && _autocastEnabled[i];
                }

                string json = JsonUtility.ToJson(data);
                PlayerPrefs.SetString(PlayerPrefsAssignmentsKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DungeonHotbarController] Failed to save hotbar assignments: {ex.Message}", this);
            }
        }

        private void LoadAssignments()
        {
            if (!PlayerPrefs.HasKey(PlayerPrefsAssignmentsKey))
            {
                EnsureSelectedArraySize();
                return;
            }

            try
            {
                string json = PlayerPrefs.GetString(PlayerPrefsAssignmentsKey, string.Empty);
                if (string.IsNullOrWhiteSpace(json))
                {
                    EnsureSelectedArraySize();
                    return;
                }

                var data = JsonUtility.FromJson<HotbarAssignmentSaveData>(json);
                if (data == null || data.selectedGemIds == null)
                {
                    EnsureSelectedArraySize();
                    return;
                }

                EnsureSelectedArraySize();
                int count = Mathf.Min(SlotCount, data.selectedGemIds.Length);
                for (int i = 0; i < count; i++)
                {
                    GemId id = (GemId)data.selectedGemIds[i];
                    selectedGemIds[i] = Enum.IsDefined(typeof(GemId), id) ? id : GemId.None;
                }

                for (int i = count; i < SlotCount; i++)
                    selectedGemIds[i] = GemId.None;

                if (data.autocastEnabled != null)
                {
                    int autoCount = Mathf.Min(SlotCount, data.autocastEnabled.Length);
                    for (int i = 0; i < autoCount; i++)
                        _autocastEnabled[i] = data.autocastEnabled[i];

                    for (int i = autoCount; i < SlotCount; i++)
                        _autocastEnabled[i] = false;
                }
                else
                {
                    for (int i = 0; i < SlotCount; i++)
                        _autocastEnabled[i] = false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DungeonHotbarController] Failed to load hotbar assignments: {ex.Message}", this);
                EnsureSelectedArraySize();
                for (int i = 0; i < SlotCount; i++)
                    _autocastEnabled[i] = false;
            }
        }

        private void EnsureSelectedArraySize()
        {
            if (selectedGemIds == null || selectedGemIds.Length != SlotCount)
                Array.Resize(ref selectedGemIds, SlotCount);
        }

        private void ApplySelectionsToSlots()
        {
            if (_slots == null)
                return;

            for (int i = 0; i < _slots.Length; i++)
            {
                Button slot = _slots[i];
                if (slot == null)
                    continue;

                GemId selectedId = (selectedGemIds != null && i < selectedGemIds.Length) ? selectedGemIds[i] : GemId.None;
                if (selectedId == GemId.None)
                {
                    slot.text = i == 6 ? "RMB" : (i + 1).ToString();
                    slot.tooltip = $"Slot {i + 1}";
                    slot.style.backgroundImage = StyleKeyword.None;
                    slot.RemoveFromClassList("has-gem");
                    UpdateSlotAutocastVisual(i);
                    continue;
                }

                GemEntry gem = _gems.FirstOrDefault(g => g.gemId == selectedId);
                if (gem?.sprite != null)
                    slot.style.backgroundImage = new StyleBackground(gem.sprite);

                slot.text = string.Empty;
                string gemName = gem != null && !string.IsNullOrWhiteSpace(gem.idText) ? gem.idText : selectedId.ToString();
                slot.tooltip = $"{i + 1}: {ToDisplayName(gemName)} ({gemName})";
                slot.AddToClassList("has-gem");
                UpdateSlotAutocastVisual(i);
            }
        }

        private void PositionPopupOverSlot(int slotIndex)
        {
            if (_popupRoot == null || _root == null || _slots == null || slotIndex < 0 || slotIndex >= _slots.Length)
                return;

            Button slot = _slots[slotIndex];
            if (slot == null)
                return;

            Rect rootRect = _root.worldBound;
            Rect slotRect = slot.worldBound;

            float popupWidth = Mathf.Max(220f, _popupRoot.resolvedStyle.width);
            float left = (slotRect.center.x - rootRect.xMin) - (popupWidth * 0.5f);
            float maxLeft = Mathf.Max(0f, _root.layout.width - popupWidth - popupHorizontalMargin);
            left = Mathf.Clamp(left, popupHorizontalMargin, maxLeft);

            float bottom = (rootRect.yMax - slotRect.yMin) + popupYOffset;

            _popupRoot.style.left = left;
            _popupRoot.style.bottom = bottom;
        }

        private void HidePopup()
        {
            _activePopupSlot = -1;
            if (_popupRoot != null)
                _popupRoot.style.display = DisplayStyle.None;
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            if (_activePopupSlot >= 0 && _popupRoot != null && _popupRoot.resolvedStyle.display != DisplayStyle.None)
                PositionPopupOverSlot(_activePopupSlot);
        }

        private void HandleHotbarCastInput()
        {
            uint requestedMask = 0u;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.isPressed || kb.numpad1Key.isPressed) requestedMask |= (1u << 0);
                if (kb.digit2Key.isPressed || kb.numpad2Key.isPressed) requestedMask |= (1u << 1);
                if (kb.digit3Key.isPressed || kb.numpad3Key.isPressed) requestedMask |= (1u << 2);
                if (kb.digit4Key.isPressed || kb.numpad4Key.isPressed) requestedMask |= (1u << 3);
                if (kb.digit5Key.isPressed || kb.numpad5Key.isPressed) requestedMask |= (1u << 4);
                if (kb.digit6Key.isPressed || kb.numpad6Key.isPressed) requestedMask |= (1u << 5);
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
                requestedMask |= (1u << 6);

            for (int i = 0; i < SlotCount; i++)
            {
                if (_autocastEnabled[i])
                    requestedMask |= (1u << i);
            }

            for (int i = 0; i < SlotCount; i++)
            {
                if ((requestedMask & (1u << i)) != 0u)
                    TriggerSlotCast(i);
            }
        }

        private void TriggerSlotCast(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
                return;

            GemId assignedGemId = (selectedGemIds != null && slotIndex < selectedGemIds.Length)
                ? selectedGemIds[slotIndex]
                : GemId.None;

            PulseSlot(slotIndex);

            if (assignedGemId == GemId.None)
                return;

            if (IsSlotOnCooldown(slotIndex))
                return;

            int humanSlot = slotIndex + 1;
            Debug.Log($"[SkillCast] Casting {assignedGemId} from slot {humanSlot}!", this);
            CastRequested?.Invoke(slotIndex, assignedGemId);
            BeginSlotCooldown(slotIndex, assignedGemId);
        }

        private void PulseSlot(int slotIndex)
        {
            if (_slots == null || slotIndex < 0 || slotIndex >= _slots.Length)
                return;

            Button slot = _slots[slotIndex];
            if (slot == null)
                return;

            slot.AddToClassList("is-hotkey-pressed");
            slot.style.opacity = 0.72f;
            slot.style.unityBackgroundImageTintColor = new StyleColor(new Color(1f, 0.92f, 0.55f, 1f));

            slot.schedule.Execute(() =>
            {
                if (slot == null)
                    return;

                slot.RemoveFromClassList("is-hotkey-pressed");
                slot.style.opacity = 1f;
                slot.style.unityBackgroundImageTintColor = StyleKeyword.Null;
            }).ExecuteLater(120);
        }

        private void UpdateSlotAutocastVisual(int slotIndex)
        {
            if (_slots == null || slotIndex < 0 || slotIndex >= _slots.Length)
                return;

            Button slot = _slots[slotIndex];
            if (slot == null)
                return;

            if (_autocastEnabled[slotIndex])
                slot.AddToClassList("is-autocasting");
            else
                slot.RemoveFromClassList("is-autocasting");
        }

        private bool IsSlotOnCooldown(int slotIndex)
        {
            if (!_slotCooldownUntil.TryGetValue(slotIndex, out float until))
                return false;

            if (Time.time >= until)
            {
                _slotCooldownUntil.Remove(slotIndex);
                return false;
            }

            return true;
        }

        private void BeginSlotCooldown(int slotIndex, GemId gemId)
        {
            if (slotIndex < 0 || gemId == GemId.None)
                return;

            float cooldown = ResolveGemCooldown(gemId);
            if (cooldown <= 0f)
            {
                _slotCooldownUntil.Remove(slotIndex);
                return;
            }

            _slotCooldownUntil[slotIndex] = Time.time + cooldown;
        }

        private float ResolveGemCooldown(GemId gemId)
        {
            if (gemRegistry == null || gemRegistry.gems == null)
                return 0f;

            for (int i = 0; i < gemRegistry.gems.Count; i++)
            {
                var row = gemRegistry.gems[i];
                if (row == null || row.gemId != gemId || row.skillDefinition == null)
                    continue;

                return Mathf.Max(0f, row.skillDefinition.cooldown);
            }

            return 0f;
        }
    }
}
