// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldSharedPagingButtonsRouter.cs
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Owns ONE shared Prev/Next pair and routes them to either Tile pager or Building pager.
    /// Designed to avoid listener conflicts (no RemoveAllListeners), and to work even when
    /// the individual pagers have their own prev/next references cleared.
    /// </summary>
    public sealed class HexWorldSharedPagingButtonsRouter : MonoBehaviour
    {
        [Header("Shared Buttons")]
        [SerializeField] private Button sharedPrevButton;
        [SerializeField] private Button sharedNextButton;
        [SerializeField] private TMP_Text sharedPageLabel;
        [SerializeField] private bool hideButtonsWhenSinglePage = false;

        [Header("Pagers")]
        [SerializeField] private HexWorldTileBarSlotsUI tilePager;
        [SerializeField] private HexWorldBuildingBarSlotsUI buildingPager;

        [Header("Tab Buttons")]
        [SerializeField] private Button tilesTabButton;
        [SerializeField] private Button buildingsTabButton;

        [Header("Tab Panels (optional)")]
        [SerializeField] private GameObject tilesPanelRoot;
        [SerializeField] private GameObject buildingsPanelRoot;

        private enum Mode { Tiles, Buildings }
        [SerializeField] private Mode startMode = Mode.Tiles;

        private Mode _mode;
        private Coroutine _refreshCo;

        private void OnEnable()
        {
            if (tilesTabButton) tilesTabButton.onClick.AddListener(SwitchToTiles);
            if (buildingsTabButton) buildingsTabButton.onClick.AddListener(SwitchToBuildings);

            if (sharedPrevButton) sharedPrevButton.onClick.AddListener(OnPrevClicked);
            if (sharedNextButton) sharedNextButton.onClick.AddListener(OnNextClicked);

            // NOTE: We intentionally do NOT refresh here.
            // Unity calls OnEnable before Start on other components.
            // Our pagers build their slot lists in Start, so refreshing here often sees "0 slots"
            // which makes CanNext/CanPrev false until you click a tab.
            _mode = startMode;
        }

        private void Start()
        {
            // Wait one frame so PaletteTabsUI/other scripts can:
            // 1) Activate the correct panel
            // 2) Let the active pager build its slots in Start
            StartCoroutine(InitAfterFirstFrame());
        }

        private IEnumerator InitAfterFirstFrame()
        {
            yield return null;
            _mode = DetermineModeFromPanels();
            RefreshNow();
        }

        private void OnDisable()
        {
            if (tilesTabButton) tilesTabButton.onClick.RemoveListener(SwitchToTiles);
            if (buildingsTabButton) buildingsTabButton.onClick.RemoveListener(SwitchToBuildings);

            if (sharedPrevButton) sharedPrevButton.onClick.RemoveListener(OnPrevClicked);
            if (sharedNextButton) sharedNextButton.onClick.RemoveListener(OnNextClicked);

            if (_refreshCo != null)
            {
                StopCoroutine(_refreshCo);
                _refreshCo = null;
            }
        }

        public void SwitchToTiles()
        {
            _mode = Mode.Tiles;
            if (tilesPanelRoot) tilesPanelRoot.SetActive(true);
            if (buildingsPanelRoot) buildingsPanelRoot.SetActive(false);

            // Delay one frame so any newly-activated objects get their Start() before we read paging state.
            RequestRefresh(delayOneFrame: true);
        }

        public void SwitchToBuildings()
        {
            _mode = Mode.Buildings;
            if (tilesPanelRoot) tilesPanelRoot.SetActive(false);
            if (buildingsPanelRoot) buildingsPanelRoot.SetActive(true);

            // Delay one frame so the building pager (often under an inactive panel at boot)
            // has time to run Start() and build its slot list.
            RequestRefresh(delayOneFrame: true);
        }

        private void OnPrevClicked()
        {
            var p = GetActivePager();
            if (p == null) return;

            // Ensure paging state is up to date before we gate on CanPrev.
            p.RefreshPaging();

            if (p.CanPrev)
                p.PrevPage();

            RefreshSharedUI();
        }

        private void OnNextClicked()
        {
            var p = GetActivePager();
            if (p == null) return;

            // Ensure paging state is up to date before we gate on CanNext.
            p.RefreshPaging();

            if (p.CanNext)
                p.NextPage();

            RefreshSharedUI();
        }

        private IHexPager GetActivePager()
        {
            if (_mode == Mode.Buildings)
                return buildingPager;
            return tilePager;
        }

        private Mode DetermineModeFromPanels()
        {
            // Choose mode based on which panel is active (if provided), else fallback.
            if (buildingsPanelRoot && buildingsPanelRoot.activeInHierarchy && !(tilesPanelRoot && tilesPanelRoot.activeInHierarchy))
                return Mode.Buildings;
            if (tilesPanelRoot && tilesPanelRoot.activeInHierarchy && !(buildingsPanelRoot && buildingsPanelRoot.activeInHierarchy))
                return Mode.Tiles;
            return startMode;
        }

        private void RequestRefresh(bool delayOneFrame)
        {
            if (_refreshCo != null) StopCoroutine(_refreshCo);
            _refreshCo = StartCoroutine(RefreshRoutine(delayOneFrame));
        }

        private IEnumerator RefreshRoutine(bool delayOneFrame)
        {
            if (delayOneFrame)
                yield return null;
            RefreshNow();
        }

        private void RefreshNow()
        {
            GetActivePager()?.RefreshPaging();
            RefreshSharedUI();
        }

        private void RefreshSharedUI()
        {
            var p = GetActivePager();

            if (!sharedPrevButton || !sharedNextButton)
                return;

            if (p == null)
            {
                sharedPrevButton.interactable = false;
                sharedNextButton.interactable = false;
                if (sharedPageLabel)
                {
                    sharedPageLabel.text = string.Empty;
                    sharedPageLabel.gameObject.SetActive(false);
                }
                return;
            }

            sharedPrevButton.interactable = p.CanPrev;
            sharedNextButton.interactable = p.CanNext;

            bool show = p.TotalPagesCount > 1;

            if (hideButtonsWhenSinglePage)
            {
                sharedPrevButton.gameObject.SetActive(show);
                sharedNextButton.gameObject.SetActive(show);
            }

            if (sharedPageLabel)
            {
                sharedPageLabel.text = $"{p.PageIndex + 1}/{p.TotalPagesCount}";
                sharedPageLabel.gameObject.SetActive(show);
            }
        }

        /// <summary>
        /// Small interface so both pager scripts can expose paging state to the router.
        /// </summary>
        public interface IHexPager
        {
            int PageIndex { get; }
            int TotalPagesCount { get; }
            bool CanPrev { get; }
            bool CanNext { get; }

            void PrevPage();
            void NextPage();
            void RefreshPaging();
        }
    }
}
