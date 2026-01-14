// Assets/Editor/FishMetaFieldsHelperWindow.cs
// Legacy shim: the old helper window is absorbed into the Unified Fish Meta Manager.
// Menu items removed to avoid duplicates. If opened programmatically, it forwards to the unified window.
#if UNITY_EDITOR
using UnityEditor;

namespace GalacticFishing.EditorTools
{
    internal sealed class FishMetaFieldsHelperWindow : EditorWindow
    {
        private void OnEnable()
        {
            FishMetaManagerWindow.Open();
            Close();
        }
    }
}
#endif
