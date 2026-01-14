Repro:
1) Open the main scene you’ve been testing (the one with Panel_Hub and Inventory-background).
2) Play → Right-click shows Panel_Hub.
3) Clicking any left-column button does nothing; cursor changes on some items.
4) Press 'I' toggles Inventory-background (PlayerInput → UI/ToggleInventory). Hub should hide but buttons are not clickable.

Objects of interest (scene hierarchy):
- Canvas/SafeFrame_16x9/Canvas_Overlay_Hub/Panel_Hub
  • Components: CanvasGroup, FullscreenHubController, HubPanelController
  • LeftColumn child contains tiles/labels
- Canvas/SafeFrame_16x9/Inventory-background
  • Components: CanvasGroup, FullscreenHubController, PlayerInput (Actions=Controls, Map=UI; ToggleInventory → FullscreenHubController.Toggle)
- EventSystem (InputSystemUIInputModule) using InputSystem_Actions for pointer/click

What to check:
- Any full-screen Image with Raycast Target ON above the left column.
- CanvasGroup on Panel_Hub: alpha=1, interactable=ON, blocksRaycasts=ON.
- GraphicRaycaster present on the canvas holding Panel_Hub.
- LeftColumn labels: TMP Raycast Target OFF; the tile/button Image Raycast Target ON.
- OnClick test: a temporary Button wired to Panel_Hub.FullscreenHubController.Toggle().
