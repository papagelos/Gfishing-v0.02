using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Swaps the Town Hall sprite to match the current town tier definition.
    /// </summary>
    public sealed class TownHallVisualController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;

        private HexWorld3DController _controller;

        private void OnEnable()
        {
            if (!targetRenderer)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }

            BindController();
            RefreshFromController();
        }

        private void Start()
        {
            // Handles cases where save-load updates complete after OnEnable.
            RefreshFromController();
        }

        private void OnDisable()
        {
            UnbindController();
        }

        private void BindController()
        {
            if (_controller)
            {
                return;
            }

            _controller = UnityEngine.Object.FindObjectOfType<HexWorld3DController>(true);
            if (_controller)
            {
                _controller.TownHallLevelChanged += OnTownHallLevelChanged;
            }
        }

        private void UnbindController()
        {
            if (!_controller)
            {
                return;
            }

            _controller.TownHallLevelChanged -= OnTownHallLevelChanged;
            _controller = null;
        }

        private void OnTownHallLevelChanged(int level)
        {
            RefreshVisual(level);
        }

        private void RefreshFromController()
        {
            if (!_controller)
            {
                BindController();
            }

            if (!_controller)
            {
                return;
            }

            RefreshVisual(_controller.TownHallLevel);
        }

        private void RefreshVisual(int level)
        {
            if (!targetRenderer || !_controller)
            {
                return;
            }

            TownTierDefinition definition = _controller.GetTownTierDefinition(level);
            if (definition != null && definition.townHallSprite != null)
            {
                targetRenderer.sprite = definition.townHallSprite;
            }
        }
    }
}
