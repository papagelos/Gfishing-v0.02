using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace GalacticFishing.UI
{
    public sealed class AIMessageLogService : MonoBehaviour
    {
        public static AIMessageLogService Instance { get; private set; }

        /// <summary>
        /// Fired whenever the log changes (add/clear/load).
        /// Panels can subscribe to refresh automatically.
        /// </summary>
        public event Action Changed;

        [Header("History")]
        [SerializeField] private bool persistToDisk = true;
        [SerializeField] private string saveFileName = "ai_log.json";

        [Tooltip("0 = unlimited. Otherwise, older entries are trimmed to keep this max.")]
        [SerializeField] private int maxEntries = 400;

        [SerializeField] private bool showTimestamps = true;

        [Serializable] private class Entry { public string utc; public string text; }
        [Serializable] private class Wrapper { public List<Entry> entries = new(); }

        private readonly List<Entry> _entries = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (persistToDisk) Load();
        }

        public void AddImportant(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            _entries.Add(new Entry
            {
                utc = DateTime.UtcNow.ToString("o"),
                text = text
            });

            // 0 or less = unlimited
            if (maxEntries > 0 && _entries.Count > maxEntries)
                _entries.RemoveRange(0, _entries.Count - maxEntries);

            if (persistToDisk) Save();
            Changed?.Invoke();
        }

        public void Clear()
        {
            _entries.Clear();
            if (persistToDisk) Save();
            Changed?.Invoke();
        }

        public string GetAllText() => GetFilteredText(null);

        public string GetFilteredText(string query)
        {
            query = string.IsNullOrWhiteSpace(query) ? null : query.Trim();

            // Token search: all tokens must match (space separated)
            string[] tokens = null;
            if (!string.IsNullOrWhiteSpace(query))
                tokens = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var sb = new StringBuilder(_entries.Count * 64);

            bool firstWritten = true;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e == null) continue;

                if (tokens != null && !MatchesAllTokens(e.text, tokens))
                    continue;

                if (!firstWritten)
                    sb.Append("\n\n");

                firstWritten = false;

                if (showTimestamps && DateTime.TryParse(e.utc, out var dt))
                    sb.Append('[').Append(dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")).Append("] ");

                sb.Append(e.text);
            }

            return sb.ToString();
        }

        private bool MatchesAllTokens(string haystack, string[] tokens)
        {
            if (string.IsNullOrEmpty(haystack)) return false;
            for (int i = 0; i < tokens.Length; i++)
            {
                var t = tokens[i];
                if (string.IsNullOrWhiteSpace(t)) continue;

                if (haystack.IndexOf(t, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }
            return true;
        }

        private string PathOnDisk => System.IO.Path.Combine(Application.persistentDataPath, saveFileName);

        private void Load()
        {
            try
            {
                if (!File.Exists(PathOnDisk)) { Changed?.Invoke(); return; }

                var json = File.ReadAllText(PathOnDisk);
                var wrapper = JsonUtility.FromJson<Wrapper>(json);

                _entries.Clear();
                if (wrapper?.entries != null)
                    _entries.AddRange(wrapper.entries);

                // Apply trimming after load too (0 = unlimited)
                if (maxEntries > 0 && _entries.Count > maxEntries)
                    _entries.RemoveRange(0, _entries.Count - maxEntries);

                Changed?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIMessageLog] Load failed: {ex.Message}");
            }
        }

        private void Save()
        {
            try
            {
                var wrapper = new Wrapper { entries = new List<Entry>(_entries) };
                var json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(PathOnDisk, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIMessageLog] Save failed: {ex.Message}");
            }
        }
    }
}
