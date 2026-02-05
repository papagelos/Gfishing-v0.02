using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Progress
{
    /// <summary>
    /// Tracks per-material quality progression.
    /// </summary>
    [Serializable]
    public class MaterialQualityRecord
    {
        public string materialId;
        public int currentQuality;
        public float currentProgress;  // 0..1 progress toward next quality level
    }

    [Serializable]
    public class PlayerCurrencyData
    {
        // Credits stored as a float to accommodate fractional earnings before rounding
        public float credits = 0f;

        // Infrastructure Points for HexWorld progression
        public long infrastructurePoints = 0;

        // Quality Points (earned from Quality Lab, spent on material upgrades)
        public long unspentQP = 0;

        // Per-material quality records
        public List<MaterialQualityRecord> materialQualityRecords = new List<MaterialQualityRecord>();
    }
}
