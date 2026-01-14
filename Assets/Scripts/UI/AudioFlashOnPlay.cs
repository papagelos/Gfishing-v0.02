using UnityEngine;

namespace GalacticFishing.UI
{
    /// <summary>
    /// Watches an AudioSource and triggers a ScreenFlash when the source
    /// starts playing (rising edge of isPlaying).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioFlashOnPlay : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private AudioSource source;
        [SerializeField] private ScreenFlash flashOverride;

        [Header("Debug")]
        [SerializeField] private bool logEvents = false;

        private ScreenFlash _flash;
        private bool _wasPlaying;

        private void Awake()
        {
            // Auto-grab source if not wired.
            if (!source)
                source = GetComponent<AudioSource>();

            // Prefer explicit override, otherwise find any ScreenFlash in scene.
            if (flashOverride != null)
            {
                _flash = flashOverride;
            }
            else
            {
                _flash = FindObjectOfType<ScreenFlash>(true); // include inactive
                if (logEvents && _flash == null)
                    Debug.LogWarning("[AudioFlashOnPlay] No ScreenFlash found in scene.", this);
            }
        }

        private void Update()
        {
            if (!source || !_flash)
                return;

            bool nowPlaying = source.isPlaying;

            // Rising edge: sound just started.
            if (nowPlaying && !_wasPlaying)
            {
                if (logEvents)
                    Debug.Log("[AudioFlashOnPlay] Detected AudioSource.Play → Flash()", this);

                _flash.Flash();
            }

            _wasPlaying = nowPlaying;
        }
    }
}
