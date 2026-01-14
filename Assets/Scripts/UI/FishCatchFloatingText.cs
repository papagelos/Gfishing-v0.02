using UnityEngine;
using GalacticFishing.UI;

public class FishCatchFloatingText : MonoBehaviour
{
    [Header("Formatting")]
    [SerializeField] private Color textColor = new Color(1f, 0.9f, 0.4f, 1f); // nice yellow-ish
    [SerializeField] private string format = "{0:0.##} kg  •  {1:0.#} cm";

    /// <summary>
    /// Call this from your fish-catch code when the fish is successfully caught.
    /// </summary>
    public void ShowForCatch(float weightKg, float lengthCm)
    {
        var mgr = FloatingTextManager.Instance;
        if (mgr == null)
        {
            Debug.LogWarning("[FishCatchFloatingText] No FloatingTextManager.Instance in scene.");
            return;
        }

        string msg = string.Format(format, weightKg, lengthCm);
        mgr.SpawnAtScreenCenter(msg, textColor);
    }
}
