using System;
using UnityEngine;

namespace GalacticFishing.Progress
{
    [Serializable]
    public class PlayerCurrencyData
    {
        // Credits stored as a float to accommodate fractional earnings before rounding
        public float credits = 0f;
    }
}
