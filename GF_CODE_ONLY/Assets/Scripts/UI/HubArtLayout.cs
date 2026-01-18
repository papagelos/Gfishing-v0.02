using UnityEngine;

namespace GalacticFishing.UI
{
    [ExecuteAlways]
    [AddComponentMenu("Galactic Fishing/UI/Hub Art Layout")]
    public sealed class HubArtLayout : MonoBehaviour
    {
        public RectTransform leftColumn;
        public RectTransform gridArea;

        public Rect leftRect = new Rect(0.05f, 0.18f, 0.28f, 0.64f);
        public Rect gridRect = new Rect(0.36f, 0.18f, 0.60f, 0.70f);

        void OnEnable()   => Apply();
        void OnValidate() => Apply();

        void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) Apply();
#endif
        }

        void Apply()
        {
            if (leftColumn) Set(leftColumn, leftRect);
            if (gridArea) Set(gridArea, gridRect);
        }

        static void Set(RectTransform t, Rect r)
        {
            var min = new Vector2(r.xMin, r.yMin);
            var max = new Vector2(r.xMax, r.yMax);
            t.anchorMin = min;
            t.anchorMax = max;
            t.offsetMin = t.offsetMax = Vector2.zero;
        }
    }
}
