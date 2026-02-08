# agents.md — Codex/Claude Implementation Rules (Unity + MCP)

This file is for the **coding agent** that implements tickets inside the Unity project.

## 1) Operating Mode (Token-Saving Default)
- Your job is to **implement changes** (code/assets/wiring) and then **report** what you did.
- **Do NOT** run long compile loops, Play Mode, or automated tests unless the ticket explicitly asks.
- The **human** will compile/test/play by default.
- Prefer **static checks** (search, verify references, validate UXML names, inspect Inspector fields) over runtime.

## 2) Safety Defaults (Unless the ticket says otherwise)
**Hard default:** NO DELETES • NO RENAMES • NO MOVES • NO “cleanup refactors”.

You may only do destructive actions if the ticket:
- explicitly allows it **and**
- lists exact targets (paths / GameObject names / GUIDs / instance IDs).

If something is ambiguous or risky, **STOP and ask the human**.

## 3) Unity MCP “Danger Mode” Rules
When MCP can create/delete/edit assets and scene objects:
- Always follow the ticket steps in order.
- If the ticket includes a “READ ONLY / Inventory first” step, you MUST complete it and output results **before** writing anything.
- If you find **near matches** (similar names), STOP for approval to avoid duplicate assets.
- Never mass-edit assets “because it seems right”. Only touch what the ticket lists.

## 4) Stop Conditions (Must halt and ask)
Stop and request human approval BEFORE proceeding if:
- Multiple possible candidates exist for wiring (more than one ticker/warehouse/controller instance, multiple UIDocuments, etc.).
- A required UXML name/field/property is missing and the ticket doesn’t authorize changing UXML.
- A change could break saves/serialization and the ticket doesn’t mention compatibility or migration.
- You would need to invent IDs, names, tags, or “guess” canonical identifiers.
- Any delete/rename/move is required but not explicitly authorized.

## 5) Determinism / Serialization Rules
- **Never renumber existing enum values** used in saves. Only append.
- Avoid changing public serialized field names/types unless the ticket explicitly handles migration.
- Prefer stable identifiers (e.g., `buildingName` / Asset IDs) over prefab GameObject names.
- If you must adjust save payloads, keep backward compatibility unless ticket says to bump version.

## 6) Performance / GC Rules
- No per-frame rebuilding of UI lists. Update only when inputs change (coord/building selection changes).
- Reuse VisualElements (pool rows) instead of allocating new ones in Update loops.
- Do not recompute expensive BFS/pathing every frame; trigger on layout changes only.

## 7) Wiring Rules (Critical)
- Prefer serialized references set in Inspector/scene.
- Fallback `FindObjectOfType` is allowed only if **exactly one** instance exists; otherwise:
  - `Debug.LogError` **once**, return early, and report what needs manual wiring.
- Do not introduce new `FindObjectOfType` calls in `Update()`.

## 8) Asset Authoring Rules
- Do not hand-write `.asset` YAML unless the ticket explicitly requires it.
- If creating ScriptableObjects, create them via Unity APIs/MCP with correct type, name, folder, and minimal required fields.
- If the ticket says “create-only”, you may not modify existing assets.

## 9) Required Output Format (Every ticket)
After implementing, you MUST output:

1) **Summary (2–6 bullets)** of what changed.
2) **Changed/Created files list** (exact paths).
3) **Unity wiring steps** (Hierarchy path → Component → Field → Assigned object/asset).
4) **Human verification checklist** (short DoD steps the human should run).
5) **Open risks / follow-ups** (only if needed; concise).

## 10) Minimal Manual Verification (Human-run by default)
Unless the ticket requests otherwise, your DoD should be phrased as **human checks**, e.g.:
- “Open scene X, click Y, confirm label Z updates”
- “Compile and confirm 0 errors”
- “Enter Play Mode and verify UI opens”

(Do not spend agent tokens doing these unless requested.)
