# Segment 02 — Town Tier 1–10 Progression

Segment 02 — Town Tier 1–10 Progression
This segment defines the full Town Tier (1–10) progression: what each tier unlocks, which caps increase, how Town Tier upgrades are earned, and how Town Tier interacts with Quality Points (QP) and Material Quality caps.
02.1 Design goals
Make Town Tier the clear, long-term progression spine for the village minigame (10 meaningful steps).
Gate deeper resources and crafting breadth through Town Tier + cross-minigame milestones (Fishing/Dungeon/Museum/Heists).
Keep moment-to-moment village play about planning and production, not about hoarding one-time bonuses (roads and synergies are continuous).
Keep starter tiles forgiving: free paint + remove, no gameplay bonuses; bonuses come from upgraded tiles and building synergies.
02.2 Two progression currencies (IP vs QP)
Town progression uses two different point types so we can keep planning bonuses continuous, while Town Tier and Material Quality remain meaningful long-term decisions:
Infrastructure Points (IP): Town Tier XP. Earned from purchases, upgrades, and milestone rewards. IP fills the Town Tier bar and is consumed when you upgrade the Town Hall.
Quality Points (QP): Spendable points used in the Material Quality panel to raise per-material quality (filling progress bars). QP is granted mainly when Town Tier increases (and sometimes from milestone rewards).
Important rule: roads and other planning systems provide continuous production modifiers, but never award IP or QP while placing/removing tiles.
02.3 Town Tier upgrade flow
Earn IP from eligible sources (purchases/upgrades/milestones).
When the Town Tier bar is full, the Town Hall becomes upgrade-eligible (button enabled).
If required milestone gates are satisfied, upgrading consumes the IP bar and increases Town Tier by +1.
On upgrade: update caps (tiles/buildings/active producers), unlock new blueprints/tiles, and grant QP (quality points).
Milestone gates are intentionally mixed: some tiers are IP-only (early onboarding), later tiers require at least one achievement from Fishing, Dungeon, Museum Reputation, or Heist progression to keep the overall game loop connected.
02.4 Baseline caps and rewards per tier
Default cap math uses hex-ring radius growth (classic 1 + 3r(r+1)) to keep the map expansion visually clean. These numbers are the starting balance values and can be tuned later without changing the structure.
Notes:
‘Active producer cap’ is the max number of producers allowed to run simultaneously (enforced by Town Hall rules).
Baseline material quality cap (per material) can be further increased by specific milestone unlocks (defined in Segment 06).
If you prefer a non-radius tile cap (e.g., ‘~457 max tiles’), keep the tier structure and replace only the Tile cap numbers; the rest of the design remains valid.
02.5 IP sources and suggested starting values
IP is earned only from actions that represent permanent progress (not from temporary placement/removal). Suggested starting categories:
Blueprint purchase (new building): +IP based on rarity tier (e.g., Common +10, Uncommon +20, Rare +35, Epic +60).
Building level upgrade: +IP based on new level (e.g., L2 +10, L3 +15, L4 +25, L5 +40, then +10 per level thereafter).
Tile upgrade purchase (turning a starter tile into an upgraded tile): +IP small but consistent (e.g., +2 to +5).
Museum reputation milestones / Heist milestones / Boss defeats: +IP large ‘milestone bursts’ (e.g., +50 to +250).
Town Tier upgrade thresholds (IP required) are intentionally rising; use these as a starting curve:
These thresholds are meant to be tuned during playtests; the important part is the shape (early fast, mid steady, late slower) and that cross-minigame milestones matter more over time.
02.6 Unlock cadence and cross-minigame gates
Each Town Tier should meaningfully expand at least one of: (a) map capacity, (b) new resource family access, (c) new extractor minigame depth, (d) new processing tool tier, or (e) cross-minigame integration.
The specific blueprint lists per tier will be fully enumerated in Segment 05 (Buildings) and Segment 07 (Blueprints & unlock sources). Segment 02 only defines the cadence and gating logic.
02.7 Implementation notes for coders
Represent Town Tier as a single saved integer (1–10) plus a saved IP progress value (0…IPRequiredForNextTier).
Define a data asset list TownTierDefinition[1..10] that contains: caps, baseline material caps, QP granted, unlock tags, and milestone requirements.
When Town Tier changes: raise caps, unlock new paints/tiles/blueprints, and grant QP. Trigger a single event OnTownTierChanged(newTier).
Do not award IP/QP from reversible actions (tile place/remove, road place/remove). Award them only at purchase/upgrade/milestone completion time.
