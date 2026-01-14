# Galactic Fishing — Inventory Grid Hand-off

## What was just fixed
- UI clicks: EventSystem uses **InputSystem_Actions** and is wired to UI actions.
- Rogue input: **PlayerInput** was removed from overlay panels; only the global EventSystem handles UI.
- Inventory toggle: **I** key toggles **Inventory-background** via InventoryWindowController.

## Scene objects of interest (SampleScene)
- Canvas/SafeFrame_16x9/**Panel_Hub** (hub menu; opens via right-click)
- Canvas/SafeFrame_16x9/**Inventory-background** (fullscreen inventory frame; contains the grid)
- EventSystem (Input System UI Input Module bound to UI actions)

## Task for this chat
Populate the inventory grid with fish icons and counts.
- Use the fish **catalog/registry** available in the project. If a single catalog asset exists, prefer that.
- Each slot shows: fish sprite + compact count (999→1k→1.2m…).
- Provide a tiny public API to mutate counts at runtime (e.g., `InventoryService.Add(fishId, amount)`), so the fishing minigame can call it.

## Acceptance checklist
- Right-click → hub opens; Inventory→Fish shows the fullscreen inventory.
- Press **I** toggles the inventory overlay without killing hub clicks afterward.
- Grid fills with all known fish; zero-count items appear dimmed or hidden (configurable).
- Counts format compactly: 1k, 12.3k, 1.2m, 1.2b, etc.
- Adding counts at runtime updates the UI immediately.

## Useful paths/files included
- Scenes: `Assets/Scenes/SampleScene.unity`
- Scripts (UI, inventory, hub, input): `Assets/Scripts/**`
- Editor tools (catalog/meta builders): `Assets/Editor/**`
- Data/registries/catalogs: `Assets/Data/**`
- Prefabs/UI: `Assets/Prefabs/**`
- Input actions: `Assets/**/*.inputactions` (includes InputSystem_Actions)
- Packages + ProjectVersion for reference
- Fish sprite manifest (names + paths): `GF_FishSpriteManifest.txt` (sample images are not all included)

## Notes for dev
- Assume one global EventSystem. Do not add PlayerInput to UI panels.
- If you need fades, keep CanvasGroup but ensure alpha=0 does not block raycasts.
- If a different fish registry exists, write a tiny adapter so the grid code expects: `{ id:string, displayName:string, icon:Sprite }`.
