// Assets/Minigames/HexWorld3D/Scripts/WorldProperty.cs
using System;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// A key/value property used for displaying stats in the context menu.
    /// Shared between buildings and tiles.
    /// </summary>
    [Serializable]
    public class WorldProperty
    {
        public string label;
        public string value;
    }
}
