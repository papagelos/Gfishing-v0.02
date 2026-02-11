using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Upgrades
{
    /// <summary>
    /// Optional "database-like" upgrade effects store.
    /// Each entry is keyed by the SAME saveKey format used by ShopListUI:
    ///   shop:<catalogId>:<itemId>
    /// </summary>
    [CreateAssetMenu(menuName = "Galactic Fishing/Shop/Upgrade Effects Database", fileName = "UpgradeEffectsDatabase")]
    public sealed class UpgradeEffectsDatabase : ScriptableObject
    {
        public List<UpgradeEffectEntry> entries = new();
    }

    [Serializable]
    public sealed class UpgradeEffectEntry
    {
        [Tooltip("Must match ShopListUI save key format: shop:<catalogId>:<itemId>")]
        public string saveKey;

        [TextArea]
        public string notes;

        public List<UpgradeEffect> effects = new();
    }

    [Serializable]
    public sealed class UpgradeEffect
    {
        [Tooltip("A gameplay stat key your scripts will query. Example: rod_power_add, minigame_window_ms, fish_value_mult")]
        public string statKey;

        [Tooltip("Value at level 0. (Usually 0 for additive stats, or 1 for multiplier stats.)")]
        public float baseValue = 0f;

        [Tooltip("Added per purchased level. Final = baseValue + perLevel * level")]
        public float perLevel = 0f;

        [Header("Safety rails (optional)")]
        public bool clamp = false;
        public float min = 0f;
        public float max = 999999f;

        public float Evaluate(int level)
        {
            level = Mathf.Max(0, level);
            float v = baseValue + perLevel * level;
            if (clamp) v = Mathf.Clamp(v, min, max);
            return v;
        }
    }
}
