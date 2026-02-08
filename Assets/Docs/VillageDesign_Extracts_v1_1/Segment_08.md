# Segment 08 — pricing levers, caps, tuning checklist

Segment 08 — pricing levers, caps, tuning checklist
08.1 Design goals
Prevent infinite progress from free placement/removal (no ‘road shuffle’ or ‘tile shuffle’ farming).
Keep planning meaningful: bonuses come from current layout + connectivity, not from one-time triggers.
Keep math cheap: >450 tiles should still be fast (update only when layout changes).
Make balance adjustable: expose a small set of numeric knobs with clear effects and safe ranges.
Keep UX readable: any bonus affecting production/quality must be explainable in a breakdown list (✅/❌).
08.2 Anti-exploit rules (must-haves)
These are hard rules. If any future feature would violate them, the feature must be redesigned or implemented as a continuous effect instead of a one-time reward.
08.2.1 Infrastructure Points (IP) are never earned from placement
IP is earned only from purchases/upgrades/milestones (e.g., buying a building blueprint, upgrading Town Hall, completing a milestone).
Placing/removing tiles or buildings never grants IP.
If a purchase grants IP, it must be idempotent (earned once per purchase transaction).
08.2.2 Roads grant only continuous buffs
Road tiles do not grant IP (neither on purchase nor placement).
Road buffs are continuous and depend on current state: (a) Road-adjacent bonus and (b) Connected-to-Town-Hall bonus.
If a road is removed, its bonus immediately disappears (no lingering effects).
No ‘first time connected’ one-time rewards for roads.
08.2.3 Upgraded tiles are not abusable by free repainting
Base starter paints are free to place/remove and grant no bonuses.
Upgraded tiles grant bonuses, but repainting/removing them has a cost (credits/resources) and/or requires a confirmation step.
Upgraded tile state belongs to the tile instance (persisted), not to a temporary inventory item.
Optional safety rule: disallow removing/downgrading an upgraded tile if it would invalidate storage capacity rules (see 08.2.5).
08.2.4 Building synergies are continuous only
Synergy bonuses are recalculated from the live map state (adjacency + connectivity + required neighbors).
No one-time ‘connected to X’ rewards. If the connection breaks, the bonus disappears.
A building’s Total Bonus is clamped (e.g., max +200%) to prevent runaway scaling.
08.2.5 Storage cannot be exploited by temporary warehouses
If removing a warehouse would drop total capacity below currently stored amount, removal is blocked.
Alternative (if you prefer flexibility): allow removal but automatically spill excess into a ‘Overflow Crate’ UI that must be handled before unpausing production. (Blocking is simpler for MVP.)
08.2.6 Processor tools are permanent (no durability)
Tools do not degrade and never require repair.
Tool upgrades are a progression gate (quality transfer) but not a maintenance sink.
Avoid catch-22 loops: at least one high-quality tool path must rely on a different resource family than the output it improves (see 08.3.3).
08.2.7 Exploit → mitigation matrix
08.3 Balancing knobs (the tuning dashboard)
These knobs should be centralized in data (ScriptableObject/config) so balancing can happen without code edits.
08.3.1 Recommended formula knobs
Production bonus composition (example, adjustable):
TotalBonusPct = clamp( roadAdj + townHallConn + sum(synergyBonuses) + tileBonus + otherBonuses, 0, totalBonusCapPct )
Optional diminishing returns (if needed later):
TotalBonusPct = cap * (1 - exp(-rawSum / cap))  (smoothly approaches cap)
08.3.2 Processor output quality (no minigame, tool-driven)
We want processors to be deterministic but still interesting via tool quality. A practical, tunable formula is a weighted blend of input quality and tool quality, plus small building level bonus.
Example:
outputQ = round( inputQ * w_in + toolQ * w_tool + levelBonus )
Then clamp to [floor, ceiling] where floor and ceiling are also tunable (per processor type).
08.3.3 Avoiding the ‘catch-22’ tool loop
Rule of thumb: the tool that improves Resource Family A should be craftable primarily from Families B and/or C.
Example: Saw (improves wood→planks) crafted from Metal + Leather (not from planks).
Example: Furnace Liner (improves smelting) crafted from Clay + Stone (not from ingots).
Example: Stone Cutter Head (improves stone blocks) crafted from Metal + Wood (not from stone blocks).
This creates natural ‘focus two families to unlock a third’ progression without repairs.
08.4 Keep the math cheap (450+ tiles)
Most recalculation can be event-driven: only recompute networks and bonuses when layout changes (tile painted, road placed/removed, building placed/removed/moved).
08.4.1 Road connectivity (Town Hall network)
Compute ConnectedToTownHall for road tiles via BFS starting at Town Hall over contiguous road tiles.
A building is ConnectedToTownHall if it is adjacent to any road tile that is ConnectedToTownHall (or if you decide buildings can sit ‘on’ roads).
Recompute BFS only when road layout changes (roadNetworkVersion++).
At ~500 tiles, BFS is cheap; no need for union-find until you see perf issues.
08.4.2 Synergy evaluation
Evaluate adjacency synergies by scanning neighbors within radius R (usually R=1).
Evaluate ‘connected to X’ synergies using cached connectivity booleans (ConnectedToTownHall + ConnectedToWorkshop, etc.).
Update a building’s bonus breakdown only when (a) it or a relevant neighbor changes, or (b) connectivity version changes.
08.5 Tuning checklist for first playable
Pick 3 extractors + 2 processors for MVP loop (e.g., Lumber Mill + Quarry + Hunter; Sawmill + Smelter).
Define Town Tier pacing target: time to reach T2, T3, T4 with normal play.
Set storage so ‘full-stop’ happens sometimes (player engages) but not constantly (annoying).
Ensure road bonuses matter (+5%/+10%) but do not dominate (they should feel like ‘good planning’, not mandatory spaghetti).
Run a ‘worst-case stacking’ test: maximize synergies + upgraded tiles + roads and confirm the bonus cap prevents breakage.
Confirm no exploit loop exists: place/remove/rotate anything should not increase permanent progress.
08.6 Debug + UX requirements
Context menu must show a breakdown list of bonuses with ✅/❌ and the exact % contribution (players shouldn’t memorize).
Provide a developer toggle to show road connectivity overlay (highlight connected roads).
Provide a developer toggle to print each building’s computed TotalBonusPct and the cap applied.
When an action is blocked (e.g., warehouse removal), show a clear reason and how to fix it.
End of Segment 08.
Appendix A — Coding Handoff Checklist
This appendix is a practical, implementation-oriented checklist for a coding AI. It does not add new design rules; it just organizes what to build first.
A1. Data & config assets
Define resource families + resource IDs (raw, processed, crafted).
Define building definitions (extractors vs processors vs services) with: tiers/levels, base output per tick, storage behavior, and unlock rules.
Define tile definitions (base paints vs upgraded tiles) with: placement cost rules, remove/replace cost rules, and any adjacency/connectivity flags.
Define synergy rules as data (e.g., BuildingA requires/benefits from BuildingB within range N, road adjacency, Town Hall connectivity).
A2. Core runtime systems
Tile map service: query neighbors, distances, and ‘is road’ + ‘is connected to Town Hall’ connectivity (continuous calculation).
Building production ticker: compute ‘effective production per tick’ = base * (1 + total_bonus%) and clamp by storage/capacity.
Road bonus evaluation (continuous): +5% if adjacent to any road tile; +10% if road-connected to Town Hall (as designed).
Synergy evaluation (continuous): return a list of active/failed synergies for UI (✅/❌ + %).
Infrastructure Points (IP) economy: earned from purchases/upgrades/milestones only; spent to advance Town Tier bar only (consumable).
A3. UI surfaces to implement early
Town Hall overview panel (HUD button): Town Tier progress, caps, unlocked tiles/buildings, and links to other minigames.
Material Quality allocation panel: spend Quality Points into per-resource progress bars; upgrades raise per-resource quality cap.
Building/tile context menu (click): show at-a-glance production + storage + list of live bonuses and synergies (✅/❌), plus minimal actions (Activate/Deactivate, etc.).
A4. Known open questions
Exact list of resource families and which minigame unlocks each (fishing vs dungeon vs museum reputation).
Final set of upgraded tile types + their costs and bonuses (starter tiles are free but give no bonuses).
Exact synergy catalog (which buildings pair with which) and whether any synergy has ranges beyond adjacency/road-connectivity.
Processor tool-quality model details (no durability): how tool Q modifies output Q, and the ‘no catch-22’ bootstrap path.
