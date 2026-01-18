# TICKET #002 - Quick Reference Card

## 🎯 What This Ticket Does
Adds **strategic depth** to HexWorld Village by making tile placement and terrain matter for building production.

---

## 🔑 Key Concepts

### Terrain Types
- **Forest** - Wood production
- **Mountain** - Stone production
- **Plains** - Fiber/farm production
- **Water** - Fishing production

### Terrain Tiers
- **Tier 0** (default) - Weight: 0 - No bonus
- **Tier 1** - Weight: 1 - Costs: 10 wood, 6 stone, 4 fiber, 50 credits
- **Tier 2** - Weight: 2 - Same cost (requires Town Hall Level 2+)

### District Bonus Formula
```
District Bonus = min(SumOfWeights × 5%, 40%)
Final Production = Base × (1.0 + Bonus)
```

### Placement Tranches
- **Tiles 0-37**: FREE
- **Tiles 38-61**: 60 credits each
- **Tiles 62-91**: 90 credits each

---

## 🛠️ Unity Editor Quick Setup

### 1️⃣ Tile Styles
Select TileStyle asset → Set **Terrain Type** dropdown

### 2️⃣ Buildings
Select BuildingDefinition → Set **Preferred Terrain Type**

### 3️⃣ Building Prefabs (Producers Only)
Add components:
- `HexWorldBuildingProductionProfile` (set base outputs)
- `HexWorldBuildingActiveState`

### 4️⃣ Town Hall Level
Select HexWorld3DController → Set **Town Hall Level** to 2+ for testing

---

## 🎮 In-Game Usage

### Paint Terrain (Free)
1. Select tile style from palette
2. Click to place/paint tiles
3. This sets the **visual** and **type** only

### Upgrade Tiles (Costs Resources)
1. Call `controller.SetPaletteModeTileUpgrade()` (add a UI button)
2. Click tiles to upgrade them
3. Costs: 10 wood, 6 stone, 4 fiber, 50 credits per tier

### Place Buildings
1. Select building from palette
2. Place on a tile
3. Production is boosted by surrounding matching terrain

---

## 📊 Example Scenarios

### Scenario A: Lumber Mill
- **Building**: Lumber Mill (Preferred Terrain = Forest)
- **Base Production**: 10 wood/tick
- **Surrounding Tiles**: 8 Forest tiles at Tier 1
- **Calculation**: 8 × 1 = 8 weight → 8 × 5% = 40% bonus
- **Result**: 10 × 1.40 = **14 wood/tick**

### Scenario B: Quarry
- **Building**: Quarry (Preferred Terrain = Mountain)
- **Base Production**: 8 stone/tick
- **Surrounding Tiles**: 4 Mountain tiles at Tier 1, 2 at Tier 2
- **Calculation**: (4 × 1) + (2 × 2) = 8 weight → 40% bonus
- **Result**: 8 × 1.40 = **11 stone/tick**

### Scenario C: Farm (No Upgrades)
- **Building**: Farm (Preferred Terrain = Plains)
- **Base Production**: 10 fiber/tick
- **Surrounding Tiles**: 6 Plains tiles at Tier 0
- **Calculation**: 6 × 0 = 0 weight → 0% bonus
- **Result**: 10 × 1.00 = **10 fiber/tick** (no bonus)

---

## 🧪 Testing Checklist

**Before You Start:**
- [ ] Town Hall Level set to 2+
- [ ] Warehouse has resources (for upgrades)
- [ ] Credits available (for tile expansion)

**Test District Bonuses:**
- [ ] Place 8 Forest tiles around a Lumber Mill
- [ ] Upgrade those tiles to Tier 1
- [ ] Wait 60 seconds for production tick
- [ ] Check warehouse - should see 40% more wood

**Test Placement Tranches:**
- [ ] Place 37 tiles (free)
- [ ] Place tile #38 (should cost 60 credits)
- [ ] Place tile #62 (should cost 90 credits)

---

## 📞 API Reference

### Controller Methods
```csharp
controller.SetPaletteModeTileUpgrade() // Switch to upgrade mode
controller.TryUpgradeTileTier(coord)   // Upgrade a tile (returns bool)
controller.OwnedTiles                  // Access tile dictionary
controller.ResolveBuildingByName(name) // Get building definition
```

### District Bonus Service
```csharp
HexWorldDistrictBonusService.CalculateDistrictBonus(coord, terrainType, tiles)
HexWorldDistrictBonusService.GetTilesInRadius(center, radius, tiles)
HexWorldDistrictBonusService.GetCoordsInRadius(center, radius)
HexWorldDistrictBonusService.GetDistrictBonusPreview(coord, type, tiles)
```

---

## 🐛 Common Issues

### "Town Hall Level 2 required to upgrade tiles"
**Solution:** Select HexWorld3DController in Hierarchy → Set Town Hall Level to 2+

### "Not enough [resource]"
**Solution:** Add resources to warehouse via gameplay or debug commands

### "Need X credits to place more tiles"
**Solution:** Earn credits through gameplay or set higher starting credits in Inspector

### District bonus not applying
**Check:**
- Building has `HexWorldBuildingProductionProfile` component
- Building has `HexWorldBuildingActiveState` component (and is Active)
- BuildingDefinition has `preferredTerrainType` set
- Surrounding tiles match that terrain type
- Tiles are upgraded to Tier 1 or 2

---

## 📁 Key Files

- **Service**: `HexWorldDistrictBonusService.cs`
- **Controller**: `Hexworld3DController.cs`
- **Ticker**: `HexWorldProductionTicker.cs`
- **Tile**: `HexWorld3DTile.cs`
- **Setup Guide**: `TICKET_002_SETUP_INSTRUCTIONS.md`
- **Summary**: `TICKET_002_IMPLEMENTATION_SUMMARY.md`

---

## ✅ Status

**All code complete.** Ready for Unity Editor configuration.
