using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GalacticFishing
{
    [CreateAssetMenu(menuName = "GF/Data/Fish Meta Index")]
    public class FishMetaIndex : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public Fish fish;     // exact Fish asset this entry refers to (optional)
            public FishMeta meta; // the FishMeta (source of truth)
            public string key;    // human/name key (fallback when fish ref missing)
        }

        public List<Entry> entries = new();

        // Maps exact Fish asset -> FishMeta
        Dictionary<Fish, FishMeta> _byFish;

        // Maps various string keys -> FishMeta
        Dictionary<string, FishMeta> _byKey;

        // NEW: maps FishMeta -> Fish asset (for world pools that store FishMeta)
        Dictionary<FishMeta, Fish> _fishByMeta;

        void OnEnable() => BuildMap();

        static string Normalize(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();

        static IEnumerable<string> Variants(string raw)
        {
            if (string.IsNullOrEmpty(raw)) yield break;
            string k = Normalize(raw);
            if (string.IsNullOrEmpty(k)) yield break;

            const string prefix = "fish_";
            string AddPrefix(string x) => x.StartsWith(prefix) ? x : prefix + x;
            string RemovePrefix(string x) => x.StartsWith(prefix) ? x.Substring(prefix.Length) : x;

            string underscore = k.Replace(' ', '_');
            string spaced = k.Replace('_', ' ');

            static string Slug(string input)
            {
                var builder = new StringBuilder(input.Length);
                foreach (var ch in input)
                {
                    if (char.IsLetterOrDigit(ch)) builder.Append(char.ToLowerInvariant(ch));
                }
                return builder.ToString();
            }

            yield return k;
            yield return underscore;
            yield return spaced;
            yield return AddPrefix(k);
            yield return RemovePrefix(k);
            yield return AddPrefix(underscore);
            yield return RemovePrefix(underscore);
            yield return AddPrefix(spaced);
            yield return RemovePrefix(spaced);
            yield return Slug(k);
            yield return Slug(underscore);
            yield return Slug(spaced);
        }

        public void BuildMap()
        {
            _byFish    = new Dictionary<Fish, FishMeta>();
            _byKey     = new Dictionary<string, FishMeta>();
            _fishByMeta = new Dictionary<FishMeta, Fish>();

            if (entries == null) return;

            foreach (var e in entries)
            {
                if (!e.meta) continue;

                // Fish -> Meta
                if (e.fish && !_byFish.ContainsKey(e.fish))
                    _byFish[e.fish] = e.meta;

                // Meta -> Fish (for FishMeta pools)
                if (e.meta && e.fish && !_fishByMeta.ContainsKey(e.meta))
                    _fishByMeta[e.meta] = e.fish;

                // Key variants from explicit key
                if (!string.IsNullOrWhiteSpace(e.key))
                {
                    foreach (var key in Variants(e.key))
                    {
                        if (!_byKey.ContainsKey(key))
                            _byKey[key] = e.meta;
                    }
                }

                // Key variants from meta asset name
                foreach (var key in Variants(e.meta.name))
                {
                    if (!_byKey.ContainsKey(key))
                        _byKey[key] = e.meta;
                }
            }
        }

        public FishMeta FindByFish(Fish fish)
        {
            if (!fish) return null;

            if (_byFish != null &&
                _byFish.TryGetValue(fish, out var byAsset) &&
                byAsset)
                return byAsset;

            var dn = SafeDisplayName(fish);
            var primary = string.IsNullOrWhiteSpace(dn) ? fish.name : dn;

            if (_byKey != null)
            {
                foreach (var key in Variants(primary))
                {
                    if (_byKey.TryGetValue(key, out var byDisplay) && byDisplay)
                        return byDisplay;
                }

                foreach (var key in Variants(fish.name))
                {
                    if (_byKey.TryGetValue(key, out var byName) && byName)
                        return byName;
                }
            }

            return null;
        }

        // NEW: FishMeta -> Fish (for world definition pools)
        public Fish FindFishByMeta(FishMeta meta)
        {
            if (!meta) return null;

            if (_fishByMeta != null &&
                _fishByMeta.TryGetValue(meta, out var fish) &&
                fish)
                return fish;

            // Very small list, linear scan is fine as a fallback
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e.meta == meta && e.fish)
                        return e.fish;
                }
            }

            return null;
        }

        public static string SafeDisplayName(Fish fish)
        {
            if (!fish) return null;
            try
            {
                var t = fish.GetType();
                var p = t.GetProperty("displayName") ?? t.GetProperty("DisplayName");
                if (p != null)
                {
                    var v = p.GetValue(fish, null) as string;
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }
                var f = t.GetField("displayName") ?? t.GetField("DisplayName");
                if (f != null)
                {
                    var v = f.GetValue(fish) as string;
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }
            }
            catch { }
            return fish.name;
        }
    }
}
