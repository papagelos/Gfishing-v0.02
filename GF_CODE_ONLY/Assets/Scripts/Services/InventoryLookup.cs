using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


namespace GalacticFishing
{
    /// <summary>
    /// Provides a resilient way to read total-caught values for a fish species.
    /// </summary>
    public static class InventoryLookup
    {
        static readonly string[] CandidateTypes =
        {
            "GalacticFishing.InventoryService",
            "InventoryService",
            "GalacticFishing.InventoryCountsPersistence",
            "InventoryCountsPersistence"
        };

        static readonly string[] CandidateMethods =
        {
            "GetTotalForSpecies",
            "GetTotalCaughtForSpecies",
            "GetCount",
            "Get"
        };

        static readonly BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        public static bool TryGetCaughtTotal(string speciesName, out int count)
        {
            count = 0;
            if (string.IsNullOrWhiteSpace(speciesName))
            {
                return false;
            }

            var wantNorm = Normalize(speciesName);

            for (int i = 0; i < CandidateTypes.Length; i++)
            {
                var type = Type.GetType(CandidateTypes[i]);
                if (type == null)
                {
                    continue;
                }

                if (TryFromType(type, null, speciesName, wantNorm, out count))
                {
                    return true;
                }

                var instance = GetSingleton(type);
                if (instance != null && TryFromType(type, instance, speciesName, wantNorm, out count))
                {
                    return true;
                }
            }

            return false;
        }

        static bool TryFromType(Type type, object target, string speciesName, string wantNorm, out int count)
        {
            count = 0;

            for (int i = 0; i < CandidateMethods.Length; i++)
            {
                var method = type.GetMethod(CandidateMethods[i], Any);
                if (method == null || method.GetParameters().Length != 1 || method.GetParameters()[0].ParameterType != typeof(string))
                {
                    continue;
                }

                if ((target == null && !method.IsStatic) || (target != null && method.IsStatic))
                {
                    continue;
                }

                try
                {
                    var result = method.Invoke(target, new object[] { speciesName });
                    if (result != null && int.TryParse(result.ToString(), out count))
                    {
                        return true;
                    }
                }
                catch
                {
                    // ignored
                }
            }

            if (TryGetCountsDictionary(type, target, out var dict))
            {
                if (dict.TryGetValue(speciesName, out count))
                {
                    return true;
                }

                foreach (var kv in dict)
                {
                    if (string.Equals(kv.Key, speciesName, StringComparison.OrdinalIgnoreCase))
                    {
                        count = kv.Value;
                        return true;
                    }
                }

                if (TryGetByFuzzyKey(dict, wantNorm, out count))
                {
                    return true;
                }
            }

            return false;
        }

        static bool TryGetByFuzzyKey(Dictionary<string, int> dict, string wantNorm, out int count)
        {
            count = 0;
            if (dict == null || string.IsNullOrEmpty(wantNorm))
            {
                return false;
            }

            foreach (var kv in dict)
            {
                if (NormalizeKey(kv.Key) == wantNorm)
                {
                    count = kv.Value;
                    return true;
                }
            }

            return false;
        }

        static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return NormalizeKey(value);
        }

        static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            value = value.Trim().Replace(' ', '_');
            while (value.Contains("__"))
            {
                value = value.Replace("__", "_");
            }

            var span = value.ToLowerInvariant().AsSpan();
            System.Text.StringBuilder sb = null;

            for (int i = 0; i < span.Length; i++)
            {
                var c = span[i];
                if (c == '_')
                {
                    sb ??= new System.Text.StringBuilder(span.Length);
                    sb.Append('_');
                    continue;
                }

                if (c == '-')
                {
                    continue;
                }

                sb ??= new System.Text.StringBuilder(span.Length);
                sb.Append(c);
            }

            return sb?.ToString() ?? string.Empty;
        }

        static object GetSingleton(Type type)
        {
            var names = new[] { "Instance", "Current", "Active" };
            for (int i = 0; i < names.Length; i++)
            {
                var prop = type.GetProperty(names[i], Any);
                if (prop != null && prop.CanRead && prop.GetIndexParameters().Length == 0)
                {
                    try
                    {
                        var value = prop.GetValue(null);
                        if (value != null)
                        {
                            return value;
                        }
                    }
                    catch
                    {
                    }
                }

                var field = type.GetField(names[i], Any);
                if (field != null)
                {
                    try
                    {
                        var value = field.GetValue(null);
                        if (value != null)
                        {
                            return value;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                try
                {
                    return UnityEngine.Object.FindObjectOfType(type);
                }
                catch
                {
                }
            }
            return null;
        }

        static bool TryGetCountsDictionary(Type type, object target, out Dictionary<string, int> dict)
        {
            dict = null;

            var prop = type.GetProperty("Counts", Any);
            if (prop != null && prop.CanRead)
            {
                try
                {
                    var obj = prop.GetValue(prop.GetGetMethod(true)?.IsStatic == true ? null : target);
                    if (obj is Dictionary<string, int> d)
                    {
                        dict = d;
                        return true;
                    }
                }
                catch
                {
                }
            }

            var field = type.GetField("Counts", Any);
            if (field != null)
            {
                try
                {
                    var obj = field.GetValue(field.IsStatic ? null : target);
                    if (obj is Dictionary<string, int> d)
                    {
                        dict = d;
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }
    }
}
