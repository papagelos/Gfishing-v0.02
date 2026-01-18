using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.UI
{
    public static class RMBBlocker
    {
        private static int _count = 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const int MaxHistory = 32;
        private static readonly Queue<string> _history = new Queue<string>(MaxHistory);
#endif

        public static bool IsBlocked => _count > 0;

        /// <summary>Current blocker depth (debug/inspection).</summary>
        public static int Count => _count;

        // This runs even when Domain Reload is disabled, preventing "stuck blocked" between play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitOnPlay()
        {
            _count = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _history.Clear();
#endif
        }

        public static void Push()
        {
            _count++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddHistory($"+ Push -> {_count}");
#endif
        }

        public static void Pop()
        {
            if (_count <= 0)
            {
                _count = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                AddHistory("! Pop while already 0");
                Debug.LogWarning("[RMBBlocker] Pop() called while count is already 0. This usually means mismatched Push/Pop.");
#endif
                return;
            }

            _count = Mathf.Max(0, _count - 1);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddHistory($"- Pop -> {_count}");
#endif
        }

        public static void ResetAll()
        {
            _count = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddHistory("x ResetAll -> 0");
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Useful if you ever want to print current push/pop history when debugging.</summary>
        public static string GetDebugHistory()
        {
            return string.Join("\n", _history);
        }

        private static void AddHistory(string line)
        {
            if (_history.Count >= MaxHistory) _history.Dequeue();
            _history.Enqueue(line);
        }
#endif
    }
}
