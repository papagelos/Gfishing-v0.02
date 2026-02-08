# Segment 03 — list, tiers, properties, costs, remove/replace rules

Segment 03 — list, tiers, properties, costs, remove/replace rules
This segment defines the tile paint system (cosmetic vs upgraded gameplay tiles), how tiles are purchased/placed/removed, and the initial tile catalog + unlock cadence across Town Tiers 1–10.
03.1 Goals and principles
Base paints are **free** and **cosmetic-only**. They never grant production bonuses or count for synergies.
Upgraded paints ("Gameplay Tiles") grant bonuses and count for synergies, but **cost resources** to apply and **cost resources to remove/repaint**.
All tile-based bonuses must reference **gameplay tags** (not cosmetic tags) so that free starter paints cannot be exploited.
Tiles are simple, readable and low-math: bonuses are adjacency/radius checks and small BFS connectivity for roads (defined later).
03.2 Tile terminology
**Tile**: one hex cell in the village grid.
**Tile Paint / Tile Style**: the current visual paint on a tile (meadow, forest, rocky, etc.).
**Cosmetic Paint**: free, removable, no gameplay tags.
**Gameplay Paint (Upgraded Tile)**: paid paint that adds gameplay tags and can participate in bonuses/synergies.
**Constructed Tile**: an item-like tile piece you buy/place/pick up (e.g., Road tiles). These are not paints. (Road system is specified in Segment 06.)
03.3 Repaint and removal rules
We use a clear rule set to make upgraded tiles meaningful without adding repair/decay mechanics.
Cosmetic → Cosmetic: free.
Cosmetic → Gameplay: pay the gameplay paint cost.
Gameplay → Cosmetic: pay a **Demolition Fee**; tile becomes cosmetic again; the upgrade is lost.
Gameplay → Gameplay: pay Demolition Fee (to clear the old upgrade) + pay the new gameplay paint cost.
Default Demolition Fee = **30% of the paint cost** (rounded up). Each gameplay paint may override this (e.g., 20–50%) for balance.
Why this exists: players can still redesign layouts, but repeated re-painting has friction so "perfect" layouts require planning.
03.4 Data model (what the coder needs)
We represent paints as ScriptableObjects (or equivalent data) so the palette is data-driven.
TilePaintDefinition fields
id (string, stable key)
displayName (string)
category (enum): Cosmetic | Gameplay
family (enum/string): Meadow, Forest, Quarry, Wetland, Water, Volcanic, Snow, Alien, Urban, etc.
unlockTownTier (int 1–10)
paintCost (credits + list of resources)
demolitionFeeFactor (float, default 0.30)
cosmeticTags (list<string>) — visual only; never referenced by bonuses
gameplayTags (list<string>) — used by bonuses/synergies (examples below)
tileModifiers (optional, list): small always-on effects like "+2% to processors placed ON this tile"
notes (string) — designer note / art prop suggestions
Gameplay tags conventions
Keep tags few and consistent. All synergy rules should reference these tags, not specific paint ids.
meadow.upgraded, meadow.fertile, meadow.orchard
forest.upgraded, forest.managed, forest.hardwood
quarry.upgraded, quarry.stabilized, quarry.deepvein
wetland.upgraded, wetland.herbmarsh, wetland.peat
water.upgraded, water.nursery
volcanic.upgraded, volcanic.geothermal
snow.upgraded
alien.upgraded, alien.biolum, alien.crystal
03.5 Core cosmetic paints (always available)
These are the paints the player can place/remove freely from the start. They are **aesthetic only**.
Meadow / Plains (default grass)
Forest Floor (leaf litter + moss look)
Rocky Ground (stone + gravel look)
Wetland / Bog (mud + puddles look)
Shallow Water (marsh edge look)
Deep Water (open water look)
Mountain / Cliff (rock mass look)
Volcanic Crust / Lava Region (visual only at Tier 1)
03.6 Gameplay (Upgraded) paints catalog
Upgraded paints are where bonuses live. They are unlocked by Town Tier and paid with resources.
Meadow family
Fertile Meadow I (Tier 2) — tags: meadow.upgraded, meadow.fertile
Fertile Meadow II (Tier 3) — stronger version
Fertile Meadow III (Tier 5) — late-game meadow bonus tile
Orchard Grove (Tier 6) — tags: meadow.orchard (supports fruit/leather chain indirectly)
Intended uses: adjacency bonuses for Farms/Gardens, Hunters (bait plants), and any "Gatherer" extractor that makes sense.
Forest family
Managed Forest I (Tier 2) — tags: forest.upgraded, forest.managed
Managed Forest II (Tier 4)
Managed Forest III (Tier 6)
Hardwood Grove (Tier 7) — tags: forest.hardwood (enables oak/aspen style resources via extractor minigame unlocks)
Important: Lumber Mill adjacency bonuses must check forest.* gameplay tags (managed/hardwood), never the cosmetic forest paint.
Quarry / Mountain family
Stabilized Quarry I (Tier 2) — tags: quarry.upgraded, quarry.stabilized
Stabilized Quarry II (Tier 4)
Deep Vein Site (Tier 5) — tags: quarry.deepvein (enables iron/copper etc via Quarry minigame depth unlocks)
Crystal Vein Site (Tier 8) — tags: quarry.crystal (late resource family)
Quarry upgrades synergize with Quarry building depth progression and with heavy industry placement (Smelter, Stonecutter).
Wetland family
Herb Marsh (Tier 3) — tags: wetland.upgraded, wetland.herbmarsh
Reedbed (Tier 4) — tags: wetland.reed (fiber, ropes)
Peat Bog (Tier 6) — tags: wetland.peat (fuel family)
Wetlands support Alchemy/Herbalism chains and any crafting that needs fiber/fuel.
Water family
Fish Nursery (Tier 4) — tags: water.upgraded, water.nursery (village-side fish resources / bait / cooking inputs)
Irrigation Channel (Tier 6) — tags: water.irrigation (supports farms/orchards)
Note: this is separate from the main Fishing minigame; it feeds village crafting and buffs certain extractors.
Volcanic family
Geothermal Vent Tile (Tier 5) — tags: volcanic.upgraded, volcanic.geothermal (power adjacency for robots / heavy processors)
Ash-Fertile Soil (Tier 7) — tags: volcanic.ashsoil (special farm bonus)
Used mainly for power theming and to give volcano worlds a unique identity.
Snow family (late)
Frosted Ground (Tier 7) — tags: snow.upgraded (cold-world materials)
Ice Shelf (Tier 8) — tags: snow.ice (specialized fishing/processing options)
Cold tiles mainly exist on cold worlds; unlocks here gate cold-only resource families.
Alien families (endgame)
Biolum Moss (Tier 9) — tags: alien.upgraded, alien.biolum (rare catalyst/herb family)
Crystal Dust (Tier 10) — tags: alien.crystal (high-tier tech/weapon crafting)
Alien tiles are your late-game bridge into Museum reputation / heist / high-end crafting.
03.7 Town Tier unlock cadence (tiles)
This is the broad plan. Exact numbers/costs are tuned later; the unlock *shape* should stay stable so coding can start.
Town Tier 1: Cosmetic paints only (no tile bonuses).
Town Tier 2: Fertile Meadow I, Managed Forest I, Stabilized Quarry I. (First meaningful tile bonuses.)
Town Tier 3: Fertile Meadow II + Herb Marsh. (Introduce herb/fiber economy.)
Town Tier 4: Managed Forest II + Stabilized Quarry II + Fish Nursery + Reedbed.
Town Tier 5: Fertile Meadow III + Deep Vein Site + Geothermal Vent Tile.
Town Tier 6: Managed Forest III + Orchard Grove + Peat Bog + Irrigation Channel.
Town Tier 7: Hardwood Grove + Ash-Fertile Soil + Frosted Ground.
Town Tier 8: Crystal Vein Site + Ice Shelf.
Town Tier 9: Biolum Moss.
Town Tier 10: Crystal Dust (and any final world-specific upgraded paints).
03.8 UI requirements for tile painting
Palette has at least two tabs: **Cosmetic** and **Upgraded**.
Upgraded tiles show: unlock tier, cost, gameplay tags (small icons/keywords), and demolition fee warning.
Repainting an upgraded tile shows a confirmation: "You will pay demolition fee and lose the upgrade."
Context menu (tile click) should show: Cosmetic vs Upgraded, gameplay tags, and any tile modifiers relevant to nearby buildings (computed elsewhere).
