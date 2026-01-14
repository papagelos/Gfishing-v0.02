using UnityEditor;

[InitializeOnLoad]
public static class GF_AutoBuildFishMetaIndex
{
    static GF_AutoBuildFishMetaIndex()
    {
        // Build (or rebuild) the index on each domain reload. Fast + keeps it fresh as you add fish.
        EditorApplication.delayCall += () =>
        {
            try { GF_BuildFishMetaIndex.Build(); } catch { /* ignore in case of compilation order */ }
        };
    }
}
