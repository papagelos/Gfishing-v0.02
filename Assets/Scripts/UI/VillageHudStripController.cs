using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using GalacticFishing.Minigames.HexWorld;

public class VillageHudStripController : MonoBehaviour
{
    public enum TabKind { Tiles, Props, Buildings }

    [Serializable]
    public class Entry
    {
        public string displayName = "Name";
        public Texture2D icon; // card image
    }

    [Header("Optional: tab button icons")]
    public Texture2D tilesTabIcon;
    public Texture2D buildingsTabIcon;

    [Header("Data")]
    public List<Entry> tiles = new();
    public List<Entry> props = new();
    public List<Entry> buildings = new();

    [Header("Gameplay Wiring")]
    public HexWorld3DController controller;

    [Header("Paging")]
    public int itemsPerPage = 5;

    private TabKind _tab = TabKind.Tiles;
    private int _page = 0;

    private UIDocument _doc;

    private Button _tabTiles, _tabBuildings;
    private Button _btnPropsMode;
    private Button _prev, _next;
    private Label _pageLabel;
    private VisualElement _propPager;

    private Button[] _slots;

    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
        var root = _doc.rootVisualElement;

        _tabTiles = root.Q<Button>("Tab_Tiles");
        _btnPropsMode = root.Q<Button>("BtnPropsMode");
        _tabBuildings = root.Q<Button>("Tab_Buildings");

        _prev = root.Q<Button>("PrevPage");
        _next = root.Q<Button>("NextPage");
        _pageLabel = root.Q<Label>("PageLabel");
        _propPager = root.Q<VisualElement>("PropPager");

        _slots = new[]
        {
            root.Q<Button>("SlotBtn0"),
            root.Q<Button>("SlotBtn1"),
            root.Q<Button>("SlotBtn2"),
            root.Q<Button>("SlotBtn3"),
            root.Q<Button>("SlotBtn4"),
        };

        // Set tab icons (optional)
        if (tilesTabIcon != null) _tabTiles.style.backgroundImage = new StyleBackground(tilesTabIcon);
        if (buildingsTabIcon != null) _tabBuildings.style.backgroundImage = new StyleBackground(buildingsTabIcon);

        if (_tabTiles != null) _tabTiles.clicked += () => SwitchTab(TabKind.Tiles);
        if (_btnPropsMode != null) _btnPropsMode.clicked += () => SwitchTab(TabKind.Props);
        if (_tabBuildings != null) _tabBuildings.clicked += () => SwitchTab(TabKind.Buildings);
        if (_prev != null) _prev.clicked += PrevPage;
        if (_next != null) _next.clicked += NextPage;

        Refresh();
    }

    private List<Entry> CurrentList()
    {
        return _tab switch
        {
            TabKind.Tiles => tiles,
            TabKind.Props => props,
            _ => buildings
        };
    }

    private void SwitchTab(TabKind tab)
    {
        if (_tab == tab) return;
        _tab = tab;
        _page = 0;

        if (controller != null)
        {
            switch (tab)
            {
                case TabKind.Tiles:
                    controller.SetPaletteMode(HexWorld3DController.PaletteMode.Tiles);
                    break;
                case TabKind.Props:
                    controller.SetPaletteMode(HexWorld3DController.PaletteMode.Props);
                    break;
                case TabKind.Buildings:
                    controller.SetPaletteMode(HexWorld3DController.PaletteMode.Buildings);
                    break;
            }
        }

        Refresh();
    }

    private void PrevPage() { _page--; Refresh(); }
    private void NextPage() { _page++; Refresh(); }

    private void Refresh()
    {
        var list = CurrentList();
        int per = Mathf.Max(1, itemsPerPage);
        int totalPages = Mathf.Max(1, Mathf.CeilToInt(list.Count / (float)per));
        _page = Mathf.Clamp(_page, 0, totalPages - 1);

        SetActiveClass(_tabTiles, _tab == TabKind.Tiles);
        SetActiveClass(_btnPropsMode, _tab == TabKind.Props);
        SetActiveClass(_tabBuildings, _tab == TabKind.Buildings);

        if (_propPager != null)
            _propPager.style.display = _tab == TabKind.Props ? DisplayStyle.Flex : DisplayStyle.None;

        _pageLabel.text = $"{_page + 1}/{totalPages}";
        _prev.SetEnabled(_page > 0);
        _next.SetEnabled(_page < totalPages - 1);

        for (int i = 0; i < _slots.Length; i++)
        {
            int idx = _page * per + i;

            if (idx < list.Count && list[idx] != null)
            {
                var e = list[idx];

                _slots[i].style.display = DisplayStyle.Flex;
                _slots[i].text = string.IsNullOrWhiteSpace(e.displayName) ? "" : e.displayName;

                if (e.icon != null)
                    _slots[i].style.backgroundImage = new StyleBackground(e.icon);
                else
                    _slots[i].style.backgroundImage = StyleKeyword.None;
            }
            else
            {
                _slots[i].style.display = DisplayStyle.None;
            }
        }
    }

    private static void SetActiveClass(VisualElement el, bool active)
    {
        if (el == null) return;
        const string cls = "is-active";
        if (active) el.AddToClassList(cls);
        else el.RemoveFromClassList(cls);
    }
}
