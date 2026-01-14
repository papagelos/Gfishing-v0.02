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
