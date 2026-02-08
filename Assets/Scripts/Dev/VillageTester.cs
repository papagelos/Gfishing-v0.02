using GalacticFishing.Minigames.HexWorld;
using GalacticFishing.Progress;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;

public sealed class VillageTester : MonoBehaviour
{
    [Header("Hotkeys")]
    [SerializeField] private Key injectHotkey = Key.F10;
    [SerializeField] private bool enableTierHotkeys = true;

    [Header("Optional Refs")]
    [SerializeField] private HexWorld3DController controller;
    [SerializeField] private HexWorldWarehouseInventory warehouse;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (enableTierHotkeys && TryHandleTierHotkeys(keyboard))
            return;

        var injectControl = keyboard[injectHotkey];
        if (injectControl != null && injectControl.wasPressedThisFrame)
        {
            RunFullInjection();
        }
    }

    private bool TryHandleTierHotkeys(Keyboard keyboard)
    {
        bool shiftHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        if (!shiftHeld)
            return false;

        if (WasPressedThisFrame(keyboard, Key.F1)) return TrySetTownHallLevel(1);
        if (WasPressedThisFrame(keyboard, Key.F2)) return TrySetTownHallLevel(2);
        if (WasPressedThisFrame(keyboard, Key.F3)) return TrySetTownHallLevel(3);
        if (WasPressedThisFrame(keyboard, Key.F4)) return TrySetTownHallLevel(4);
        if (WasPressedThisFrame(keyboard, Key.F5)) return TrySetTownHallLevel(5);
        if (WasPressedThisFrame(keyboard, Key.F6)) return TrySetTownHallLevel(6);
        if (WasPressedThisFrame(keyboard, Key.F7)) return TrySetTownHallLevel(7);
        if (WasPressedThisFrame(keyboard, Key.F8)) return TrySetTownHallLevel(8);
        if (WasPressedThisFrame(keyboard, Key.F9)) return TrySetTownHallLevel(9);
        if (WasPressedThisFrame(keyboard, Key.F10)) return TrySetTownHallLevel(10);

        return false;
    }

    private static bool WasPressedThisFrame(Keyboard keyboard, Key key)
    {
        var control = keyboard[key];
        return control != null && control.wasPressedThisFrame;
    }

    private bool TrySetTownHallLevel(int level)
    {
        ResolveReferences();
        if (controller == null)
        {
            Debug.LogWarning("[VillageTester] Could not set Town Hall level because HexWorld3DController was not found.");
            return false;
        }

        controller.DebugSetTownHallLevel(level);
        Debug.Log($"[VillageTester] Town Hall debug level set to T{level}.");
        return true;
    }

    private void RunFullInjection()
    {
        ResolveReferences();

        InjectCurrencies();
        UnlockAllBlueprintsInCatalog();
        InjectWarehouseResources();

        Debug.Log("[VillageTester] Injection complete: +999,999 Credits/IP/QP, catalog blueprints unlocked, warehouse resource pass applied.");
    }

    private void InjectCurrencies()
    {
        var ppm = PlayerProgressManager.Instance;
        if (ppm == null)
        {
            Debug.LogWarning("[VillageTester] PlayerProgressManager.Instance is null. Currency injection skipped.");
            return;
        }

        ppm.AddCredits(999999f);
        ppm.AddIP(999999);
        ppm.AddQP(999999);
    }

    private void UnlockAllBlueprintsInCatalog()
    {
        if (controller == null)
            return;

        var catalog = controller.GetBuildingCatalog();
        if (catalog == null || catalog.Length == 0)
            return;

        for (int i = 0; i < catalog.Length; i++)
        {
            var def = catalog[i];
            if (def == null)
                continue;

            controller.UnlockBlueprint(def.buildingName);

            if (string.IsNullOrWhiteSpace(def.buildingName))
                controller.UnlockBlueprint(def.name);
        }
    }

    private void InjectWarehouseResources()
    {
        if (warehouse == null)
        {
            Debug.LogWarning("[VillageTester] HexWorldWarehouseInventory not found. Warehouse injection skipped.");
            return;
        }

        warehouse.WarehouseLevel = 7;

        for (int value = (int)HexWorldResourceId.LakeBossTrophy2; value >= (int)HexWorldResourceId.Wood; value--)
        {
            var resourceId = (HexWorldResourceId)value;
            if (resourceId == HexWorldResourceId.None)
                continue;

            warehouse.TryAdd(resourceId, 500);
        }
    }

    private void ResolveReferences()
    {
        if (controller == null)
            controller = GetComponent<HexWorld3DController>();
        if (controller == null)
            controller = UnityEngine.Object.FindObjectOfType<HexWorld3DController>(true);

        if (warehouse == null && controller != null)
            warehouse = controller.GetComponent<HexWorldWarehouseInventory>();
        if (warehouse == null)
            warehouse = UnityEngine.Object.FindObjectOfType<HexWorldWarehouseInventory>(true);
    }
}
#endif
