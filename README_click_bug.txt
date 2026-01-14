Repro:
1) Open the provided scene in Assets/Scenes.
2) Enter Play.
3) Clicking any button under Canvas/Panel_Hub (and TestButton) does nothing.

Current wiring:
- EventSystem → Input System UI Input Module:
  Actions Asset = InputSystem_Actions; UI/Point, UI/Click, etc. are assigned.
- Canvas has GraphicRaycaster (Ignore Reversed Graphics = false).
- Tile 1 prefab: parent Image raycastTarget = ON; child TMP Label raycastTarget = OFF.
- Panel_Hub CanvasGroup: interactable = true, blocksRaycasts = true.
- InventoryWindow_Floating is disabled.

What to inspect next:
- Any full-screen Graphic (Image/TMP) with raycastTarget = ON above Panel_Hub.
- Any ancestor CanvasGroup with blocksRaycasts = false above Panel_Hub.
- Any negative scale or 180° Z on ancestors of Panel_Hub.
- PlayerInput: which object method ToggleInventory calls.
