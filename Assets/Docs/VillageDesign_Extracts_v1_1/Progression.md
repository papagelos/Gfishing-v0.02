# Progression & milestones

Use this file when a ticket is about **Town Hall gating**, milestones, blueprint unlocks, or tier progression.

---

## Segment 02 (progression / rewards)
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


---

## Segment 07 (milestones / cross-system loop)
# Segment 07 — dungeon/fishing/museum, milestones, blueprint rewards

Segment 07 — dungeon/fishing/museum, milestones, blueprint rewards
07.1 Design goals
Every major minigame can unlock something meaningful for the Village (blueprints, caps, tool patterns, Town Tier key items).
Village outputs feed back into minigames via crafting (weapons/armor for Dungeon, rods/baits for Fishing).
Rewards are mostly deterministic for first-time milestones (avoid exploits + reduce RNG frustration).
Blueprint/upgrade gating should create ‘always a next unlock’ pacing across Town Tier 1–10.
07.2 Reward types and definitions
The integration system uses a small set of reusable reward primitives.
07.3 Core loop of cross-minigame rewards
Do activity → hit milestone → receive RewardBundle (often includes 1 blueprint + IP + optional key item).
Town Tier upgrades require: IP threshold + one or more Milestone Key Items (from other minigames).
Museum Reputation unlocks: additional blueprint bundles and deeper Heist access (if/when Heist is implemented).
07.4 Data model additions (code-facing)
Add these data structures (ScriptableObjects or serializable JSON) to drive rewards without hardcoding.
RewardBundle { id, displayName, ipAmount, museumRepDelta, items: ItemStack[], blueprints: BlueprintId[], toolPatterns: RecipeId[] }
MilestoneDefinition { id, source: (Fishing|Dungeon|Museum|Heist), conditionId, firstTimeOnly, rewardBundleId }
TownTierRequirement { tier, ipRequired, requiredKeyItems: ItemId[], optionalAltItems: ItemId[] (if any), notes }
BlueprintDefinition { id, category, unlockedByDefault, unlockMilestoneIds[], previewText }
Implementation rule: MilestoneDefinition drives everything. UI reads locked/unlocked state from BlueprintDefinition and recipe state.
07.5 Town Tier key items (T1–T10)
Town Tier upgrades are intentionally not ‘grind-only’. Each tier requires at least one key item from another system.
Notes:
• If Heist is not implemented yet, provide alternative ‘Museum Rep + Dungeon’ paths for T5/T8/T9/T10.
• Key items are non-tradeable, non-craftable, and one-time consumed by Town Tier upgrades.
07.6 Blueprint reward cadence
Blueprints are granted in small bundles so the player never unlocks 10 things at once. Each bundle should feel like a ‘new toy’ that interacts with existing systems.
07.6.1 Blueprint bundle rules
First-time milestone bundles are deterministic (fixed blueprint list).
Repeatable sources (daily/weekly) may use small RNG tables, but never for critical progression items.
Blueprint bundles should include: 1 primary blueprint + 0–2 supporting unlocks (tool pattern / QoL).
07.6.2 Suggested blueprint bundles by Town Tier
07.7 Fishing integration
Fishing contributes via trophies, specimen submission, and rare material drops used for processor tools.
07.7.1 Milestones and rewards
07.7.2 Craft feedback loop
Village processing improves rods/baits (quality and/or efficiency).
Fishing milestones should occasionally drop ‘Tool Materials’ that do not come from the Village (breaks circular dependency).
Example tool materials: Hardened Scale Plate (saw blade), Abyssal Resin (furnace seal), Pearl Dust (polisher).
07.8 Dungeon integration
Dungeon contributes via relics, tool patterns, and key items for Town Tier gates.
07.8.1 Milestones and rewards
07.8.2 Craft feedback loop
Dungeon gear recipes require processed resources (Ingots, Planks, Leather).
Dungeon drops provide ‘special components’ not obtainable from Village (break circular dependency).
Example components: Tempering Oil, Enchanted Rivets, Runed Grip Wrap.
07.9 Museum reputation and contracts
Museum Reputation is the glue that ties systems together. It unlocks blueprints and (optionally) Heist depth.
07.9.1 Reputation sources
Specimen submissions (Fishing): primary, repeatable.
Milestones (Dungeon/Fishing): medium bursts.
Museum Contracts (Village UI): optional repeatable loop that converts crafted items into Rep + small IP.
07.9.2 Museum Contract Board (recommended)
Unlock a simple Contract Board UI in the Village (suggested at Town Tier 9):
• Each contract requests a crafted item (e.g., 10 Planks Q≥30, 5 Ingots Q≥25).
• Completing a contract grants: Rep + small IP + optional cosmetic props.
• Contracts rotate daily/weekly; the rotation can be deterministic per seed to reduce RNG complaints.
07.10 Heist integration (optional, but planned)
If/when the Museum Heist system is implemented, it becomes the premium source of late-game key items and department keys. Until then, provide alternative paths via Museum Rep + Dungeon milestones.
Heist rewards should never grant Town Tier IP directly (avoid trivializing pacing).
Heist rewards can grant: key items, blueprint bundles, and unique village carry-over items (teleporter components).
07.11 UI surfaces and player messaging
Town Hall panel: shows current Town Tier progress (IP) + required key items with checkmarks/locks.
Material Quality panel: shows per-material quality progress; ‘quality points’ are allocated there (not in Town Hall).
Context menu (click building): shows continuous live bonuses only (roads adjacency + town hall connectivity + tile upgraded bonuses + synergy checks).
Milestone popups: always say what unlocked and why (e.g., “Dungeon Floor 3 cleared → Quarry blueprint unlocked”).
07.12 Open questions (park for later segments)
Exact numeric IP thresholds for Town Tier 1–10 (belongs in Segment 02).
Exact blueprint lists per tier (belongs in Segment 02/04/05).
How many reputation levels, and whether rep is global or per world.
Whether contracts can be completed automatically from inventory, or require manual delivery.


