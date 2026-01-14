// Bridges callers that reference Sprites.UI.Inventory.InventorySlot to the new global InventorySlot class.
using TMPro;
using UnityEngine.UI;

namespace Sprites.UI.Inventory
{
    public class InventorySlot : global::InventorySlot
    {
        public new Image Frame         => base.Frame;
        public new Image Icon          => base.IconImage;
        public new Image RarityRing    => base.RarityRingImg;
        public new Image PressedMask   => base.PressedMaskImg;
        public new Image DisabledMask  => base.DisabledMask;
        public new TMP_Text CountText  => base.CountText;
    }
}
