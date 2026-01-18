# TICKET #002 - District Bonuses & Terrain Tiers Setup Instructions

## Overview
This document provides step-by-step Unity Editor instructions for setting up the District Bonus and Terrain Tier systems for your HexWorld Village.

---

## Part 1: Configure Tile Styles with Terrain Types

### Step 1: Set Terrain Types on Existing Tile Styles
1. In the **Project** window, navigate to your HexWorldTileStyle assets (wherever you store them)
2. For each TileStyle asset:
   - Select the asset
   - In the **Inspector**, scroll down to the **Terrain Type** section
   - Set the **Terrain Type** dropdown to match what the tile represents:
     - `Forest` - for forest/wood tiles
     - `Mountain` - for mountain/stone tiles
     - `Plains` - for plains/fiber/farm tiles
     - `Water` - for water tiles
     - `None` - for generic or undefined tiles

**Example:**
- `TileStyle_Forest` → set **Terrain Type** to `Forest`
- `TileStyle_Mountain` → set **Terrain Type** to `Mountain`
- `TileStyle_Plains` → set **Terrain Type** to `Plains`

---

## Part 2: Configure Buildings with Preferred Terrain

### Step 2: Set Preferred Terrain on Building Definitions
1. In the **Project** window, navigate to your HexWorldBuildingDefinition assets
2. For each building that should benefit from district bonuses:
   - Select the building definition asset
   - In the **Inspector**, scroll to the **District Bonus** section
   - Set **Preferred Terrain Type** to the terrain this building benefits from

**Examples:**
- **Lumber Mill** → set **Preferred Terrain Type** to `Forest`
- **Quarry** → set **Preferred Terrain Type** to `Mountain`
- **Farm** → set **Preferred Terrain Type** to `Plains`
- **Fishery** → set **Preferred Terrain Type** to `Water`
- **Town Hall** → set to `None` (infrastructure doesn't get bonuses)
- **Warehouse** → set to `None` (infrastructure doesn't get bonuses)

---

## Part 3: Add Production Profiles to Buildings

### Step 3: Ensure Buildings Have Production Profiles
For buildings that produce resources:

1. Open the **building prefab** in the Project window (double-click it)
2. Select the **root GameObject** of the prefab
3. In the **Inspector**, check if there's a **HexWorldBuildingProductionProfile** component
4. If not, click **Add Component** → search for `HexWorldBuildingProductionProfile` → add it
5. Configure the production:
   - **Base Output Per Tick**: Click the **+** button to add resource outputs
   - For each output:
     - Set **Id** (e.g., `Wood`, `Stone`, `Fiber`)
     - Set **Amount** (e.g., `10` for 10 units per 60-second tick)
6. Click **Apply** at the top of the Inspector to save prefab changes

**Example for a Lumber Mill:**
- Base Output Per Tick:
  - Id: `Wood`, Amount: `10`

**Example for a Farm:**
- Base Output Per Tick:
  - Id: `Fiber`, Amount: `8`

---

## Part 4: Add Active State Component to Buildings

### Step 4: Ensure Buildings Have Active State Component
For buildings that produce resources:

1. Open the **building prefab** in the Project window
2. Select the **root GameObject** of the prefab
3. In the **Inspector**, check if there's a **HexWorldBuildingActiveState** component
4. If not, click **Add Component** → search for `HexWorldBuildingActiveState` → add it
5. This component tracks whether the building is active or dormant
6. Click **Apply** at the top of the Inspector to save prefab changes

---

## Part 5: Test the System In-Game

### Step 5: Testing District Bonuses
1. **Play** the scene with your HexWorld Village
2. **Place tiles** of a specific terrain type (e.g., Forest tiles)
3. **Place a matching building** (e.g., Lumber Mill with Preferred Terrain = Forest)
4. **Upgrade tiles to Tier 1**:
   - First, ensure your **Town Hall Level is 2+** (Inspector on HexWorld3DController)
   - Switch to **Tile Upgrade Mode** (you'll need to add a UI button for this that calls `SetPaletteModeTileUpgrade()`)
   - Click on tiles to upgrade them (costs: 10 wood, 6 stone, 4 fiber, 50 credits)
5. **Wait for a production tick** (60 seconds by default)
6. **Check the warehouse** - production should be higher for buildings surrounded by matching terrain

**District Bonus Formula:**
- Each Tier 0 tile = 0 weight
- Each Tier 1 tile = 1 weight
- Each Tier 2 tile = 2 weight
- Bonus = `sum of weights * 5%`, capped at `40%`

**Example:**
- Lumber Mill surrounded by 8 Forest tiles at Tier 1 = 8 weight = 40% bonus
- Base production: 10 wood → With 40% bonus: 14 wood per tick

---

## Part 6: Testing Placement Tranches

### Step 6: Test Credit Costs for Tile Placement
1. **Play** the scene
2. **Place 37 tiles** (these are free from your starting budget)
3. **Try to place tile #38**:
   - You should see a toast message about needing 60 credits
   - If you have 60+ credits, the tile will place and deduct 60 credits
4. **Continue placing until you reach 62 tiles**:
   - Tiles 38-61 cost 60 credits each
5. **Tiles 62-91** cost 90 credits each

---

## Part 7: Adding UI Buttons (Optional)

### Step 7A: Add a "Tile Upgrade Mode" Button
1. In your HexWorld UI Canvas, add a new **Button**
2. Name it `Button_TileUpgrade`
3. Set the button text to "Upgrade Tiles"
4. Add an **OnClick** event:
   - Drag the **HexWorld3DController** GameObject into the event slot
   - Select function: `HexWorld3DController → SetPaletteModeTileUpgrade()`

### Step 7B: Display Tile Tier Information (Optional)
You can create a UI text label that shows the tier of the currently hovered tile by:
1. Creating a script that listens for cursor position
2. Using `controller.OwnedTiles` to get the tile at that coordinate
3. Displaying `tile.TerrainTier` in the UI

---

## Part 8: Adjusting Town Hall Level

### Step 8: Change Town Hall Level for Testing
1. Select the **HexWorld3DController** GameObject in the Hierarchy
2. In the **Inspector**, find **Town Hall / Slots (Design Doc)**
3. Change **Town Hall Level** (1-10)
4. **Note**: Town Hall Level 2+ is required to upgrade tiles to Tier 1

---

## Part 9: Adding Resources to Warehouse for Testing

### Step 9: Manually Add Resources via Code (Temporary Testing)
If you need to add resources to test tile upgrades:

1. Select the **HexWorld3DController** GameObject
2. In the **Inspector**, find the **Warehouse** reference
3. In the **Console**, you can use Debug commands, or:
4. Create a simple test button that calls:
```csharp
warehouse.TryAdd(HexWorldResourceId.Wood, 100);
warehouse.TryAdd(HexWorldResourceId.Stone, 100);
warehouse.TryAdd(HexWorldResourceId.Fiber, 100);
```

Or add resources through your existing game progression systems.

---

## Summary Checklist

- [ ] All TileStyle assets have Terrain Types set
- [ ] All producer Building Definitions have Preferred Terrain Types set
- [ ] All producer building prefabs have HexWorldBuildingProductionProfile component
- [ ] All producer building prefabs have HexWorldBuildingActiveState component
- [ ] Town Hall Level is set to 2+ for testing tier upgrades
- [ ] UI button added for Tile Upgrade Mode (optional)
- [ ] Tested placing 37+ tiles to verify credit costs
- [ ] Tested tile tier upgrades
- [ ] Tested district bonuses by checking production output

---

## Code Files Modified

The following files were created or modified for this ticket:

**New Files:**
- `Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldDistrictBonusService.cs`

**Modified Files:**
- `Assets/Minigames/HexWorld3D/Scripts/HexWorld3DTile.cs` - Added TerrainTier and TerrainType
- `Assets/Minigames/HexWorld3D/Scripts/HexWorldTileStyle.cs` - Added terrainType field
- `Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldBuildingDefinition.cs` - Added preferredTerrainType
- `Assets/Minigames/HexWorld3D/Scripts/Hexworld3DController.cs` - Added TryUpgradeTileTier, tranche system, TileUpgrade mode
- `Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldProductionTicker.cs` - Integrated district bonus calculations

---

## Additional Notes

- **District radius** is set to 2 hexes (can be seen in HexWorldDistrictBonusService.cs)
- **Tier upgrade costs** are currently hardcoded in TryUpgradeTileTier (can be made configurable later)
- **Placement tranches** follow the design doc: 0-37 free, 38-61 cost 60, 62-91 cost 90
- **Town Hall gating** prevents tier upgrades until Town Hall Level 2+

---

## Future Enhancements (Not in This Ticket)

- UI overlay highlighting district tiles when building is selected
- Visual indicators showing tile tiers (glow, height, etc.)
- Preview of district bonus percentage in building placement UI
- Configurable tier upgrade costs via ScriptableObject
- More placement tranches beyond 92 tiles
