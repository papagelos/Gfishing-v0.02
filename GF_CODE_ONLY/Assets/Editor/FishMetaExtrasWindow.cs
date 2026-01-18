// Assets/Editor/FishMetaExtrasWindow.cs
// Legacy shim: features absorbed into the Unified Fish Meta Manager.
// Menu items removed to avoid duplicates. If opened programmatically, it forwards to the unified window.
#if UNITY_EDITOR
using UnityEditor;

namespace GalacticFishing.EditorTools
{
    internal sealed class FishMetaExtrasWindow : EditorWindow
    {
        private void OnEnable()
        {
            FishMetaManagerWindow.Open();
            Close();
        }
    }
}
#endif
