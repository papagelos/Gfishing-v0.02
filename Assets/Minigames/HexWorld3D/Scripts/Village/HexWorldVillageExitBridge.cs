using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GalacticFishing.Minigames.HexWorld
{
    /// <summary>
    /// Bridge for leaving the HexWorld_Village scene.
    /// - Tries to call MenuRouter.Back() (your global "one step back" navigation).
    /// - If MenuRouter isn't available in this scene, falls back to loading a scene name.
    /// - Calls HexWorld3DController.SaveAutosave() if it exists (via reflection, so it won't break if you rename/remove it).
    /// </summary>
    public sealed class HexWorldVillageExitBridge : MonoBehaviour
    {
        [Header("Optional refs")]
        [SerializeField] private MonoBehaviour controller; // assign HexWorld3DController if you have it

        [Header("Input")]
        [SerializeField] private bool exitOnEscape = true;
        [SerializeField] private bool exitOnBackspace = true;

        [Tooltip("If true, don't exit when the pointer is over UI.")]
        [SerializeField] private bool ignoreWhenPointerOverUI = true;

        [Header("Fallback (if MenuRouter.Back() not found)")]
        [Tooltip("Set this to your main gameplay scene (the one you came from). Used only if MenuRouter isn't present.")]
        [SerializeField] private string fallbackSceneName = "";

        private void Reset()
        {
            // Best-effort auto-fill: find controller in scene
            controller = FindFirstMonoBehaviourByTypeName("HexWorld3DController");
        }

        private void Update()
        {
            if (Mouse.current == null || Keyboard.current == null)
                return;

            if (ignoreWhenPointerOverUI && EventSystem.current && EventSystem.current.IsPointerOverGameObject())
                return;

            if (exitOnEscape && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ExitNow();
                return;
            }

            if (exitOnBackspace && Keyboard.current.backspaceKey.wasPressedThisFrame)
            {
                ExitNow();
                return;
            }
        }

        /// <summary>Hook this to an Exit button if/when you add one.</summary>
        public void ExitNow()
        {
            // 1) Save (if the method exists)
            TryInvoke(controller, "SaveAutosave");

            // 2) Prefer your global "Back()" system if present
            var menuRouter = FindFirstMonoBehaviourByTypeName("MenuRouter");
            if (menuRouter != null && TryInvoke(menuRouter, "Back"))
                return;

            // 3) Fallback: just load a known scene
            if (!string.IsNullOrWhiteSpace(fallbackSceneName))
            {
                SceneManager.LoadScene(fallbackSceneName);
                return;
            }

            Debug.LogWarning("HexWorldVillageExitBridge: No MenuRouter.Back() found and fallbackSceneName is empty. Set fallbackSceneName in the Inspector.");
        }

        private static MonoBehaviour FindFirstMonoBehaviourByTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

#if UNITY_2023_1_OR_NEWER
            var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var all = FindObjectsOfType<MonoBehaviour>(true);
#endif
            for (int i = 0; i < all.Length; i++)
            {
                var mb = all[i];
                if (!mb) continue;
                var t = mb.GetType();
                if (t != null && string.Equals(t.Name, typeName, StringComparison.Ordinal))
                    return mb;
            }
            return null;
        }

        private static bool TryInvoke(object target, string methodName, params object[] args)
        {
            if (target == null) return false;
            if (string.IsNullOrEmpty(methodName)) return false;

            try
            {
                var t = target.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var m = t.GetMethod(methodName, flags);
                if (m == null) return false;

                m.Invoke(target, args);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"HexWorldVillageExitBridge: Invoke failed {target.GetType().Name}.{methodName}() -> {e.Message}");
                return false;
            }
        }
    }
}
