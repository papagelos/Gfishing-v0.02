// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldUpgradeDefinition.cs
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Defines a single upgrade option for a building.
    /// TICKET 1: UI display fields only (purchase logic in TICKET 2).
    /// </summary>
    [CreateAssetMenu(menuName = "Galactic Fishing/Village/Upgrade Definition", fileName = "Upgrade_")]
    public sealed class HexWorldUpgradeDefinition : ScriptableObject
    {
        [Header("UI Display")]
        [Tooltip("Display name shown in the context menu.")]
        public string upgradeName = "New Upgrade";

        [Tooltip("Icon shown in the context menu button.")]
        public Sprite icon;

        [Header("Costs")]
        [Tooltip("Credits required to purchase this upgrade (0 = free).")]
        public int creditCost = 0;

        [Tooltip("Resource costs required to purchase this upgrade.")]
        public List<HexWorldResourceStack> resourceCosts = new List<HexWorldResourceStack>();

        private void OnEnable()
        {
            // Safety for older assets / edge cases where Unity may deserialize null.
            if (resourceCosts == null)
                resourceCosts = new List<HexWorldResourceStack>();
        }

        // TICKET 2 will add:
        // - effects (production bonus, capacity increase, etc.)
    }
}
