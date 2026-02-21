using System;
using UnityEngine;
using GalacticFishing.Progress;

namespace GalacticFishing.Upgrades
{
    /// <summary>
    /// Runtime helper:
    /// - Builds save keys in the same format as ShopListUI: shop:<catalogId>:<itemId>
    /// - Reads current level from PlayerProgressManager workshop upgrades
    /// - Evaluates effects from UpgradeEffectsDatabase
    ///
    /// To make this "just work", place an UpgradeEffectsDatabase asset at:
    ///   Assets/Resources/UpgradeEffectsDatabase.asset
    /// so Resources.Load can find it.
    /// </summary>
    public static class UpgradeService
    {
        private static UpgradeEffectsDatabase _db;

        public static UpgradeEffectsDatabase Database
        {
            get
            {
                if (_db == null)
                    _db = Resources.Load<UpgradeEffectsDatabase>("UpgradeEffectsDatabase");
                return _db;
            }
            set => _db = value;
        }

        public static string BuildSaveKey(string catalogId, string itemId)
        {
            catalogId = string.IsNullOrWhiteSpace(catalogId) ? "catalog" : catalogId.Trim();
            itemId = string.IsNullOrWhiteSpace(itemId) ? "item" : itemId.Trim();
            return $"shop:{catalogId}:{itemId}";
        }

        public static int GetLevel(string catalogId, string itemId)
        {
            var ppm = PlayerProgressManager.Instance;
            if (ppm == null) return 0;

            string key = BuildSaveKey(catalogId, itemId);
            return ppm.GetWorkshopUpgradeLevel(key);
        }

        public static float GetValue(string catalogId, string itemId, string statKey, float defaultValue = 0f)
        {
            int level = GetLevel(catalogId, itemId);
            string key = BuildSaveKey(catalogId, itemId);
            return GetValueBySaveKey(key, statKey, level, defaultValue);
        }

        /// <summary>
        /// Convenience evaluator used by gameplay systems that map to the standard
        /// dungeon mining radius stat.
        /// </summary>
        public static float Evaluate(string catalogId, string itemId)
        {
            return Evaluate(catalogId, itemId, "dungeon_mining_radius", 0f);
        }

        /// <summary>
        /// Evaluates a stat for a catalog/item pair.
        /// Tries current ShopList key format first, then legacy flat key format.
        /// </summary>
        public static float Evaluate(string catalogId, string itemId, string statKey, float defaultValue = 0f)
        {
            if (string.IsNullOrWhiteSpace(statKey))
                return defaultValue;

            var ppm = PlayerProgressManager.Instance;

            // 1) Current format: shop:<catalogId>:<itemId>
            string shopKey = BuildSaveKey(catalogId, itemId);
            int shopLevel = ppm != null ? Mathf.Max(0, ppm.GetWorkshopUpgradeLevel(shopKey)) : 0;
            float value = GetValueBySaveKey(shopKey, statKey, shopLevel, float.NaN);
            if (!float.IsNaN(value))
                return value;

            // 2) Legacy flat key fallback: <catalogId>_<itemId>
            string legacyKey = $"{(catalogId ?? string.Empty).Trim()}_{(itemId ?? string.Empty).Trim()}";
            if (!string.IsNullOrWhiteSpace(legacyKey) && ppm != null)
            {
                int legacyLevel = Mathf.Max(0, ppm.GetWorkshopUpgradeLevel(legacyKey));
                value = GetValueBySaveKey(legacyKey, statKey, legacyLevel, float.NaN);
                if (!float.IsNaN(value))
                    return value;
            }

            return defaultValue;
        }

        public static float GetValueBySaveKey(string saveKey, string statKey, int level, float defaultValue = 0f)
        {
            if (string.IsNullOrWhiteSpace(saveKey) || string.IsNullOrWhiteSpace(statKey))
                return defaultValue;

            var db = Database;
            if (db == null || db.entries == null)
                return defaultValue;

            for (int i = 0; i < db.entries.Count; i++)
            {
                var e = db.entries[i];
                if (e == null) continue;
                if (!string.Equals(e.saveKey, saveKey, StringComparison.Ordinal)) continue;

                var list = e.effects;
                if (list == null) return defaultValue;

                for (int j = 0; j < list.Count; j++)
                {
                    var fx = list[j];
                    if (fx == null) continue;
                    if (!string.Equals(fx.statKey, statKey, StringComparison.Ordinal)) continue;
                    return fx.Evaluate(level);
                }

                return defaultValue;
            }

            return defaultValue;
        }
    }
}
