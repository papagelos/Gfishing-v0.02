using UnityEngine;
using GalacticFishing.Minigames.HexWorld;

namespace GalacticFishing.UI
{
    public class HexWorldMilestoneFancyToastBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexWorld3DController controller;
        [SerializeField] private RecordToastView fancyToast;

        private void OnEnable()
        {
            if (controller != null)
                controller.MilestoneReached += OnMilestoneReached;
        }

        private void OnDisable()
        {
            if (controller != null)
                controller.MilestoneReached -= OnMilestoneReached;
        }

        private void OnMilestoneReached(string header, string body)
        {
            if (fancyToast != null)
            {
                fancyToast.ShowGenericMilestone(header, body);
            }
        }
    }
}