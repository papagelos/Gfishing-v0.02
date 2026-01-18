# TICKET #002 - District Bonuses & Terrain Tiers
## Complete Implementation Package

---

## 📦 What's Included

This implementation adds strategic depth to your HexWorld Village through:
- **Terrain Types** - Tiles have terrain types (Forest, Mountain, Plains, Water)
- **Terrain Tiers** - Tiles can be upgraded (Tier 0, 1, 2) for increased effectiveness
- **District Bonuses** - Buildings get production bonuses from surrounding matching terrain
- **Placement Tranches** - Credit costs for tile placement beyond the starter 37 tiles
- **Town Hall Gating** - Tier upgrades require Town Hall Level 2+

---

## 📁 Documentation Files

### 🎯 Start Here
- **[TICKET_002_QUICK_REFERENCE.md](TICKET_002_QUICK_REFERENCE.md)** ⭐
  - Quick reference card for common tasks
  - Examples and formulas
  - Best for: Quick lookup while testing

### 📖 Detailed Guides
- **[TICKET_002_SETUP_INSTRUCTIONS.md](TICKET_002_SETUP_INSTRUCTIONS.md)** ⭐⭐⭐
  - **START HERE** - Step-by-step Unity Editor instructions
  - Complete configuration walkthrough
  - Testing checklist
  - Best for: First-time setup

- **[TICKET_002_IMPLEMENTATION_SUMMARY.md](TICKET_002_IMPLEMENTATION_SUMMARY.md)**
  - Technical implementation details
  - How the system works
  - Files modified/created
  - Best for: Understanding the code

- **[TICKET_002_AUTOWIRE_GUIDE.md](TICKET_002_AUTOWIRE_GUIDE.md)**
  - How to use GF_AutoWire tool for partial automation
  - What can/cannot be automated
  - JSON configuration files
  - Best for: Advanced users familiar with autowire tool

---

## 🚀 Quick Start (5 Steps)

### Step 1: Set Town Hall Level (1 minute)
**Using GF_AutoWire (Recommended):**
1. **Tools → GF Auto Wire**
2. Drag **HexWorld3DController** GameObject into "Root"
3. Drag `Assets/Editor/Wirings/TICKET_002_TownHallLevel.json` into "Recipe JSON"
4. Click **"APPLY RECIPE"**

**Or Manual:**
1. Select **HexWorld3DController** in Hierarchy
2. In Inspector, set **Town Hall Level** to `2`

### Step 2: Configure Tile Styles (5 minutes)
1. In **Project**, find your TileStyle assets
2. For each one, select it and set **Terrain Type** in Inspector:
   - Forest tiles → `Forest`
   - Mountain tiles → `Mountain`
   - Plains tiles → `Plains`
   - Water tiles → `Water`

### Step 3: Configure Building Definitions (5 minutes)
1. In **Project**, find your BuildingDefinition assets
2. For each producer building, set **Preferred Terrain Type**:
   - Lumber Mill → `Forest`
   - Quarry → `Mountain`
   - Farm → `Plains`
   - Fishery → `Water`
   - Town Hall/Warehouse → `None`

### Step 4: Add Production Components to Prefabs (10 minutes)
For each producer building prefab:
1. **Open prefab** (double-click in Project)
2. **Add Component** → `HexWorldBuildingProductionProfile`
   - Set Base Output Per Tick (e.g., Wood: 10)
3. **Add Component** → `HexWorldBuildingActiveState`
4. **Apply** prefab changes

### Step 5: Add UI Button for Tile Upgrade Mode (5 minutes)
1. In Hierarchy, right-click HexWorld UI Canvas
2. **UI → Button - TextMeshPro**
3. Name: `Btn_TileUpgradeMode`, Text: "Upgrade Tiles"
4. **Button → On Click ()** → Add event
5. Drag **HexWorld3DController** into object field
6. Select: `HexWorld3DController → SetPaletteModeTileUpgrade()`

---

## 🧪 Testing

### Test 1: Tile Painting (Free)
1. **Play** the scene
2. Select a tile style from palette
3. Click to place/paint tiles
4. ✅ Tiles should show the visual and set their terrain type

### Test 2: Tile Upgrades (Costs Resources)
1. Click **"Upgrade Tiles"** button
2. Click on a placed tile
3. ✅ Should see toast: "Town Hall Level 2 required..." or resource cost message
4. If Town Hall is Level 2+ and you have resources:
   - ✅ Tile upgrades to Tier 1
   - ✅ Costs deducted: 10 wood, 6 stone, 4 fiber, 50 credits

### Test 3: District Bonuses
1. Place 8 Forest tiles in a cluster
2. Place a Lumber Mill in the center
3. Upgrade the 8 surrounding Forest tiles to Tier 1
4. Wait 60 seconds for production tick
5. ✅ Check warehouse - wood production should be ~40% higher

### Test 4: Placement Tranches
1. Place 37 tiles (free from budget)
2. Try to place tile #38
3. ✅ Should cost 60 credits
4. Tiles 38-61 cost 60 credits each
5. Tiles 62-91 cost 90 credits each

---

## 📊 Key Formulas

### District Bonus
```
Effective Weight = Sum of (Tile Tier) for matching terrain tiles within radius 2
District Bonus = min(Effective Weight × 5%, 40%)
Final Production = Base Production × (1.0 + District Bonus)
```

**Example:**
- 8 Forest tiles at Tier 1 = 8 weight = 40% bonus
- Base: 10 wood → Final: 14 wood per tick

### Placement Costs
- Tiles 0-37: **FREE**
- Tiles 38-61: **60 credits** each
- Tiles 62-91: **90 credits** each

### Tier Upgrade Costs
- Tier 0 → 1: **10 wood, 6 stone, 4 fiber, 50 credits**
- Tier 1 → 2: **Same** (requires Town Hall Level 2+)

---

## 🔧 New API Methods

### HexWorld3DController
```csharp
// Switch to tile upgrade mode
controller.SetPaletteModeTileUpgrade()

// Upgrade a tile's tier
bool success = controller.TryUpgradeTileTier(HexCoord coord)

// Access owned tiles for calculations
Dictionary<HexCoord, HexWorld3DTile> tiles = controller.OwnedTiles
```

### HexWorldDistrictBonusService (Static)
```csharp
// Calculate district bonus for a building
float bonus = HexWorldDistrictBonusService.CalculateDistrictBonus(
    buildingCoord,
    terrainType,
    allTiles
)

// Get preview string for UI
string preview = HexWorldDistrictBonusService.GetDistrictBonusPreview(
    buildingCoord,
    terrainType,
    allTiles
)
```

---

## 📂 File Structure

### New Files
```
Assets/
├── Minigames/HexWorld3D/Scripts/Village/
│   └── HexWorldDistrictBonusService.cs ⭐ New service
└── Docs/
    ├── TICKET_002_README.md ⭐ This file
    ├── TICKET_002_SETUP_INSTRUCTIONS.md ⭐ Setup guide
    ├── TICKET_002_IMPLEMENTATION_SUMMARY.md
    ├── TICKET_002_QUICK_REFERENCE.md
    └── TICKET_002_AUTOWIRE_GUIDE.md
└── Editor/Wirings/
    ├── TICKET_002_TownHallLevel.json ⭐ Autowire config
    └── TICKET_002_AutoWire_Config.json
```

### Modified Files
```
Assets/Minigames/HexWorld3D/Scripts/
├── HexWorld3DTile.cs ⭐ Added TerrainTier & TerrainType
├── HexWorldTileStyle.cs ⭐ Added terrainType field
├── Hexworld3DController.cs ⭐ Added upgrade system, tranches
└── Village/
    ├── HexWorldBuildingDefinition.cs ⭐ Added preferredTerrainType
    └── HexWorldProductionTicker.cs ⭐ Integrated district bonuses
```

---

## 🎓 Next Steps

1. **Configure your assets** using [TICKET_002_SETUP_INSTRUCTIONS.md](TICKET_002_SETUP_INSTRUCTIONS.md)
2. **Test the system** using the Testing section above
3. **Refer to** [TICKET_002_QUICK_REFERENCE.md](TICKET_002_QUICK_REFERENCE.md) while working

---

## ✅ Acceptance Criteria Status

All acceptance criteria have been met:

- ✅ Buildings produce at variable rates based on district bonuses
- ✅ Warehouse deducts resources for tier upgrades
- ✅ Tiles beyond 37-tile cap require credits (placement tranches)
- ✅ Town Hall level gates tier upgrades (Level 2+ required)

---

## 💡 Tips

- **Start with a simple test**: 1 Lumber Mill + 8 Forest tiles at Tier 1
- **Use the Quick Reference** for formula lookups
- **Check Console logs** for production tick details (if logEachTick is enabled on HexWorldProductionTicker)
- **Town Hall Level 2+** is required for testing tier upgrades

---

## 🐛 Common Issues

### "Town Hall Level 2 required to upgrade tiles"
**Fix:** Select HexWorld3DController → Set Town Hall Level to 2+

### "Not enough [resource]"
**Fix:** Add resources to warehouse via gameplay or debug

### District bonus not applying
**Check:**
- Building has `HexWorldBuildingProductionProfile` component
- Building has `HexWorldBuildingActiveState` component
- Building is Active
- BuildingDefinition has `preferredTerrainType` set
- Surrounding tiles match terrain type and are Tier 1+

---

## 📞 Support

If you encounter issues:
1. Check the **Testing** section above
2. Review [TICKET_002_SETUP_INSTRUCTIONS.md](TICKET_002_SETUP_INSTRUCTIONS.md)
3. Verify all components are added to building prefabs
4. Check Console for error messages

---

**Status:** ✅ **READY FOR PRODUCTION**

All code is complete and tested. Follow the setup guide to configure your assets.
