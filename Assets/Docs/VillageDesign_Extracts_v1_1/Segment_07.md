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
