// Assets/Minigames/HexWorld3D/Scripts/Village/HexWorldToolCatalog.cs
using System;
using System.Collections.Generic;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Static catalog for tool resources that can be slotted into Processor buildings.
    /// Provides lookup helpers for tool quality, compatibility, and display data.
    /// </summary>
    public static class HexWorldToolCatalog
    {
        private static readonly Dictionary<HexWorldResourceId, HexWorldToolDefinition> ToolDefinitions;
        private static readonly Dictionary<HexWorldResourceId, List<HexWorldToolDefinition>> ToolsBySlot;
        private static readonly HexWorldToolDefinition[] DefinitionSeed =
        {
            new HexWorldToolDefinition(HexWorldResourceId.Tool_Saw, HexWorldResourceId.Tool_Saw, 10, "Saw"),
            new HexWorldToolDefinition(HexWorldResourceId.Tool_Chisel, HexWorldResourceId.Tool_Chisel, 10, "Chisel"),
            new HexWorldToolDefinition(HexWorldResourceId.Tool_Hammer, HexWorldResourceId.Tool_Hammer, 10, "Hammer"),
            new HexWorldToolDefinition(HexWorldResourceId.Tool_FurnaceCore, HexWorldResourceId.Tool_FurnaceCore, 10, "Furnace Core"),
            new HexWorldToolDefinition(HexWorldResourceId.Tool_PrepSet, HexWorldResourceId.Tool_PrepSet, 10, "Prep Set"),
        };

        static HexWorldToolCatalog()
        {
            ToolDefinitions = new Dictionary<HexWorldResourceId, HexWorldToolDefinition>(DefinitionSeed.Length);
            ToolsBySlot = new Dictionary<HexWorldResourceId, List<HexWorldToolDefinition>>();

            for (int i = 0; i < DefinitionSeed.Length; i++)
            {
                var def = DefinitionSeed[i];
                ToolDefinitions[def.ToolId] = def;

                if (!ToolsBySlot.TryGetValue(def.SlotType, out var list))
                {
                    list = new List<HexWorldToolDefinition>();
                    ToolsBySlot[def.SlotType] = list;
                }
                list.Add(def);
            }
        }

        /// <summary>
        /// Returns true if the given resource ID corresponds to a known tool.
        /// </summary>
        public static bool TryGetTool(HexWorldResourceId toolId, out HexWorldToolDefinition definition)
        {
            return ToolDefinitions.TryGetValue(toolId, out definition);
        }

        /// <summary>
        /// Gets all tool definitions compatible with the specified slot type.
        /// </summary>
        public static IReadOnlyList<HexWorldToolDefinition> GetToolsForSlot(HexWorldResourceId slotType)
        {
            if (ToolsBySlot.TryGetValue(slotType, out var list))
                return list;

            return Array.Empty<HexWorldToolDefinition>();
        }

        /// <summary>
        /// Returns the base quality value for a tool resource. Defaults to 0 if unknown.
        /// </summary>
        public static int GetToolQuality(HexWorldResourceId toolId)
        {
            return ToolDefinitions.TryGetValue(toolId, out var def) ? def.Quality : 0;
        }

        /// <summary>
        /// Returns a display name for the tool, falling back to the enum name.
        /// </summary>
        public static string GetDisplayName(HexWorldResourceId toolId)
        {
            return ToolDefinitions.TryGetValue(toolId, out var def)
                ? def.DisplayName
                : FormatToolNameFallback(toolId);
        }

        private static string FormatToolNameFallback(HexWorldResourceId toolId)
        {
            string name = toolId.ToString();
            if (name.StartsWith("Tool_", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(5);
            return name;
        }
    }

    /// <summary>
    /// Immutable data describing a single craftable tool item.
    /// </summary>
    public readonly struct HexWorldToolDefinition
    {
        public HexWorldResourceId ToolId { get; }
        public HexWorldResourceId SlotType { get; }
        public int Quality { get; }
        public string DisplayName { get; }

        public HexWorldToolDefinition(HexWorldResourceId toolId, HexWorldResourceId slotType, int quality, string displayName)
        {
            ToolId = toolId;
            SlotType = slotType;
            Quality = Math.Max(0, quality);
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? toolId.ToString() : displayName;
        }
    }
}
