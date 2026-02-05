// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldTickTimerUI.cs
using TMPro;
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Displays countdown to next production tick: "Time To Next Tick: XX sec".
    /// </summary>
    public sealed class HexWorldTickTimerUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private HexWorldProductionTicker ticker;
        [SerializeField] private float refreshHz = 4f;

        private float _nextUpdate;

        private void Awake()
        {
            if (label == null)
                label = GetComponent<TMP_Text>() ?? GetComponentInChildren<TMP_Text>(true);

            if (ticker == null)
                ticker = FindObjectOfType<HexWorldProductionTicker>(true);
        }

        private void Update()
        {
            if (ticker == null || label == null) return;

            _nextUpdate -= Time.deltaTime;
            if (_nextUpdate > 0f) return;

            _nextUpdate = refreshHz > 0f ? 1f / refreshHz : 0.25f;

            int sec = Mathf.Max(0, Mathf.CeilToInt(ticker.SecondsUntilTick));
            label.text = $"Time To Next Tick: {sec} sec";
        }
    }
}
