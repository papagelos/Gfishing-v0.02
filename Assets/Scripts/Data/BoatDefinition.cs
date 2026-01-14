// Assets/Scripts/Data/BoatDefinition.cs
using UnityEngine;

namespace GalacticFishing.Data
{
    [CreateAssetMenu(
        menuName = "GalacticFishing/Gear/Boat",
        fileName = "Boat_")]
    public class BoatDefinition : ScriptableObject
    {
        [Header("Id (stable, used in saves)")]
        public string id = "boat_default";

        [Header("Display")]
        public string displayName = "Rowboat";
        [TextArea] public string description;

        [Header("Stats")]
        [Tooltip("How many inventory slots this boat comfortably supports.")]
        public int capacitySlots = 50;

        [Tooltip("Approx max weight the boat is happy with (kg).")]
        public float maxWeightKg = 100f;

        [Tooltip("Travel speed multiplier for future world maps.")]
        public float travelSpeed = 1f;

        [Header("Visuals")]
        public Sprite icon;
        public Sprite cardArt;
    }
}
