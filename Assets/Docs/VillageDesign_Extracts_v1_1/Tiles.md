# Tiles (Styles, Tags, Tiers)

Use this file when a ticket is about **tile styles**, **gameplay tags**, **tile tiers**, **upgrade costs**, or **refund math**.

## Where the details come from
- Tile families + tiers: `Segment_03.md` (03.5–03.7)
- Economy hooks (refunds etc): `Segment_08.md`

---

## 03.5 Base tile styles (verbatim extract)
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

---

## 03.6 Gameplay tiles & upgrades (verbatim extract)
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

---

## 03.7 Tier rules / weights (verbatim extract)
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

### Ticket-writing reminders
- Tiles should carry **tags** that gameplay systems use (e.g., roads, biomes). Copy the exact tag string used in code.
- Tier persistence must restore tier **after** style assignment to avoid init overwrites.
