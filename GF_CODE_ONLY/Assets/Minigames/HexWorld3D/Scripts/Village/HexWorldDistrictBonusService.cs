// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldDistrictBonusService.cs
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Calculates district bonuses for buildings based on surrounding tiles.
    /// Queries a radius of 2 hexes around a building and sums terrain tier weights
    /// of tiles matching the building's terrain type.
    /// </summary>
    public static class HexWorldDistrictBonusService
    {
        private const int DISTRICT_RADIUS = 2;
        private const float BONUS_PER_WEIGHT = 0.05f; // 5% per weight point
        private const float MAX_BONUS = 0.40f; // Cap at 40%

        /// <summary>
        /// Calculates the district bonus for a building at the given coordinate.
        /// Returns a multiplier (e.g., 0.15 means +15% bonus).
        /// </summary>
        /// <param name="buildingCoord">The hex coordinate of the building</param>
        /// <param name="buildingTerrainType">The terrain type this building benefits from</param>
        /// <param name="allTiles">Dictionary of all owned tiles in the village</param>
        /// <returns>Bonus multiplier (0.0 to 0.40)</returns>
        public static float CalculateDistrictBonus(
            HexCoord buildingCoord,
            HexWorldTerrainType buildingTerrainType,
            Dictionary<HexCoord, HexWorld3DTile> allTiles)
        {
            if (buildingTerrainType == HexWorldTerrainType.None)
                return 0f;

            if (allTiles == null || allTiles.Count == 0)
                return 0f;

            // Get all tiles within radius 2
            var tilesInRadius = GetTilesInRadius(buildingCoord, DISTRICT_RADIUS, allTiles);

            // Sum weights of matching terrain tiles
            int totalWeight = 0;
            foreach (var tile in tilesInRadius)
            {
                if (tile == null) continue;
                if (tile.TerrainType != buildingTerrainType) continue;

                totalWeight += tile.GetTierWeight();
            }

            // Calculate bonus: summedWeights * 0.05, capped at 0.40
            float bonus = totalWeight * BONUS_PER_WEIGHT;
            bonus = Mathf.Min(bonus, MAX_BONUS);

            return bonus;
        }

        /// <summary>
        /// Gets all tile objects within a given radius of a center coordinate.
        /// </summary>
        public static List<HexWorld3DTile> GetTilesInRadius(
            HexCoord center,
            int radius,
            Dictionary<HexCoord, HexWorld3DTile> allTiles)
        {
            var result = new List<HexWorld3DTile>();

            if (allTiles == null)
                return result;

            // Use cube coordinates for radius search
            for (int q = -radius; q <= radius; q++)
            {
                for (int r = Mathf.Max(-radius, -q - radius); r <= Mathf.Min(radius, -q + radius); r++)
                {
                    var coord = new HexCoord(center.q + q, center.r + r);

                    // Exclude the center tile (the building itself)
                    if (coord.q == center.q && coord.r == center.r)
                        continue;

                    if (allTiles.TryGetValue(coord, out var tile) && tile != null)
                    {
                        result.Add(tile);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets all coordinates within a given radius of a center coordinate.
        /// Useful for UI highlighting.
        /// </summary>
        public static List<HexCoord> GetCoordsInRadius(HexCoord center, int radius)
        {
            var result = new List<HexCoord>();

            for (int q = -radius; q <= radius; q++)
            {
                for (int r = Mathf.Max(-radius, -q - radius); r <= Mathf.Min(radius, -q + radius); r++)
                {
                    var coord = new HexCoord(center.q + q, center.r + r);

                    // Exclude the center
                    if (coord.q == center.q && coord.r == center.r)
                        continue;

                    result.Add(coord);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns info string showing effective tiles and bonus percentage.
        /// Useful for UI preview.
        /// </summary>
        public static string GetDistrictBonusPreview(
            HexCoord buildingCoord,
            HexWorldTerrainType buildingTerrainType,
            Dictionary<HexCoord, HexWorld3DTile> allTiles)
        {
            if (buildingTerrainType == HexWorldTerrainType.None)
                return "No terrain type";

            var tilesInRadius = GetTilesInRadius(buildingCoord, DISTRICT_RADIUS, allTiles);

            int matchingTiles = 0;
            int totalWeight = 0;

            foreach (var tile in tilesInRadius)
            {
                if (tile == null) continue;
                if (tile.TerrainType != buildingTerrainType) continue;

                matchingTiles++;
                totalWeight += tile.GetTierWeight();
            }

            float bonus = CalculateDistrictBonus(buildingCoord, buildingTerrainType, allTiles);
            int bonusPercent = Mathf.RoundToInt(bonus * 100f);

            return $"Effective Tiles: {matchingTiles} (Weight: {totalWeight}) | Bonus: +{bonusPercent}%";
        }
    }
}
