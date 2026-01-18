using System.Text;
using UnityEditor;
using UnityEngine;

public static class DumpHierarchyToClipboard
{
    [MenuItem("Tools/GF/Copy Selected Hierarchy Paths (Clipboard)")]
    public static void CopyPaths()
    {
        var root = Selection.activeTransform;
        if (!root)
        {
            EditorUtility.DisplayDialog("Copy Hierarchy Paths", "Select a GameObject/Transform in the Hierarchy first.", "OK");
            return;
        }

        var sb = new StringBuilder(64 * 1024);
        int count = 0;

        DumpPathsRecursive(root, sb, ref count);

        GUIUtility.systemCopyBuffer = sb.ToString();
        EditorUtility.DisplayDialog("Copied!", $"Copied {count} paths to clipboard.\n\nPaste it into chat.", "OK");
    }

    [MenuItem("Tools/GF/Copy Selected Hierarchy Tree (Indented)")]
    public static void CopyIndentedTree()
    {
        var root = Selection.activeTransform;
        if (!root)
        {
            EditorUtility.DisplayDialog("Copy Hierarchy Tree", "Select a GameObject/Transform in the Hierarchy first.", "OK");
            return;
        }

        var sb = new StringBuilder(64 * 1024);
        int count = 0;

        DumpIndentedRecursive(root, sb, 0, ref count);

        GUIUtility.systemCopyBuffer = sb.ToString();
        EditorUtility.DisplayDialog("Copied!", $"Copied indented tree ({count} nodes) to clipboard.\n\nPaste it into chat.", "OK");
    }

    private static void DumpPathsRecursive(Transform t, StringBuilder sb, ref int count)
    {
        // This is the exact relative path string your GF_AutoWire expects (root-relative)
        // Example output when selecting Prefab_HexWorld3D_Village:
        // Prefab_HexWorld3D_Village/UI_Root/Canvas/TileBar/...
        sb.AppendLine(GetFullPathFromSelectedRoot(t));
        count++;

        for (int i = 0; i < t.childCount; i++)
            DumpPathsRecursive(t.GetChild(i), sb, ref count);
    }

    private static void DumpIndentedRecursive(Transform t, StringBuilder sb, int depth, ref int count)
    {
        sb.Append(' ', depth * 2);
        sb.AppendLine(t.name);
        count++;

        for (int i = 0; i < t.childCount; i++)
            DumpIndentedRecursive(t.GetChild(i), sb, depth + 1, ref count);
    }

    private static string GetFullPathFromSelectedRoot(Transform t)
    {
        // Builds path from the selected root in the Hierarchy window.
        // If you select UI_Root, paths will start at UI_Root/...
        // If you select Prefab_HexWorld3D_Village, paths will start at Prefab_HexWorld3D_Village/...
        var parts = new System.Collections.Generic.List<string>(64);
        var cur = t;
        while (cur != null)
        {
            parts.Add(cur.name);
            if (cur == Selection.activeTransform) break;
            cur = cur.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }
}
