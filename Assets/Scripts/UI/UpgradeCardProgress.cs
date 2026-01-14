using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalacticFishing.UI
{
    /// <summary>
    /// Very small save system for upgrade cards.
    /// Stores, per deckId, which card indices have been unlocked (revealed).
    /// Uses PlayerPrefs with a JSON blob.
    /// </summary>
    public static class UpgradeCardProgress
    {
        private const string PlayerPrefsKey = "upgrade_cards_v1";

        [Serializable]
        private class DeckSave
        {
            public string deckId;
            public string bits; // "01011..." where '1' = unlocked
        }

        [Serializable]
        private class SaveRoot
        {
            public List<DeckSave> decks = new();
        }

        private static readonly Dictionary<string, bool[]> Cache = new();
        private static bool _loaded;

        // ------------------------------------------------------------
        // Load / cache
        // ------------------------------------------------------------

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            Cache.Clear();
            string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                var root = JsonUtility.FromJson<SaveRoot>(json);
                if (root?.decks == null) return;

                foreach (var deck in root.decks)
                {
                    if (string.IsNullOrEmpty(deck.deckId) ||
                        string.IsNullOrEmpty(deck.bits))
                        continue;

                    var arr = new bool[deck.bits.Length];
                    for (int i = 0; i < deck.bits.Length; i++)
                        arr[i] = deck.bits[i] == '1';

                    Cache[deck.deckId] = arr;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UpgradeCardProgress] Failed to load progress: " + e);
            }
        }

        /// <summary>
        /// Returns the bool[] for a deck. Ensures it is at least minSize long.
        /// </summary>
        public static bool[] LoadDeck(string deckId, int minSize)
        {
            if (string.IsNullOrWhiteSpace(deckId))
                return null;

            EnsureLoaded();

            if (!Cache.TryGetValue(deckId, out var arr) || arr == null)
            {
                arr = new bool[Mathf.Max(minSize, 0)];
                Cache[deckId] = arr;
            }
            else if (arr.Length < minSize)
            {
                Array.Resize(ref arr, minSize);
                Cache[deckId] = arr;
            }

            return arr;
        }

        // ------------------------------------------------------------
        // Mark unlocked + save
        // ------------------------------------------------------------

        public static void MarkUnlocked(string deckId, int cardIndex)
        {
            if (string.IsNullOrWhiteSpace(deckId) || cardIndex < 0)
                return;

            var arr = LoadDeck(deckId, cardIndex + 1);
            if (cardIndex >= arr.Length)
                return;

            if (arr[cardIndex])
                return; // already unlocked

            arr[cardIndex] = true;
            SaveAll();
        }

        private static void SaveAll()
        {
            EnsureLoaded();

            var root = new SaveRoot();

            foreach (var kvp in Cache)
            {
                var arr = kvp.Value ?? Array.Empty<bool>();

                char[] bits = new char[arr.Length];
                for (int i = 0; i < arr.Length; i++)
                    bits[i] = arr[i] ? '1' : '0';

                root.decks.Add(new DeckSave
                {
                    deckId = kvp.Key,
                    bits   = new string(bits)
                });
            }

            string json = JsonUtility.ToJson(root);
            PlayerPrefs.SetString(PlayerPrefsKey, json);
            PlayerPrefs.Save();
        }
    }
}
