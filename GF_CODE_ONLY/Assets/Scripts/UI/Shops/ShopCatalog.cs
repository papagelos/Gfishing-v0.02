using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Galactic Fishing/Shop/Shop Catalog", fileName = "ShopCatalog")]
public class ShopCatalog : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique id for this catalog. Used in save keys. Example: workshop, boat_shop, crafting, exploring.")]
    public string catalogId = "shop_default";

    [Tooltip("UI title for this catalog/panel.")]
    public string title = "Shop";

    [Header("Items")]
    public List<ShopCatalogItem> items = new List<ShopCatalogItem>();
}

[Serializable]
public class ShopCatalogItem
{
    [Tooltip("Unique id within this catalog. Example: rod_power, net_size, boat_hull.")]
    public string id;

    public string title;

    [TextArea]
    public string description;

    public Sprite icon;

    [Min(0)]
    public int cost = 100;

    [Min(1)]
    public int maxLevel = 10;
}
