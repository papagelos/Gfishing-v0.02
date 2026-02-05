// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldBuildingActivationClickToggle.cs
using UnityEngine;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// DEPRECATED: Direct-click activation/deactivation has been removed.
    /// Building activation must now happen via the context menu (HexWorldBuildingContextMenu).
    ///
    /// This script is intentionally disabled. Remove this component from any GameObjects
    /// or delete this file entirely once migration is confirmed.
    /// </summary>
    [System.Obsolete("Direct-click activation removed. Use context menu for activation.")]
    public sealed class HexWorldBuildingActivationClickToggle : MonoBehaviour
    {
        // All functionality disabled. Activation now happens via context menu only.
        // See: HexWorldBuildingContextMenu.cs and HexWorld3DController.TryInteractWithBuildingUnderMouse()
    }
}
