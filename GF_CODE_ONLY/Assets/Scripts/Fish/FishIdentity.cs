// FishIdentity.csd
using UnityEngine;
using GalacticFishing; // resolves FishMeta without changing any other logic

[System.Serializable]
public class Reaction2Settings
{
    [Tooltip("Random delay BEFORE the beep (seconds, realtime).")]
    public float beepDelayMin = 0.5f;

    [Tooltip("Random delay BEFORE the beep (seconds, realtime). Max must be >= Min.")]
    public float beepDelayMax = 2.0f;

    [Tooltip("Allowed reaction window AFTER the beep (seconds, realtime).")]
    public float successWindow = 1.2f;
}

public class FishIdentity : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string displayName = "Fish";
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;

    [Header("Optional metadata (ScriptableObject)")]
    public FishMeta meta;
    [Tooltip("If true, copy Reaction2 values from meta (if present) on Awake.")]
    public bool applyMetaReactionOnAwake = true;

    // ✅ Added as requested:
    // Spawned fish can store its per-fish bullseye requirement here.
    public float bullseyeThreshold;

    [Header("Catch Difficulty (Phase 2)")]
    public Reaction2Settings reaction2 = new Reaction2Settings();

    private void Awake()
    {
        if (applyMetaReactionOnAwake && meta != null)
        {
            // Copy Reaction2 overrides if meta present
            reaction2.beepDelayMin = Mathf.Max(0f, meta.reaction2.beepDelayMin);
            reaction2.beepDelayMax = Mathf.Max(reaction2.beepDelayMin, meta.reaction2.beepDelayMax);
            reaction2.successWindow = Mathf.Max(0.05f, meta.reaction2.successWindow);
        }
    }

    private void OnValidate()
    {
        if (reaction2 == null) reaction2 = new Reaction2Settings();
        reaction2.beepDelayMin = Mathf.Max(0f, reaction2.beepDelayMin);
        reaction2.beepDelayMax = Mathf.Max(reaction2.beepDelayMin, reaction2.beepDelayMax);
        reaction2.successWindow = Mathf.Max(0.05f, reaction2.successWindow);
    }

    public void SetDisplayName(string name)
    {
        displayName = name;
        if (!string.IsNullOrWhiteSpace(name))
        {
            gameObject.name = name;
        }
    }
}
