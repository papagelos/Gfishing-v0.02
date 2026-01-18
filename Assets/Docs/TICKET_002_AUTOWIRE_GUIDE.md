# TICKET #002 - AutoWire Configuration Guide

## Overview
This guide shows you how to use the **GF_AutoWire** tool to automatically configure settings for TICKET #002 (District Bonuses & Terrain Tiers).

---

## What Can Be Auto-Configured

Unfortunately, most of TICKET #002 setup involves configuring **ScriptableObject assets** (TileStyle and BuildingDefinition), which the autowire tool cannot modify. However, we can auto-configure:

1. ✅ **Town Hall Level** on the HexWorld3DController
2. ⚠️ **UI Button for Tile Upgrade Mode** (needs manual onClick setup)

---

## How to Use GF_AutoWire

### Step 1: Open the Tool
1. In Unity, go to menu: **Tools → GF Auto Wire**
2. A window will open

### Step 2: Set Town Hall Level to 2

**Why:** Town Hall Level 2 is required for testing tile tier upgrades.

**Instructions:**
1. In the **Hierarchy**, select your **HexWorld3DController** GameObject
   - Usually located at the root level or inside a manager GameObject
2. Drag it into the **"Root (GO or Prefab)"** field in the GF_AutoWire window
3. In the **Recipe** section:
   - Check **"Use raw JSON text"**
   - Paste this JSON:

```json
{
  "ops": [
    {
      "op": "set_int",
      "path": "",
      "componentType": "GalacticFishing.Minigames.HexWorld.HexWorld3DController",
      "fieldName": "townHallLevel",
      "intValue": 2
    }
  ]
}
```

4. Click **"APPLY RECIPE"**
5. Check the Console - you should see: `GF_AutoWire: Applied JSON to Scene object. OK=1, SKIP=0, FAIL=0`

**Alternative:** You can use the JSON file instead:
1. Drag `Assets/Editor/Wirings/TICKET_002_TownHallLevel.json` into the **"Recipe JSON (TextAsset)"** field
2. Click **"APPLY RECIPE"**

---

## What Must Be Configured Manually

### 1. Tile Styles - Set Terrain Types

**Cannot be automated** because TileStyle assets are ScriptableObjects.

**Manual steps:**
1. In **Project** window, find your TileStyle assets
2. For each TileStyle:
   - Select it
   - In **Inspector**, find **Terrain Type** dropdown
   - Set to: `Forest`, `Mountain`, `Plains`, `Water`, or `None`

**Recommended mapping:**
- `TileStyle_Forest` → `Forest`
- `TileStyle_Mountain` → `Mountain`
- `TileStyle_Plains` → `Plains`
- `TileStyle_Water` → `Water`

---

### 2. Building Definitions - Set Preferred Terrain Types

**Cannot be automated** because BuildingDefinition assets are ScriptableObjects.

**Manual steps:**
1. In **Project** window, find your BuildingDefinition assets
2. For each producer building:
   - Select it
   - In **Inspector**, find **District Bonus** section
   - Set **Preferred Terrain Type**

**Recommended mapping:**
- Lumber Mill → `Forest`
- Quarry → `Mountain`
- Farm → `Plains`
- Fishery → `Water`
- Town Hall → `None`
- Warehouse → `None`

---

### 3. Building Prefabs - Add Production Components

**Cannot be automated** because components need to be added to prefabs.

**Manual steps for each producer building prefab:**

1. **Open the prefab** (double-click in Project window)
2. Select the **root GameObject** of the prefab
3. **Add Component** → search `HexWorldBuildingProductionProfile`
4. Configure production:
   - **Base Output Per Tick**: Click **+** button
   - Set **Id** (e.g., `Wood`) and **Amount** (e.g., `10`)
5. **Add Component** → search `HexWorldBuildingActiveState`
6. Click **Apply** (top-right of Inspector) to save prefab changes

**Example for Lumber Mill:**
- Add `HexWorldBuildingProductionProfile`
  - Base Output Per Tick: `Wood`, Amount: `10`
- Add `HexWorldBuildingActiveState`

---

### 4. UI Button for Tile Upgrade Mode

**Partially automated** - button creation can be automated, but onClick wiring must be manual.

#### Option A: Create Button Manually (Recommended)
1. In **Hierarchy**, right-click your HexWorld UI Canvas
2. **UI → Button - TextMeshPro**
3. Name it `Btn_TileUpgradeMode`
4. Set position/size as desired
5. Set button text to "Upgrade Tiles"
6. In **Inspector**, scroll to **Button** component
7. In **On Click ()** section:
   - Click **+** to add an event
   - Drag **HexWorld3DController** GameObject into the object field
   - Select function: `HexWorld3DController → SetPaletteModeTileUpgrade()`

#### Option B: Try Auto-Creating Button (Experimental)

**Note:** This creates the button but you must still wire the onClick event manually.

1. In **Hierarchy**, select your HexWorld UI **Canvas GameObject**
2. Open **GF_AutoWire** tool
3. Drag the Canvas into **"Root (GO or Prefab)"**
4. Use raw JSON, paste:

```json
{
  "ops": [
    {
      "op": "ensure_tmp_button",
      "parentPath": "",
      "name": "Btn_TileUpgradeMode",
      "label": "Upgrade Tiles",
      "anchorMinX": 0,
      "anchorMinY": 1,
      "anchorMaxX": 0,
      "anchorMaxY": 1,
      "pivotX": 0,
      "pivotY": 1,
      "posX": 20,
      "posY": -180,
      "sizeX": 120,
      "sizeY": 40
    }
  ]
}
```

5. Click **"APPLY RECIPE"**
6. **Still need to wire manually:**
   - Select the created button
   - In **Button** component → **On Click ()**
   - Click **+**
   - Drag **HexWorld3DController** into object field
   - Select: `HexWorld3DController → SetPaletteModeTileUpgrade()`

---

## Summary of What Autowire Can/Cannot Do

| Task | Can Autowire? | Method |
|------|---------------|---------|
| Set Town Hall Level to 2 | ✅ Yes | Use `TICKET_002_TownHallLevel.json` |
| Set Terrain Types on TileStyles | ❌ No | Manual - ScriptableObject assets |
| Set Preferred Terrain on Buildings | ❌ No | Manual - ScriptableObject assets |
| Add Production components to prefabs | ❌ No | Manual - Prefab modification |
| Create UI button | ⚠️ Partial | Can create button, but onClick must be manual |
| Wire button onClick event | ❌ No | Manual - Unity event system |

---

## Recommended Workflow

1. **Use autowire for:**
   - Setting Town Hall Level to 2

2. **Do manually:**
   - Configure all TileStyle terrain types (5 minutes)
   - Configure all BuildingDefinition preferred terrains (5 minutes)
   - Add production components to building prefabs (10 minutes)
   - Create and wire UI button (5 minutes)

**Total manual time:** ~25 minutes

---

## Troubleshooting

### "Component not found"
- Make sure you selected the correct GameObject
- The path `""` means the root (the GameObject you dragged in)
- Check that `HexWorld3DController` component exists on the GameObject

### "Field not found"
- Field names are case-sensitive
- Check spelling: `townHallLevel` (camelCase)

### "OK=0, SKIP=0, FAIL=1"
- Read the Console error message for details
- Most common: wrong GameObject selected or component missing

---

## JSON File Reference

The following JSON files are available in `Assets/Editor/Wirings/`:

1. **TICKET_002_TownHallLevel.json**
   - Sets Town Hall Level to 2
   - Apply to: HexWorld3DController GameObject

2. **TICKET_002_AutoWire_Config.json** (Experimental)
   - Attempts to create UI button
   - Apply to: Canvas GameObject
   - Note: onClick wiring still needs manual setup

---

## Alternative: Just Do It Manually

Given the limitations, it might be faster to just configure everything manually using the standard [TICKET_002_SETUP_INSTRUCTIONS.md](TICKET_002_SETUP_INSTRUCTIONS.md) guide.

The autowire tool is most useful when you have many GameObjects to wire (like wiring 20 UI buttons), but TICKET #002 mostly involves asset configuration which can't be automated.
