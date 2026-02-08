# Buildings (Extractors + Processors)

Use this file when a ticket is about **building definitions**, **building kinds**, **tool slots**, **recipes**, or **tier unlocks**.

## Where the details come from
- Extractors & minigames: `Segment_04.md` (section 04.5)
- Processors: `Segment_04.md` (section 04.6)
- Tool-quality / processing math: `Segment_05.md`

---

## 04.5 Extractors & minigames (verbatim extract)
04.5 Extractor building catalog (v1)
Below is the initial extractor set. We start with a small number of buildings but each has deep internal progression with many unlockable resource variants.
Forestry Station (Lumber Camp)
Unlock: Town Tier 1 (blueprint optional later).
Role: Produces logs and plant fibers. Later unlocks specific wood species for crafting.
Minigame: Forest Plot Manager: manage 3–6 plots. Assign a tree species per plot. Plots gain Growth from adjacent upgraded forest tiles. Unlock new species via plot goals (biodiversity, harvest targets).
Base outputs (L1): Softwood Logs, Sap, Bark Fiber (small).
Unlocks by Building Level:
L2: unlock Seed Catalog page 2 (Birch/Aspen); targeting slider Softwood ↔ Mixed.
L3: unlock Hardwood species (Oak/Maple); 'Selective Harvest' mode (higher quality, lower quantity).
L4: unlock Resin-rich species (Pine Resin); 'Resin Tap' sub-action.
L5: unlock Exotic woods via blueprints; 'Graft' mechanic for late-game variants.
Tile synergy tags (checked by bonuses): forest.upgraded, forest.managed, forest.hardwood
External items that can affect this extractor: Forestry Drone Module (Dungeon), Rare Seeds (Fishing), Museum Specimen Tags (Reputation).
Quarry Rig (Mine)
Unlock: Town Tier 1 (blueprint optional later).
Role: Produces stone and ores. Primary source of metals for crafting tools and weapons.
Minigame: Strata Drill: a vertical depth track. Spend Drill Energy to push deeper, install sensors, and pick a target stratum. Targeting biases output toward that stratum's ore.
Base outputs (L1): Stone, Gravel.
Unlocks by Building Level:
L2: Coal seam stratum; add Stabilizers slot (reduces cave-in risk).
L3: Copper Ore stratum; add Scanner slot (raises rare find chance).
L4: Iron Ore stratum; add Deep Drilling (higher energy cost, better yield).
L5: Silver/Gold + Gem pockets; Precision Drill targeting (narrower, higher quality).
Tile synergy tags (checked by bonuses): quarry.upgraded, quarry.stabilized, quarry.deepvein, quarry.crystal
External items that can affect this extractor: Ore Scanner Chip (Dungeon), Reinforced Drill Bit blueprint (Workshop), Ancient Core (Museum).
Hunter Lodge (Tracking & Ranch)
Unlock: Town Tier 2 (blueprint optional later).
Role: Produces hides, sinew, feathers, and specialty drops used in crafting (leather chain).
Minigame: Trail Board: choose a trail on a node map, set traps/baits, attempt captures. Captured animals become ranch entries that passively produce resources.
Base outputs (L1): Raw Hide, Feathers (small).
Unlocks by Building Level:
L2: small game set; add Bait Pouch slot.
L3: tough hide set; add Trap Kit slot.
L4: exotic fauna (Tier 6+ worlds); add Taming (quality bias).
L5: mythical/alien fauna via blueprints; Ranch Training (strong quality bias).
Tile synergy tags (checked by bonuses): meadow.fertile, forest.managed, wetland.reed (fauna-dependent)
External items that can affect this extractor: Bait recipes from Fishing, Trap parts from Dungeon, Museum Wildlife Permits (Reputation).
Herbalist Greenhouse (Botany)
Unlock: Town Tier 2 (blueprint optional later).
Role: Produces herbs, fibers, dyes, and alchemy ingredients.
Minigame: Planting Grid: arrange beds in adjacency patterns. Patterns unlock new plant varieties (cross-pollination). Pick a focus recipe and fill the pattern over time.
Base outputs (L1): Common Herbs, Plant Fiber.
Unlocks by Building Level:
L2: dye plants; add Fertilizer slot.
L3: medicinal herbs; add Humidity Control (wetland synergy).
L4: rare petals/spores; add Hybrid research track (pattern challenges).
L5: alien biolum plants via blueprint; Glow Harvest catalyst drops.
Tile synergy tags (checked by bonuses): meadow.fertile, wetland.herbmarsh, wetland.peat, alien.biolum
External items that can affect this extractor: Rare spores from Dungeon, Botanical samples from Fishing, Museum Botany Grants (Reputation).
Salvage Drone Bay (Scrap Field)
Unlock: Town Tier 5 (blueprint optional later).
Role: Produces scrap parts used for late-game tools, upgrades, and museum tech.
Minigame: Scanner Sweep: reveal a salvage heatmap by spending Scan Charges. Choose hotspots to salvage. Unlock new zones by completing component sets.
Base outputs (L1): Scrap Metal, Glass Shards.
Unlocks by Building Level:
L2: Electronics scrap; add Magnet Arm slot.
L3: Machine Parts; add Sorting (bias toward chosen part family).
L4: Alien Alloys (Tier 8+); add Hazard Shield slot.
L5: Relic Fragments for museum/heists; Deep Cache hunts.
Tile synergy tags (checked by bonuses): urban.deck (future), alien.crystal (future), quarry.stabilized (fallback)
External items that can affect this extractor: Dungeon relic drops, Museum heist intel, Fishing salvage events.

---

## 04.6 Processors (verbatim extract)
04.6 Processor buildings (overview only; no minigames)
Processors convert raw → refined. UI is recipe selection + input checks. Output quality rules are in Segment 05.
Sawmill: Logs → Planks / Beams
Stonecutter: Stone → Blocks / Slabs
Smelter: Ore + Fuel → Ingots
Tannery: Raw Hide → Leather / Fine Leather
Loom: Fiber → Cloth / Rope
Workshop: Parts → Tools / Components (also makes processor tools)
Alchemy Bench (later): Herbs → Coatings / Potions

---

## Tool-quality / processing model (high level pointers)
Open `Quality.md` for the key rules and formulas.

### Ticket-writing reminders (to avoid “wrong object” bugs)
- Prefer **buildingName / asset ID** (e.g., `Building_Sawmill`) over prefab GameObject names.
- Do **not** create new systems for timers/ticks; reuse the existing village tick pipeline.
- Tool requirements should be checked by **resource ID**, not by display text.
