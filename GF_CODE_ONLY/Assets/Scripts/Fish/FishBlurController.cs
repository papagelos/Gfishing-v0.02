using UnityEngine;
using GalacticFishing.Progress;
using GalacticFishing.UI;   // for FloatingTextManager (world popups)

namespace GalacticFishing
{
    /// <summary>
    /// Per-fish helper that knows this fish's "power" and compares it to
    /// the player's current rod power.
    ///
    /// Rule:
    ///   if (fishPower > CurrentRodPower) -> fish is "too strong"
    ///   else                             -> fish is within rod power
    ///
    /// NOTE: This **no longer** changes sprites, scale, or flip.
    ///       It only:
    ///         - stores fishPower (from meta or inspector)
    ///         - computes whether it's too strong
    ///         - can show a floating text message on click if too strong
    /// </summary>
    public class FishBlurController : MonoBehaviour
    {
        [Header("Fish strength")]
        [Tooltip("How strong this fish is compared to rods. " +
                 "If > current rod power, the fish is considered 'too strong'.")]
        public float fishPower = 10f;

        [Header("Behaviour")]
        [Tooltip("If true, we auto-evaluate on Start(). " +
                 "Turn off if you plan to call Init(...) or InitFromMeta(...) from your spawner.")]
        public bool applyOnStart = true;

        [Tooltip("Optional debug logging of power comparisons.")]
        public bool logDebug = false;

        [Header("Too-strong feedback (optional)")]
        [SerializeField] private bool showTooStrongTextOnClick = true;
        [SerializeField] private string tooStrongMessage = "Upgrade your rod!";
        [SerializeField] private Color tooStrongTextColor = new Color(1f, 0.85f, 0.2f);

        // internal state
        private bool _lastTooStrong;
        private bool _evaluatedOnce;

        private void Start()
        {
            if (applyOnStart)
                EvaluatePower();
        }

        /// <summary>
        /// Compare fishPower to PlayerProgressManager.Instance.CurrentRodPower
        /// and remember if the fish is currently too strong.
        /// </summary>
        public void EvaluatePower()
        {
            float rodPower = 0f;
            var ppm = PlayerProgressManager.Instance;
            if (ppm != null)
            {
                rodPower = ppm.CurrentRodPower;
            }
            else if (logDebug)
            {
                Debug.LogWarning("[FishBlurController] No PlayerProgressManager.Instance found; using rodPower=0.", this);
            }

            bool tooStrong = fishPower > rodPower;
            _lastTooStrong = tooStrong;
            _evaluatedOnce = true;

            if (logDebug)
            {
                Debug.Log(
                    $"[FishBlurController] fishPower={fishPower}, rodPower={rodPower}, tooStrong={tooStrong}",
                    this
                );
            }
        }

        /// <summary>
        /// Helper if your spawner knows the numeric power directly.
        /// Call immediately after spawning.
        /// </summary>
        public void Init(float power)
        {
            fishPower = power;
            EvaluatePower();
        }

        /// <summary>
        /// Helper if your spawner has a FishMeta ScriptableObject.
        /// It will try to read a field/property named 'power' or 'Power'
        /// (int or float) from that meta.
        /// </summary>
        public void InitFromMeta(ScriptableObject fishMeta)
        {
            if (fishMeta == null)
            {
                if (logDebug)
                    Debug.LogWarning("[FishBlurController] InitFromMeta called with null meta.", this);
                return;
            }

            fishPower = GetFishPowerFromMeta(fishMeta);
            EvaluatePower();
        }

        // -------- CLICK FEEDBACK --------

        private void OnMouseDown()
        {
            if (!showTooStrongTextOnClick)
                return;

            if (!_evaluatedOnce)
                EvaluatePower();

            if (!_lastTooStrong)
                return;

            var mgr = FloatingTextManager.Instance;
            if (mgr != null)
            {
                mgr.SpawnWorld(tooStrongMessage, transform.position, tooStrongTextColor);
            }
        }

        // -------- META REFLECTION --------

        /// <summary>
        /// Reflection helper so we don't care about the exact type/namespace
        /// of your FishMeta. We only require a field or property called
        /// 'power' or 'Power' of type int or float.
        /// </summary>
        private float GetFishPowerFromMeta(ScriptableObject meta)
        {
            var t = meta.GetType();

            // Try fields: power / Power
            var field = t.GetField("power") ?? t.GetField("Power");
            if (field != null)
            {
                object value = field.GetValue(meta);
                if (value is int i)   return i;
                if (value is float f) return f;
            }

            // Try properties: power / Power
            var prop = t.GetProperty("power") ?? t.GetProperty("Power");
            if (prop != null)
            {
                object value = prop.GetValue(meta);
                if (value is int i2)   return i2;
                if (value is float f2) return f2;
            }

            if (logDebug)
                Debug.LogWarning($"[FishBlurController] No 'power' field/property found on meta type {t.Name}. Using 0.", this);

            return 0f;
        }

        // Optional helper if other scripts want to query it:
        public bool IsTooStrongForCurrentRod
        {
            get
            {
                if (!_evaluatedOnce)
                    EvaluatePower();
                return _lastTooStrong;
            }
        }
    }
}
