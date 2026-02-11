// Assets/Scripts/Data/RodDefinition.cs
using UnityEngine;

namespace GalacticFishing.Data
{
    public enum RodRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Prototype
    }

    [CreateAssetMenu(
        menuName = "Galactic Fishing/Gear/Rod",
        fileName = "Rod_")]
    public class RodDefinition : ScriptableObject
    {
        // --------------------------------------------------------------------
        // Identity / display
        // --------------------------------------------------------------------

        [Header("Id (stable, used in saves)")]
        [Tooltip("Stable string ID used in save data, e.g. 'rod_basic_01'.")]
        public string id = "rod_default";

        [Header("Display")]
        public string displayName = "Basic Rod";
        [TextArea(2, 4)]
        public string description;

        [Header("Progression / rarity")]
        public RodRarity rarity = RodRarity.Common;

        [Tooltip("Rough progression tier for shop/drop tables.")]
        public int tier = 1;

        [Tooltip("Museum clearance level required to buy/equip this rod.")]
        public int clearanceLevelRequired = 1;

        // --------------------------------------------------------------------
        // Core power & scaling
        // --------------------------------------------------------------------

        [Header("Core Power & Scaling")]
        [Tooltip("Base contribution to Total Fishing Power.")]
        public float basePower = 1f;

        [Tooltip("Extra power per upgrade level.")]
        public float powerPerUpgradeLevel = 0.5f;

        [Tooltip("Max upgrade levels (stars) for this rod.")]
        public int maxUpgradeLevel = 10;

        [Range(1, 100)]
        [Tooltip("Fish Quality where this rod still feels comfortable.")]
        public int qualityCap = 50;

        [Tooltip("How quickly difficulty ramps when fish Q > qualityCap.")]
        public float overcapDifficultyScale = 1.0f;

        // --------------------------------------------------------------------
        // Handling & minigame feel
        // --------------------------------------------------------------------

        [Header("Handling & Minigame")]
        [Tooltip("Base time (seconds) before the player can cast again. (Recast Time)")]
        public float recastTime = 2.0f;

        [Tooltip("Multiplier on recast time. <1 = faster.")]
        public float RecastTimeMultiplier = 1.0f;

        [Tooltip("Multiplier on FP removed per successful hit.")]
        public float fpReductionMultiplier = 1.0f;

        [Tooltip("Multiplier for green zone size. >1 = easier.")]
        public float greenZoneSizeModifier = 1.0f;

        [Tooltip("Multiplier for green zone movement speed. <1 = easier.")]
        public float greenZoneSpeedModifier = 1.0f;

        [Tooltip("Multiplier for reaction time window after the beep.")]
        public float reactionWindowModifier = 1.0f;

        [Tooltip("Additional hit windows per cast (for advanced rods).")]
        public int extraHitWindows = 0;

        // --------------------------------------------------------------------
        // Automation & trivial fish
        // --------------------------------------------------------------------

        [Header("Automation & Trivial Fish")]
        [Tooltip("Fish are trivial if fishFP <= basePower * this factor.")]
        public float autoResolveThresholdFactor = 0.3f;

        [Tooltip("How effective this rod is when used by robots/automation.")]
        public float robotEfficiencyMultiplier = 1.0f;

        // --------------------------------------------------------------------
        // Sockets / synergies
        // --------------------------------------------------------------------

        [Header("Sockets & Synergies")]
        [Tooltip("How many gem/jewel sockets this rod has.")]
        public int socketCount = 0;
        // Later: allowedGemCategories / flags etc.

        // --------------------------------------------------------------------
        // Economy / crafting
        // --------------------------------------------------------------------

        [Header("Economy & Crafting")]
        [Tooltip("Shop base price in credits.")]
        public int basePrice = 100;

        [Tooltip("Optional: link to crafting recipe asset ID.")]
        public string craftRecipeId;

        [Tooltip("True for unique heist-only experimental rods.")]
        public bool isPrototype = false;

        // --------------------------------------------------------------------
        // Visuals (mirrors BoatDefinition style)
        // --------------------------------------------------------------------

        [Header("Visuals")]
        [Tooltip("Icon used in inventory / lists.")]
        public Sprite icon;

        [Tooltip("Art used on upgrade / shop cards.")]
        public Sprite cardArt;

        [Tooltip("Optional world model or UI prefab.")]
        public GameObject worldModelPrefab;
        // public RodVfxProfile vfxProfile; // (future: aura/glow config)

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

        /// <summary>
        /// Convenience: total power at a given upgrade level.
        /// </summary>
        public float GetTotalPower(int upgradeLevel)
        {
            upgradeLevel = Mathf.Clamp(upgradeLevel, 0, maxUpgradeLevel);
            return basePower + powerPerUpgradeLevel * upgradeLevel;
        }

        /// <summary>
        /// Convenience: final recast time at runtime (seconds).
        /// </summary>
        public float GetFinalRecastTime()
        {
            return Mathf.Max(0f, recastTime * RecastTimeMultiplier);
        }
    }
}
