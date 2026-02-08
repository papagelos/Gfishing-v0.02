# Segment 04 — list, internal progression minigames, outputs, unlock paths

Segment 04 — list, internal progression minigames, outputs, unlock paths
This segment defines the building system for the village layer: building categories, shared data model, and the extractor-building catalog with unique internal progression minigames. Processors are listed here but their quality/tool rules are specified in Segment 05.
Status: complete (design draft).
04.1 Goals and principles
Extractor buildings have **unique** minigames (not the same pattern repeated). The minigame is primarily for **unlocking** new resource variants and upgrades, not for constant maintenance.
Processor buildings have **no minigames**. They convert inputs → outputs and their output quality is governed by **processor tools** + input quality (Segment 05).
Village planning matters through **continuous** bonuses (tile tags, building adjacency, roads adjacency, optional Town Hall connectivity). Nothing should grant one-time rewards that can be farmed by picking up / re-placing items.
Unlock cadence is driven by **Town Tier (1–10)** plus blueprint drops from other minigames (Fishing/Dungeon/Museum Reputation).
Keep the math implementable: adjacency/radius checks + small BFS connectivity for roads; avoid global recomputation across 450+ tiles every frame.
04.2 Building categories
**Town**: Town Hall (tiering), optional Museum Liaison / Reputation desk (later).
**Extractors**: produce raw resources; each has an internal progression minigame (Quarry, Lumber, Hunting, Herbalism, etc.).
**Processors**: convert raw → refined crafting materials (Sawmill, Smelter, Tannery, Loom, Stonecutter, etc.). No minigames.
**Support / Utility**: Warehouse, Power/Geothermal, Road Depot, Market/Shipping (later), Decorative (no gameplay).
04.3 Common data model (what the coder needs)
All buildings are data-driven definitions (ScriptableObjects or equivalent). Runtime state lives on the placed BuildingInstance.
BuildingDefinition fields
id (string, stable key)
displayName (string)
kind (enum): Town | Extractor | Processor | Support
unlockTownTier (int 1–10) and/or unlockBlueprintId (optional)
maxLevel (int) and per-level upgrade costs
footprint (tile size; assume 1 tile for MVP)
baseProductionPerTick (float) or baseCycleTimeSeconds (float)
outputs (list<ResourceStack>) for Extractors; inputs+outputs for Processors
synergyHooks (optional tags): e.g., needs forest tags, quarry tags, wetland tags
uiHints (optional): icon id, category tabs, short description
BuildingInstance runtime fields
coord (hex coordinate)
level (int)
isActive (bool)
storedOutput (per-building buffer) OR routes to Warehouse (depending on existing system)
productionMultiplier (computed from continuous bonuses)
extractorState (optional, per-extractor minigame state blob)
04.4 Extractor minigame framework
We want extractors to feel different, but they should share a thin framework so implementation stays sane.
Shared rules (all extractors)
Each extractor has a **primary output family** (Wood, Ore/Stone, Organics, Herbs/Fiber, Scrap, etc.).
Building Level unlocks **new minigame content** (new zones/species/strata) and increases base production per tick.
The minigame unlocks **resource variants** and **targeting controls** (choose what to focus on), but once unlocked the extractor can run passively (offline) using the chosen target settings.
Minigame actions should cost **time/energy/tickets** (internal to that extractor) rather than global IP. This prevents confusing overlap with Town Tier IP.
Outputs are always raw. Any 'refined' material comes from processors.
Minigame deliverables per extractor (for UI)
A clear 'progress meter' toward the next unlock (depth reached, species tamed, recipe discovered, etc.).
A 'target selector' (what you want mostly: e.g., softwood vs hardwood; copper vs iron; hides vs feathers).
A small list of active conditions with ✅/❌ to support the context menu: e.g., Road adjacent ✅, Connected to Town Hall ✅, Needs Wetland tiles ❌.
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
04.6 Processor buildings (overview only; no minigames)
Processors convert raw → refined. UI is recipe selection + input checks. Output quality rules are in Segment 05.
Sawmill: Logs → Planks / Beams
Stonecutter: Stone → Blocks / Slabs
Smelter: Ore + Fuel → Ingots
Tannery: Raw Hide → Leather / Fine Leather
Loom: Fiber → Cloth / Rope
Workshop: Parts → Tools / Components (also makes processor tools)
Alchemy Bench (later): Herbs → Coatings / Potions
04.7 Support / utility buildings (overview)
Town Hall: Tier progression uses IP (consumable). Unlocks paints/buildings/caps; increases road connectivity bonuses (see Segment 02/06).
Warehouse: increases storage so producers don’t cap out.
Road Depot: purchase/upgrade road tiles; roads grant continuous adjacency + optional Town Hall connectivity bonuses (Segment 06).
Power Node (optional): lightweight gating for high-tier buildings (no repair).
Museum Liaison (later): turns Museum Reputation into blueprint rewards / unlock tokens for village.
