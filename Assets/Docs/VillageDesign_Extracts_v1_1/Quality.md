# Quality / Tool model

Use this file when a ticket changes **processor quality math**, **tool slots**, **Q_in/Q_out**, or multi-input behaviour.

---

# Segment 05 — tool-quality model, recipes, outputs, quality math (no minigames)

Segment 05 — tool-quality model, recipes, outputs, quality math (no minigames)
Status: COMPLETED (Segment 05).
Last updated: 2026-02-02.
Overview
Processor buildings convert extracted materials into refined materials and crafted components. Unlike extractors, processors have no minigames: progression comes from building level unlocks, recipe unlocks, and the quality of the installed processing tool (e.g., saw, chisel, furnace core).
Key promise: good tools let you preserve or slightly improve material quality during processing; bad tools cause quality loss. Tools never degrade (no repair loop).
Design pillars
No processor minigames: the ‘thinking’ gameplay is in village layout/synergies and extractor minigames.
Tool-gated quality transfer: the processor’s installed tool quality is the main driver of output quality.
No durability/repair: tools are permanent upgrades; progression is about obtaining better tools, not maintaining them.
Avoid catch‑22 loops: the best tool for a resource should be craftable using other resource families (at least for early tiers).
Clear knobs for balancing: each processor/recipe exposes a few tunable constants (gain, loss, caps).
Core concepts
Input Quality (Q_in): weighted average of input item qualities for the recipe.
Tool Quality (Q_tool): quality of the installed tool item in the processor (e.g., Saw, Chisel, Furnace Core).
Output Material Cap (Q_cap_mat): cap from the Material Quality system for the output material family (per-material cap).
Building/Recipe Cap (Q_cap_recipe): optional cap by recipe difficulty or processor level (prevents jumping too far early).
Final Cap (Q_cap): min(Q_cap_mat, Q_cap_recipe).
Tool slots and installation
Each processor has 1 primary tool slot by default. Higher building levels may add extra tool slots (optional), but the MVP assumes one slot per processor.
A processor can run recipes even with the default starter tool installed; upgrading the tool is what improves quality transfer.
Recommended tool types (one per processor):
Sawmill → Saw (tool family: Cutting).
Stoneworks → Chisel (tool family: Cutting).
Smelter/Furnace → Furnace Core (tool family: Heat).
Blacksmith/Forge → Hammer (tool family: Forming).
Tannery → Tanning Vat (tool family: Chemistry).
Loom → Loom Frame (tool family: Weaving).
Kitchen/Preserves → Prep Set (tool family: Prep).
Quality transfer formula
Goal behavior (matches our examples):
• If Q_tool is lower than Q_in, output quality drops, but not catastrophically.
• If Q_tool is higher than Q_in, output quality rises a little (not all the way to tool quality).
Base formula (per recipe):
Let gain = GainFactor (default 0.50) and loss = LossFactor (default 0.33). These can be tuned per processor and/or per recipe.
If Q_tool ≥ Q_in:  Q_out_raw = Q_in + ceil((Q_tool − Q_in) × gain)
If Q_tool <  Q_in:  Q_out_raw = Q_in − ceil((Q_in − Q_tool) × loss)
Then Q_out = clamp(Q_out_raw, 1, Q_cap).
Worked examples
Wood Q50 → Planks, tool Saw Q20: diff=30, loss=0.33 → drop ≈ 10 → Planks Q40.
Wood Q50 → Planks, tool Saw Q60: diff=10, gain=0.50 → gain 5 → Planks Q55.
Iron Ore Q35 → Iron Ingot, Furnace Core Q35: diff=0 → Ingot Q35 (no change).
Recipe difficulty gates
Each recipe may define tool thresholds:
• ToolHardMin: required to run the recipe at all.
• ToolSoftMin: recommended; below this, apply an extra penalty to Q_out_raw (optional).
MVP suggestion: only ToolHardMin (simple). Add ToolSoftMin later if needed.
Quality vs amount separation
Processors define outputs as (AmountPerTick, OutputQuality). Village planning (tile adjacency, roads, connectivity, synergies) affects AmountPerTick only.
The tool-quality model affects OutputQuality only.
This separation prevents ‘infinite progress’ exploits via moving roads while still rewarding planning.
Processor building list (broad)
Below is the initial processor roster. Unlock tiers are suggestions; final tuning can shift.
Avoiding catch‑22 tool loops
Rule of thumb: the ‘best tool for processing a material’ should be craftable primarily from OTHER material families, at least for early and mid tiers. Later-tier tools may optionally use the same family for flavor, but should never block progression.
Bootstrap examples (early tiers)
Better Saw (for wood processing) → made from Metal + Leather + Resin (not high-tier planks).
Better Furnace Core (for smelting) → made from Stone + Clay + Metal (not high-tier ingots).
Better Chisel (for stone processing) → made from Metal + Leather (not cut-stone slabs).
Better Tanning Vat (for leather) → made from Wood + Stone + Herbs (does not require fine leather).
Cross-family laddering
This creates a healthy ladder:
1) Improve Mining/Hunting/Herbs via extractor minigames + Material Quality caps.
2) Use those families to craft better processor tools.
3) Better tools improve processed outputs (planks, ingots, cloth, etc.).
4) Processed outputs unlock better crafting for dungeon/fishing gear.
Implementation notes for coding AI
Processors are data-driven: recipes and tool slot type should live in ScriptableObjects (or JSON) so balancing is easy.
Each processor instance stores: BuildingLevel, InstalledToolItemId, InstalledToolQuality, ActiveRecipeId (if any).
Recipe definition includes: input stacks (itemId, amount), output itemId, output material family id, base amount per tick, optional ToolHardMin, optional RecipeCap, GainFactor, LossFactor.
Quality computation is pure function: (inputs, tool, caps) → Q_out.
No durability fields anywhere for tools (remove/ignore).
Suggested data structures
ProcessorRecipeDefinition:
• string recipeId
• List<InputStack> inputs
• ItemId outputItem
• MaterialId outputMaterial
• int baseOutputAmountPerTick
• int toolHardMinQ (optional)
• int recipeCapQ (optional)
• float gainFactor (default 0.50)
• float lossFactor (default 0.33)

ProcessorBuildingDefinition:
• BuildingId
• ToolSlotType (Saw/Chisel/Heat/Hammer/Vat/Loom/Prep)
• List<RecipeUnlock> unlocksByBuildingLevel
• Optional: maxRecipeTierByTownTier
Balancing knobs
GainFactor: how much tool quality above input transfers into output (higher = faster quality improvement).
LossFactor: how harshly bad tools reduce output quality (higher = more punishing).
RecipeCapQ: optional to prevent early jumps even with a very high tool.
ToolHardMinQ: simple gating to keep higher-tier recipes locked until appropriate tools are crafted.
MVP checklist
Implement tool slot per processor + install/swap tool UI (no durability).
Implement data-driven recipes + quality formula.
Implement at least: Sawmill, Stoneworks, Charcoal Kiln, Smelter, Tannery, Blacksmith (enough for crafting loop).
Expose Q_in, Q_tool, Q_out, caps in the building UI so players understand why quality changed.


