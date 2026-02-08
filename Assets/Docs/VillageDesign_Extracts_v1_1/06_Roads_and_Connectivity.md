# Segment 06 — continuous buffs, rules, UI presentation, calculations

Segment 06 — continuous buffs, rules, UI presentation, calculations
Status: COMPLETED (Segment 06).
Last updated: 2026-02-02.
Overview
This segment defines **continuous** (always-on) production bonuses for Village buildings and how to present them to the player. It covers:
• Roads as a planning tool (adjacency + Town Hall connectivity).
• A generic **Synergy Rule** system (tile adjacency, building proximity, road connectivity).
• A lightweight computation model (fast enough for ~457 tiles) and UI requirements so players never have to memorize hidden rules.
Key principles
1) **No one-time rewards.** Roads and synergies never grant Infrastructure Points (IP). IP is consumable and comes from purchases/upgrades/milestones only.
2) **Bonuses are continuous.** If you remove the condition, the bonus disappears immediately (future ticks).
3) **Readable over realistic.** Roads in this game are an *in-town logistics* system (one settlement). They exist to make planning meaningful, not to connect cities.
4) **Small list, always visible.** Each building should have a short “Live Bonuses” list (✅/❌) so the player can plan without a wiki.
Road system
Roads are **upgraded tiles** (not free paints). They can be placed/removed, but removal/replacement should have *some* friction (see tuning knobs).
Road bonuses (default numbers)
• **Road adjacent**: +5% Production Amount / tick (binary; on/off).
• **Connected to Town Hall**: +10% Production Amount / tick (binary; on/off).
Definitions
• A building is **road-adjacent** if **any** of its 6 neighboring tiles is a Road tile.
• A building is **Town Hall connected** if:
  - The building is road-adjacent, AND
  - At least one adjacent Road tile is connected (through Road tiles) to at least one Road tile adjacent to the Town Hall.
Connectivity algorithm (simple + fast)
Given the board size (~457 tiles), a full recompute is cheap. Use a BFS/Flood Fill from the Town Hall’s adjacent road tiles whenever a road is placed/removed (or Town Hall moved, which it shouldn’t).
Recommended cached sets:
• `roadTiles`: HashSet<Coord> for all Road tiles on the board.
• `townHallRoadComponent`: HashSet<Coord> for the road tiles reachable from TownHall-adjacent road tiles.
Then for any building at coord `b`:
• `isRoadAdjacent(b)` = any neighbor in `roadTiles`
• `isConnectedToTownHall(b)` = any neighbor in `townHallRoadComponent`
Synergy rule system
Each Building Definition declares a list of **SynergyRules** (data-driven). The runtime evaluates them every production tick (or on layout changes + cached result per building).
SynergyRule fields (recommended)
• `id` (string): stable identifier (for UI + save compatibility).
• `label` (string): what the player sees (e.g., “Road adjacent”).
• `type` (enum):
  - `RoadAdjacent` (binary)
  - `RoadConnectedToTownHall` (binary)
  - `AdjacentTileTag` (per-count, optional)
  - `AdjacentBuildingType` (binary)
  - `WithinRadiusBuildingType` (binary or per-count)
  - `AdjacentNegativeTileTag` (per-count penalty)
  - `RoadConnectedToBuildingType` (binary)
• `target` (tag/type): tile tag or building type (if applicable).
• `amountPct` (float): +0.05 for +5%, -0.03 for -3%, etc.
• `stacking` (enum): `Binary`, `PerCount`, `BestOf`, `PerUnique`.
• `cap` (optional): max counts / max pct from this rule.
Important: **Affinity is not used** in this system. Older documents mentioning “Affinity” should be treated as deprecated; synergies are explicit rules with visible UI lines.
Production math (Amount / tick)
This segment only modifies **Production Amount / tick** (quantity). Quality is handled by the Quality/Tool systems elsewhere.
Per tick:
1) Start with `baseAmountPerTick` (from building level, upgrades, robot power, etc.).
2) Evaluate all SynergyRules → sum active `amountPct` values → `totalBonusPct`.
3) Clamp `totalBonusPct` to a designer-defined range (recommended: min -80%, max +200%).
4) `finalAmountPerTick = baseAmountPerTick * (1 + totalBonusPct)`.
5) Add to building storage buffer (so bonuses affect *future* ticks only).
Anti-exploit notes
Because bonuses are continuous and applied per tick, players cannot gain permanent rewards by placing/removing roads or tiles. The only remaining degenerate behavior is micro-rearranging roads before a tick or before collection.
Recommended mitigation (pick one; simplest first):
• Make Road tiles **cost something** to place and/or cost something to remove/replace (credits + a small resource).
• Only allow **moving buildings when Dormant** (Active buildings can’t be relocated).
• Add a short **relocation cooldown** where production pauses for that building after it is moved (no durability/repair; just downtime).
UI requirements (Context Menu / hover)
When the player clicks a building, the Context Menu must show:
A) **At a glance**
• Production / tick (final, after bonuses)
• Total bonus (%), shown as a single pill/badge
• Storage (current / capacity)
B) **Live Bonuses** (checklist)
Each active/inactive rule appears as one line:
• ✅ Road adjacent … +5%
• ✅ Connected to Town Hall … +10%
• ❌ Connected to Workshop … +0%
• ✅ Adjacent Managed Forest … +8%
• ❌ Adjacent Mountains … -0% (or show the penalty line in red when active)
Rules must be sorted with the most impactful/most actionable near the top.
Example synergy sets (early game defaults)
These are example rule lists for Tier 1–3; numbers are tuning knobs:
Lumber Mill (Extractor)
• Road adjacent: +5%
• Connected to Town Hall: +10%
• Adjacent tile tag: `ManagedForest` (PerCount, cap 3) → +3% each (max +9%)
• Adjacent tile tag: `SteepMountain` (PerCount, cap 2) → -2% each (max -4%)
Quarry (Extractor)
• Road adjacent: +5%
• Connected to Town Hall: +10%
• Adjacent tile tag: `ReinforcedQuarryFace` (PerCount, cap 2) → +4% each (max +8%)
• Adjacent tile tag: `WetGround` (Binary) → -5% (mud slows mining)
Workshop (Support building)
• Road adjacent: +5% (optional; if Workshop also produces something)
• Connected to Town Hall: +10% (optional)
• WithinRadiusBuildingType: `Extractor` radius 2 (PerCount, cap 3) → +2% each (represents maintenance access)
Implementation notes
• Prefer **data-driven** rules (ScriptableObject lists) so designers can add synergies without coding.
• Compute `townHallRoadComponent` on layout changes, not every frame.
• Evaluate building bonuses either:
  - at tick time (simple), OR
  - on layout change (cached per building) + tick uses cached result (fastest).
Exit criteria for Segment 06
• Roads provide adjacency + Town Hall connectivity bonuses as continuous buffs (no IP).
• A generic SynergyRule schema exists and is shown in UI as a checklist with ✅/❌ and % values.
• A clear, cheap connectivity algorithm is defined (BFS from Town Hall road component).
