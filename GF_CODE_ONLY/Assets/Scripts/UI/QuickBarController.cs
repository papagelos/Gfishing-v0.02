using UnityEngine;
using UnityEngine.UI;

public enum QuickBarAction
{
    None,
    InventoryGrid,
    UpgradeCards
}

[System.Serializable]
public class QuickBarSlot
{
    public Button button;
    public QuickBarAction action;
}

public class QuickBarController : MonoBehaviour
{
    [Header("Routing")]
    [SerializeField] private MenuRouter menuRouter;

    [Header("Slots")]
    [SerializeField] private QuickBarSlot[] slots;

    private void Awake()
    {
        if (menuRouter == null)
        {
            Debug.LogWarning("[QuickBarController] MenuRouter is not assigned.");
            return;
        }

        foreach (var slot in slots)
        {
            if (slot == null || slot.button == null)
                continue;

            QuickBarAction action = slot.action; // capture local copy
            slot.button.onClick.AddListener(() => ActivateSlot(action));
        }
    }

    private void ActivateSlot(QuickBarAction action)
    {
        switch (action)
        {
            case QuickBarAction.InventoryGrid:
                // Direct world -> inventory
                menuRouter.OpenInventoryFromWorld();
                break;

            case QuickBarAction.UpgradeCards:
                menuRouter.OpenUpgradeCards();
                break;

            case QuickBarAction.None:
            default:
                // Unassigned slot
                break;
        }
    }
}
