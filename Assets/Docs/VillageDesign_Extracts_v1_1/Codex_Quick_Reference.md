# Village Design Spec — Codex Quick Reference (v1.0)

This is a compact reference for coding agents. Use the Segment files for full detail.

## How to cite in tickets
Reference files like: `Docs/VillageSpec_Extracts/Segment_06.md` and quote a short line or subsection title.


## Key numbers & rules (best-effort extract)

### Town Hall tier rewards / caps (02.4)

02.4 Baseline caps and rewards per tier
Default cap math uses hex-ring radius growth (classic 1 + 3r(r+1)) to keep the map expansion visually clean. These numbers are the starting balance values and can be tuned later without changing the structure.
Notes:
‘Active producer cap’ is the max number of producers allowed to run simultaneously (enforced by Town Hall rules).
Baseline material quality cap (per material) can be further increased by specific milestone unlocks (defined in Segment 06).
If you prefer a non-radius tile cap (e.g., ‘~457 max tiles’), keep the tier structure and replace only the Tile cap numbers; the rest of the design remains valid.

### IP rewards (02.5)

02.5 IP sources and suggested starting values
IP is earned only from actions that represent permanent progress (not from temporary placement/removal). Suggested starting categories:
Blueprint purchase (new building): +IP based on rarity tier (e.g., Common +10, Uncommon +20, Rare +35, Epic +60).
Building level upgrade: +IP based on new level (e.g., L2 +10, L3 +15, L4 +25, L5 +40, then +10 per level thereafter).
Tile upgrade purchase (turning a starter tile into an upgraded tile): +IP small but consistent (e.g., +2 to +5).
Museum reputation milestones / Heist milestones / Boss defeats: +IP large ‘milestone bursts’ (e.g., +50 to +250).
Town Tier upgrade thresholds (IP required) are intentionally rising; use these as a starting curve:
These thresholds are meant to be tuned during playtests; the important part is the shape (early fast, mid steady, late slower) and that cross-minigame milestones matter more over time.

### Town tier milestone items (07.5)

07.5 Town Tier key items (T1–T10)
Town Tier upgrades are intentionally not ‘grind-only’. Each tier requires at least one key item from another system.
Notes:
• If Heist is not implemented yet, provide alternative ‘Museum Rep + Dungeon’ paths for T5/T8/T9/T10.
• Key items are non-tradeable, non-craftable, and one-time consumed by Town Tier upgrades.

### Anti-exploit rules (08.2)

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


## Pointers
- Segment_05: Quality / processing model
- Segment_06: Roads / connectivity bonuses
- Segment_08: Economy hardening / anti-exploit
