using System;
using System.Reflection;
using UnityEngine;
using GalacticFishing;

public static class FishPricing
{
    // We re-use the same binding flags pattern as in CatchToInventory
    private static readonly BindingFlags MemberFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // ------------------------------------------------------------------------
    // OLD API – still here so existing callers keep working
    // ------------------------------------------------------------------------
    // Very simple placeholder; tune later.
    public static int GetSellPrice(Fish fish, InventoryStatsService.RuntimeStats stats)
    {
        if (!fish) return 0;

        float basePrice = fish.rarity switch
        {
            FishRarity.Common        => 10f,
            FishRarity.Uncommon      => 20f,
            FishRarity.Rare          => 40f,
            FishRarity.Epic          => 80f,
            FishRarity.Legendary     => 160f,
            FishRarity.UberLegendary => 320f,
            FishRarity.OneOfAKind    => 640f,
            _                        => 10f
        };

        float wMul = stats.hasWeight ? (1f + Mathf.Clamp01(stats.weightKg * 0.05f)) : 1f;
        float lMul = stats.hasLength ? (1f + Mathf.Clamp01(stats.lengthCm * 0.01f)) : 1f;
        float qMul = stats.hasQuality ? (1f + Mathf.Clamp01(stats.quality * 0.1f))  : 1f;

        return Mathf.Max(1, Mathf.RoundToInt(basePrice * wMul * lMul * qMul));
    }

    // ------------------------------------------------------------------------
    // NEW META-BASED PRICING
    // ------------------------------------------------------------------------
    /// <summary>
    /// Calculates the coin value for a caught fish based on:
    /// - Sellvalue from FishMeta (field or property, any casing)
    /// - Caught length in cm
    /// - A global multiplier (for upgrades etc.)
    ///
    /// Rule (matching your design):
    /// value = Sellvalue * (caughtLengthCm / 100f) * globalSellMultiplier
    ///
    /// Example:
    /// Sellvalue = 100
    ///  - length = 120cm (1.2m) → 120 coins  (if multiplier = 1)
    ///  - length = 70cm  (0.7m) →  70 coins  (if multiplier = 1)
    /// </summary>
    public static float CalculateCustomPrice(
        FishMeta meta,
        Fish fishDef,           // currently unused, kept for API compatibility
        float caughtLengthCm,
        float globalSellMultiplier)
    {
        if (meta == null)
            return 0f;

        // 1) Get base Sellvalue from FishMeta via reflection
        float baseSellValue = GetSellValueFromMeta(meta);
        if (baseSellValue <= 0f)
            return 0f;

        // 2) Convert length to meters; if we got nothing, treat as 1m fish
        float lengthMeters = caughtLengthCm > 0f
            ? caughtLengthCm / 100f
            : 1f;

        // Clamp multiplier (no negative weirdness)
        float safeGlobalMultiplier = Mathf.Max(0f, globalSellMultiplier);

        float finalPrice = baseSellValue * lengthMeters * safeGlobalMultiplier;
        return finalPrice;
    }

    /// <summary>
    /// Reflection helper to read the custom 'Sellvalue' field or property
    /// from a FishMeta instance. Accepts multiple common casings:
    /// Sellvalue / SellValue / sellValue / sellvalue.
    /// </summary>
    private static float GetSellValueFromMeta(FishMeta meta)
    {
        if (meta == null) return 0f;

        Type type = meta.GetType();
        string[] candidateNames = { "Sellvalue", "SellValue", "sellValue", "sellvalue" };

        foreach (string name in candidateNames)
        {
            if (string.IsNullOrEmpty(name)) continue;

            // Field
            var field = type.GetField(name, MemberFlags);
            if (field != null)
            {
                if (TryConvertFloat(field.GetValue(meta), out float fv))
                    return fv;
            }

            // Property
            var prop = type.GetProperty(name, MemberFlags);
            if (prop != null && prop.CanRead)
            {
                if (TryConvertFloat(prop.GetValue(meta), out float pv))
                    return pv;
            }
        }

        return 0f;
    }

    private static bool TryConvertFloat(object value, out float result)
    {
        result = 0f;
        if (value == null) return false;

        try
        {
            result = Convert.ToSingle(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
