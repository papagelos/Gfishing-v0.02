using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.Data
{
    [CreateAssetMenu(menuName = "Galactic Fishing/Dungeon/Gem Registry", fileName = "DungeonGemRegistry_Main")]
    public sealed class DungeonGemRegistry : ScriptableObject
    {
        [Serializable]
        public sealed class GemData
        {
            public GemId gemId = GemId.None;
            public Sprite icon;
            public DungeonSkillDefinition skillDefinition;
            [TextArea] public string description;
        }

        public List<GemData> gems = new();
    }
}
