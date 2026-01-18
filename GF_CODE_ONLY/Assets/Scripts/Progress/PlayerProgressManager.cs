// Assets/Scripts/Progress/PlayerProgressManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using GalacticFishing.Data;   // BoatDefinition, RodDefinition, RodDatabase, BoatDatabase, BoatUpgradeDatabase, BoatRuntimeStats

namespace GalacticFishing.Progress
{
    public class PlayerProgressManager : MonoBehaviour
    {
        /// <summary>
        /// Public API to add credits (used by CatchToInventory and other systems).
        /// </summary>
        public void AddCredits(float amount)
        {
            if (amount <= 0f) return;

            if (Data?.currency != null)
            {
                Data.currency.credits += amount;
                Debug.Log($"[PlayerProgress] Added {amount:N0} credits. New total: {Data.currency.credits:N0}");
            }
            else
            {
                Debug.LogWarning("[PlayerProgress] Attempted to add credits but Data is unavailable.");
            }
        }

        /// <summary>
        /// Public API to read the player's current credits.
        /// Used by UI (Workshop, etc) to display the balance.
        /// </summary>
        public float GetCredits()
        {
            if (Data?.currency == null)
                return 0f;

            return Data.currency.credits;
        }

        private const string SaveFileName = "player_progress.json";

        public static PlayerProgressManager Instance { get; private set; }

        [Header("Static data (optional)")]
        [SerializeField] private RodDatabase rodDatabase;
        [SerializeField] private BoatDatabase boatDatabase;
        [SerializeField] private BoatUpgradeDatabase boatUpgradeDatabase; // NEW

        [Header("Defaults")]
        [Tooltip("Rod id to automatically grant/equip on first load.")]
        [SerializeField] private string defaultStartingRodId = "rod_default";
        [SerializeField] private bool autoEquipDefaultRod = true;

        public PlayerSaveData Data { get; private set; } = new PlayerSaveData();

        private string SavePath =>
            Path.Combine(Application.persistentDataPath, SaveFileName);

        // -------------------------------------------------
        // Rod power modifiers (optional, future-proof)
        // -------------------------------------------------
        private struct RodPowerModifier
        {
            public float additive;    // +X power
            public float multiplier;  // *Y power
        }

        private readonly Dictionary<object, RodPowerModifier> _rodPowerModifiers =
            new Dictionary<object, RodPowerModifier>();

        /// <summary>
        /// Subscribe if some UI wants to refresh when rod power changes.
        /// </summary>
        public event Action RodPowerChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PlayerProgress] Duplicate manager in scene, destroying this one.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Load();
            EnsureDefaultRodUnlocked();
        }

        #region Save/Load

        public void Load()
        {
            if (File.Exists(SavePath))
            {
                var json = File.ReadAllText(SavePath);
                Data = JsonUtility.FromJson<PlayerSaveData>(json) ?? new PlayerSaveData();
                Debug.Log($"[PlayerProgress] Loaded from {SavePath}");
            }
            else
            {
                Data = new PlayerSaveData();
                Debug.Log("[PlayerProgress] No save, created fresh data");
            }

            // Ensure nested structures exist (safe for old saves)
            if (Data.gear == null) Data.gear = new PlayerGearData();
            if (Data.stats == null) Data.stats = new PlayerStatsData();
            if (Data.currency == null) Data.currency = new PlayerCurrencyData();

            if (Data.gear.ownedRodIds == null) Data.gear.ownedRodIds = new List<string>();
            if (Data.gear.ownedBoatIds == null) Data.gear.ownedBoatIds = new List<string>();
            if (Data.gear.unlockedBoatUpgradeIds == null) Data.gear.unlockedBoatUpgradeIds = new List<string>();

            // NEW persistent lists (safe for old saves)
            if (Data.gear.rodUpgradeLevels == null) Data.gear.rodUpgradeLevels = new List<PlayerGearData.LevelEntry>();
            if (Data.gear.workshopUpgradeLevels == null) Data.gear.workshopUpgradeLevels = new List<PlayerGearData.LevelEntry>();
        }

        public void Save()
        {
            var json = JsonUtility.ToJson(Data, true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"[PlayerProgress] Saved to {SavePath}");
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        #endregion

        #region Gear API – Rods

        /// <summary>
        /// Make sure the default starting rod is owned (and optionally equipped)
        /// so a fresh profile always has at least one rod.
        /// </summary>
        private void EnsureDefaultRodUnlocked()
        {
            if (Data == null || Data.gear == null)
                return;

            if (string.IsNullOrWhiteSpace(defaultStartingRodId))
                return;

            var owned = Data.gear.ownedRodIds;
            bool added = false;

            if (!owned.Contains(defaultStartingRodId))
            {
                owned.Add(defaultStartingRodId);
                added = true;
                Debug.Log($"[PlayerProgress] Auto-granted starting rod '{defaultStartingRodId}'");
            }

            // Equip it if nothing is equipped and auto-equip is allowed.
            if (autoEquipDefaultRod && string.IsNullOrEmpty(Data.gear.equippedRodId))
            {
                Data.gear.equippedRodId = defaultStartingRodId;
                if (!added)
                    Debug.Log($"[PlayerProgress] Auto-equipped starting rod '{defaultStartingRodId}'");

                RodPowerChanged?.Invoke();
            }

            // Optional: warn if the id is not in the database (helps catch typos)
            if (rodDatabase != null && !string.IsNullOrWhiteSpace(defaultStartingRodId))
            {
                var def = rodDatabase.GetById(defaultStartingRodId);
                if (def == null)
                {
                    Debug.LogWarning($"[PlayerProgress] Default starting rod id '{defaultStartingRodId}' not found in RodDatabase.");
                }
            }
        }

        public void UnlockRod(string rodId, bool autoEquipIfFirst = true)
        {
            if (string.IsNullOrWhiteSpace(rodId))
                return;

            var owned = Data.gear.ownedRodIds;
            if (!owned.Contains(rodId))
            {
                owned.Add(rodId);
                Debug.Log($"[PlayerProgress] Unlocked rod '{rodId}'");
            }

            if (autoEquipIfFirst && string.IsNullOrEmpty(Data.gear.equippedRodId))
            {
                EquipRod(rodId);
            }
        }

        public void EquipRod(string rodId)
        {
            if (!Data.gear.ownedRodIds.Contains(rodId))
            {
                Debug.LogWarning($"[PlayerProgress] Cannot equip rod '{rodId}' (not owned)");
                return;
            }

            Data.gear.equippedRodId = rodId;
            Debug.Log($"[PlayerProgress] Equipped rod '{rodId}'");

            RodPowerChanged?.Invoke();
        }

        /// <summary>
        /// Resolve the currently equipped rod definition from the RodDatabase.
        /// Returns null if nothing is equipped or the id is unknown.
        /// </summary>
        public RodDefinition CurrentRodDefinition
        {
            get
            {
                if (rodDatabase == null || Data == null || Data.gear == null)
                    return null;

                var id = Data.gear.equippedRodId;
                if (string.IsNullOrEmpty(id))
                    return null;

                return rodDatabase.GetById(id);
            }
        }

        /// <summary>
        /// Total rod power the player currently has equipped.
        /// Uses saved upgrade level + rod definition, then applies runtime modifiers.
        /// </summary>
        public float CurrentRodPower
        {
            get
            {
                var rod = CurrentRodDefinition;
                if (rod == null)
                    return 0f;

                int level = GetEquippedRodUpgradeLevel(); // persistent list
                level = ClampRodLevelToDefinition(rod, level);

                float basePower = ComputeRodPower(rod, level);

                // Apply any runtime modifiers (buffs, perks, etc)
                float finalPower = ApplyRodPowerModifiers(basePower);
                return finalPower;
            }
        }

        /// <summary>
        /// Simple helper: is this fish "too strong" for the current rod?
        /// </summary>
        public bool IsFishTooStrong(float fishPower)
        {
            return fishPower > CurrentRodPower;
        }

        #endregion

        #region Persisted Rod Upgrade Levels (NEW)

        public int GetRodUpgradeLevel(string rodId)
        {
            if (Data?.gear == null || string.IsNullOrWhiteSpace(rodId))
                return 0;

            var list = Data.gear.rodUpgradeLevels;
            if (list == null) return 0;

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e != null && string.Equals(e.id, rodId, StringComparison.Ordinal))
                    return Mathf.Max(0, e.level);
            }

            return 0;
        }

        public void SetRodUpgradeLevel(string rodId, int level)
        {
            if (Data?.gear == null || string.IsNullOrWhiteSpace(rodId))
                return;

            level = Mathf.Max(0, level);

            // Clamp to rod's max if we can resolve it
            var rod = (rodDatabase != null) ? rodDatabase.GetById(rodId) : null;
            int max = TryGetRodMaxUpgradeLevel(rod);
            if (max > 0) level = Mathf.Clamp(level, 0, max);

            var list = Data.gear.rodUpgradeLevels;
            if (list == null)
                list = Data.gear.rodUpgradeLevels = new List<PlayerGearData.LevelEntry>();

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e != null && string.Equals(e.id, rodId, StringComparison.Ordinal))
                {
                    if (e.level != level)
                    {
                        e.level = level;
                        RodPowerChanged?.Invoke();
                    }
                    return;
                }
            }

            list.Add(new PlayerGearData.LevelEntry { id = rodId, level = level });
            RodPowerChanged?.Invoke();
        }

        public int GetEquippedRodUpgradeLevel()
        {
            var id = Data?.gear?.equippedRodId;
            if (string.IsNullOrEmpty(id)) return 0;
            return GetRodUpgradeLevel(id);
        }

        public void SetEquippedRodUpgradeLevel(int level)
        {
            var id = Data?.gear?.equippedRodId;
            if (string.IsNullOrEmpty(id)) return;
            SetRodUpgradeLevel(id, level);
        }

        public void IncreaseEquippedRodUpgradeLevel(int delta)
        {
            delta = Mathf.Max(0, delta);
            if (delta == 0) return;

            int current = GetEquippedRodUpgradeLevel();
            SetEquippedRodUpgradeLevel(current + delta);
        }

        #endregion

        #region Workshop Upgrade Levels (generic, NEW)

        public int GetWorkshopUpgradeLevel(string upgradeId)
        {
            if (Data?.gear == null || string.IsNullOrWhiteSpace(upgradeId))
                return 0;

            var list = Data.gear.workshopUpgradeLevels;
            if (list == null) return 0;

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e != null && string.Equals(e.id, upgradeId, StringComparison.Ordinal))
                    return Mathf.Max(0, e.level);
            }

            return 0;
        }

        public void SetWorkshopUpgradeLevel(string upgradeId, int level)
        {
            if (Data?.gear == null || string.IsNullOrWhiteSpace(upgradeId))
                return;

            level = Mathf.Max(0, level);

            var list = Data.gear.workshopUpgradeLevels;
            if (list == null)
                list = Data.gear.workshopUpgradeLevels = new List<PlayerGearData.LevelEntry>();

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e != null && string.Equals(e.id, upgradeId, StringComparison.Ordinal))
                {
                    e.level = level;
                    return;
                }
            }

            list.Add(new PlayerGearData.LevelEntry { id = upgradeId, level = level });
        }

        #endregion

        #region Rod Power Modifiers API (RESTORED)

        /// <summary>
        /// Add or replace a rod power modifier. Use a stable key (like "Buff_X", this component, etc).
        /// additive: +power
        /// multiplier: *power (use 1 for none)
        /// </summary>
        public void SetRodPowerModifier(object key, float additive, float multiplier = 1f)
        {
            if (key == null) return;

            _rodPowerModifiers[key] = new RodPowerModifier
            {
                additive = additive,
                multiplier = (Mathf.Approximately(multiplier, 0f) ? 1f : multiplier)
            };

            RodPowerChanged?.Invoke();
        }

        public void RemoveRodPowerModifier(object key)
        {
            if (key == null) return;

            if (_rodPowerModifiers.Remove(key))
            {
                RodPowerChanged?.Invoke();
            }
        }

        private float ApplyRodPowerModifiers(float basePower)
        {
            float add = 0f;
            float mul = 1f;

            foreach (var kv in _rodPowerModifiers)
            {
                add += kv.Value.additive;
                mul *= kv.Value.multiplier;
            }

            float outPower = (basePower + add) * mul;
            return Mathf.Max(0f, outPower);
        }

        #endregion

        #region RodDefinition helpers (safe reflection)

        private static int ClampRodLevelToDefinition(RodDefinition rod, int level)
        {
            level = Mathf.Max(0, level);
            if (rod == null) return level;

            int max = TryGetRodMaxUpgradeLevel(rod);
            if (max > 0)
                return Mathf.Clamp(level, 0, max);

            return level;
        }

        private static int TryGetRodMaxUpgradeLevel(RodDefinition rod)
        {
            if (rod == null) return 0;

            var t = rod.GetType();

            // field
            var f = t.GetField("maxUpgradeLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(int))
                return (int)f.GetValue(rod);

            // property
            var p = t.GetProperty("maxUpgradeLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(int) && p.GetIndexParameters().Length == 0)
                return (int)p.GetValue(rod);

            return 0;
        }

        private static float ComputeRodPower(RodDefinition rod, int level)
        {
            if (rod == null) return 0f;
            level = Mathf.Max(0, level);

            var t = rod.GetType();

            // 1) Prefer RodDefinition.GetTotalPower(int) if present
            var m = t.GetMethod("GetTotalPower",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(int) },
                null);

            if (m != null && m.ReturnType == typeof(float))
            {
                try { return (float)m.Invoke(rod, new object[] { level }); }
                catch { /* fall through */ }
            }

            // 2) Fall back to basePower + powerPerUpgradeLevel * level if present
            float basePower = rod.basePower;

            var f = t.GetField("powerPerUpgradeLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float))
            {
                float per = (float)f.GetValue(rod);
                return basePower + (per * level);
            }

            var p = t.GetProperty("powerPerUpgradeLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(float) && p.GetIndexParameters().Length == 0)
            {
                float per = (float)p.GetValue(rod);
                return basePower + (per * level);
            }

            // 3) Worst case
            return basePower;
        }

        #endregion

        #region Gear API – Boats

        public void UnlockBoat(string boatId, bool autoEquipIfFirst = true)
        {
            if (string.IsNullOrWhiteSpace(boatId))
                return;

            var owned = Data.gear.ownedBoatIds;
            if (!owned.Contains(boatId))
            {
                owned.Add(boatId);
                Debug.Log($"[PlayerProgress] Unlocked boat '{boatId}'");
            }

            if (autoEquipIfFirst && string.IsNullOrEmpty(Data.gear.equippedBoatId))
            {
                EquipBoat(boatId);
            }
        }

        public void EquipBoat(string boatId)
        {
            if (!Data.gear.ownedBoatIds.Contains(boatId))
            {
                Debug.LogWarning($"[PlayerProgress] Cannot equip boat '{boatId}' (not owned)");
                return;
            }

            Data.gear.equippedBoatId = boatId;
            Debug.Log($"[PlayerProgress] Equipped boat '{boatId}'");
        }

        #endregion

        #region Boat Upgrades API

        public void UnlockBoatUpgrade(string upgradeId)
        {
            if (string.IsNullOrWhiteSpace(upgradeId))
                return;

            var list = Data.gear.unlockedBoatUpgradeIds;
            if (!list.Contains(upgradeId))
            {
                list.Add(upgradeId);
                Debug.Log($"[PlayerProgress] Unlocked boat upgrade '{upgradeId}'");
            }
        }

        public bool HasBoatUpgrade(string upgradeId)
        {
            if (string.IsNullOrWhiteSpace(upgradeId))
                return false;

            return Data.gear.unlockedBoatUpgradeIds != null &&
                   Data.gear.unlockedBoatUpgradeIds.Contains(upgradeId);
        }

        public bool TryBuildCurrentBoatStats(out BoatRuntimeStats stats)
        {
            stats = default;

            if (boatDatabase == null)
            {
                Debug.LogWarning("[PlayerProgress] No BoatDatabase assigned.");
                return false;
            }

            var boatId = Data.gear.equippedBoatId;
            if (string.IsNullOrEmpty(boatId))
            {
                Debug.LogWarning("[PlayerProgress] No equippedBoatId in save data.");
                return false;
            }

            var boat = boatDatabase.GetById(boatId);
            if (boat == null)
            {
                Debug.LogWarning($"[PlayerProgress] Boat id '{boatId}' not found in BoatDatabase.");
                return false;
            }

            stats = BoatRuntimeStats.FromBaseBoat(boat);

            if (boatUpgradeDatabase == null ||
                Data.gear.unlockedBoatUpgradeIds == null ||
                Data.gear.unlockedBoatUpgradeIds.Count == 0)
            {
                return true;
            }

            foreach (var upgradeId in Data.gear.unlockedBoatUpgradeIds)
            {
                if (string.IsNullOrWhiteSpace(upgradeId))
                    continue;

                var def = boatUpgradeDatabase.GetById(upgradeId);
                if (def != null)
                {
                    stats.ApplyUpgrade(def);
                }
                else
                {
                    Debug.LogWarning($"[PlayerProgress] Boat upgrade id '{upgradeId}' not found in BoatUpgradeDatabase.");
                }
            }

            return true;
        }

        #endregion

        #region Stats API – global fishing stats

        public void RegisterCast(bool manual)
        {
            if (manual) Data.stats.manualCasts++;
            else        Data.stats.autoCasts++;
        }

        public void RegisterCatch(float weightKg, bool manual)
        {
            Data.stats.totalFishCaught++;
            Data.stats.totalKgCaught += Mathf.Max(0f, weightKg);

            if (manual) Data.stats.manualCatches++;
            else        Data.stats.autoCatches++;
        }

        #endregion
    }
}
