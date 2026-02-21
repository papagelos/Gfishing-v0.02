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
        [SerializeField] private HexWorld3DController controller;
        [SerializeField] private float refreshHz = 4f;

        private float _nextUpdate;
        private bool _hiddenForEditor;

        private void Awake()
        {
            if (label == null)
                label = GetComponent<TMP_Text>() ?? GetComponentInChildren<TMP_Text>(true);

            if (ticker == null)
                ticker = FindObjectOfType<HexWorldProductionTicker>(true);

            if (controller == null)
                controller = FindObjectOfType<HexWorld3DController>(true);
        }

        private void Update()
        {
            if (label == null) return;
            if (controller == null)
                controller = FindObjectOfType<HexWorld3DController>(true);

            bool hideForEditor = controller != null && controller.IsDungeonEditorMode;
            if (hideForEditor != _hiddenForEditor)
            {
                label.enabled = !hideForEditor;
                _hiddenForEditor = hideForEditor;
            }

            if (hideForEditor)
                return;

            if (!label.enabled)
                label.enabled = true;

            if (ticker == null)
                ticker = FindObjectOfType<HexWorldProductionTicker>(true);
            if (ticker == null) return;

            _nextUpdate -= Time.deltaTime;
            if (_nextUpdate > 0f) return;

            _nextUpdate = refreshHz > 0f ? 1f / refreshHz : 0.25f;

            int sec = Mathf.Max(0, Mathf.CeilToInt(ticker.SecondsUntilTick));
            label.text = $"Time To Next Tick: {sec} sec";
        }
    }
}
