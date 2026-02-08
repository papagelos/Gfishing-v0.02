# Segment 01 — Systems Overview and Data Model

Segment 01 — Systems Overview and Data Model
01.1 Design pillars
Clarity over hidden math: players should understand why a bonus happens and how to improve it.
Starter tiles are free to place/remove and provide no gameplay bonuses (aesthetic only).
Gameplay bonuses come from: buildings + upgraded tiles + roads/connectivity + synergies.
Progression is long-term and layered: Town Tier (1–10) gates caps and unlocks; other minigames provide milestone items/blueprints.
Exploit resistance: avoid one-time rewards based on temporary placement when items can be freely moved.
01.2 Core currencies and what they do
Infrastructure Points (IP) — consumable progression fuel
IP is earned from purchases and upgrades only (not from placing/moving objects). IP fills the Town Tier progress bar. When the bar completes, Town Tier increases and IP is consumed.
Quality Points (QP) — allocatable material progression currency
QP is earned from milestones and/or Town Tier rank-ups (exact per-tier rewards defined in Segment 02). Players spend QP in the Material Quality panel to increase material quality progress bars, raising per-material quality.
Material Quality (MQ) — per-material quality value
Each material has its own quality value (e.g., Wood Q12). QP allocation fills that material’s progress bar (e.g., 62/100). When full, MQ increases by +1. Town Tier caps maximum MQ per material.
Continuous bonuses (not currencies)
Road adjacency/connectivity and building synergies provide continuous production multipliers (e.g., +5%, +10%). These do not grant IP and are recalculated from the current layout/state.
01.3 Deprecated concept
Affinity is deprecated.
Any earlier references to an 'Affinity' stat should be treated as legacy. All 'planning value' is expressed as either (a) consumable IP from purchases/upgrades, or (b) continuous synergy/connectivity bonuses that affect production.
01.4 Player-facing loop (high level)
Build town (place base tiles freely for aesthetics and layout).
Buy/upgrade buildings, upgraded tiles, and other town improvements -> earn IP.
IP fills Town Tier bar -> Town Tier increases -> unlocks and higher caps.
Extractor buildings produce raw resources (unique minigame per extractor).
Processor buildings refine resources (no minigame) using 'tool quality' to determine output quality uplift.
Spend QP to raise material quality (per-material) -> improves production/crafting.
Use crafted items in other minigames (fishing, dungeon, museum reputation) -> earn milestone items/blueprints -> feed back into town.
01.5 World model (what exists in code)
01.5.1 Entities
01.5.2 Definitions vs instance data
Use ScriptableObjects (or equivalent) for static definitions (tiles, buildings, upgrades, recipes). Store runtime state separately (save file) for instances (placed buildings, their levels and minigame progress, town tier/IP/QP).
Suggested definition assets
TilePaintDefinition (visual-only categories)
UpgradedTileDefinition (properties, costs, visuals)
BuildingDefinition (category: Extractor/Processor/Service; base stats; unlock tier; synergy tags; upgrades)
ExtractorMinigameDefinition (unique per extractor type)
RecipeDefinition (inputs, outputs, required processor tool tags/levels)
MilestoneDefinition (conditions + rewards: QP, blueprint, currency)
01.6 Production and quality math — principles
Detailed formulas will be specified in Segments 04–06. For now, implement these principles:
Production is computed per building tick: base_output * continuous_multipliers, then clamped by storage/caps.
Continuous multipliers derive from current layout: road adjacency, town hall connectivity, and synergy checks.
Material Quality affects either: (a) yield amount, (b) output quality, or (c) recipe unlock thresholds, depending on material family.
Processors do not use minigames. They convert input to output; output quality depends on input quality and the processor's tool quality.
No durability/repair sinks (tools are permanent once crafted).
01.7 Roads and connectivity (initial rule set)
Roads exist primarily for continuous production buffs and readability. They never grant IP directly once purchased.
IP is granted on purchase of road tiles (inventory add), not on placement. (Prevents buy-place-remove farming.)
Road placement is free to move, but the only benefit is continuous buffs while the road is currently placed.
Adjacency bonus: a building adjacent to at least 1 road tile receives +5% production quantity.
Connectivity bonus: if the building has a road-path connection to Town Hall, it receives an additional +10% production quantity.
Connectivity is evaluated on demand (when layout changes) and cached per building to avoid heavy per-frame computation.
01.8 Synergy system (initial rule set)
Synergies are optional continuous bonuses between buildings. They should be UI-discoverable (no memorization).
A synergy is a named rule: 'Producer X within N tiles of Service Y -> +Z% quantity' or 'Processor Y near Producer X -> +Z% quality'.
Synergy rules are definition-driven (data assets), not hardcoded if possible.
Synergy rules must be limited in number per building to avoid planning overload (e.g., 0–3 per building).
UI requirement: context/hover panel shows each relevant synergy line with ✅ or ❌ plus the bonus value.
01.9 Required UI surfaces (what must exist)
01.10 Open questions to resolve in Segment 02
How much QP is awarded per Town Tier rank-up (if any)?
Do any milestones grant IP directly, or only items/blueprints/QP?
Town Tier bar scaling curve: linear, exponential, or hybrid?
Per-material MQ caps per Town Tier (table).
