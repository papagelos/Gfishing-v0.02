using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using GalacticFishing.Progress;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem; // NEW input system

/// <summary>
/// Dev-only cheat menu (OnGUI) for quick testing.
/// Toggle with a key (Input System).
/// - Set/Add Credits
/// - Set Equipped Rod Upgrade Level (uses Set, not Increase)
/// - Force Final Rod Power (via modifier) so you can set exact power
/// </summary>
public sealed class DevCheatMenu : MonoBehaviour
{
    [Header("Toggle (Input System)")]
    [SerializeField] private Key toggleKey = Key.F1;
    [SerializeField] private bool startOpen = false;

    [Header("Window")]
    [SerializeField] private Rect windowRect = new Rect(20, 20, 460, 590);
    [SerializeField] private string windowTitle = "DEV CHEATS";

    // Text fields
    private string _creditsSet = "10000";
    private string _creditsAdd = "1000";

    private string _rodLevelSet = "0";

    // Force final power via modifier
    private string _targetRodPower = "19";
    private bool _forcePowerEnabled = false;

    // Status / debug
    private string _status = "";
    private float _statusUntil = 0f;

    private bool _open;

    private void Awake()
    {
        _open = startOpen;
    }

    private void Update()
    {
        if (WasPressedThisFrame(toggleKey))
            _open = !_open;
    }

    private bool WasPressedThisFrame(Key key)
    {
        var kb = Keyboard.current;
        if (kb == null) return false;

        var control = kb[key];
        return control != null && control.wasPressedThisFrame;
    }

    private void OnDisable()
    {
        // Clean up force-power modifier if this component gets disabled
        var ppm = PlayerProgressManager.Instance;
        if (ppm != null)
            ppm.RemoveRodPowerModifier(this);
    }

    private void OnGUI()
    {
        if (!Debug.isDebugBuild && !Application.isEditor)
            return;

        if (!_open)
            return;

        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, windowTitle);
    }

    private void DrawWindow(int id)
    {
        GUILayout.Space(4);

        var ppm = PlayerProgressManager.Instance;
        if (ppm == null)
        {
            GUILayout.Label("PlayerProgressManager.Instance is NULL.");
            GUILayout.Label("Add the PlayerProgressManager to your scene.");
            GUI.DragWindow();
            return;
        }

        // --- CURRENT VALUES ---
        GUILayout.Label("<b>Current</b>", RichLabelStyle());
        GUILayout.Label($"Credits: {ppm.GetCredits():N0}");
        GUILayout.Label($"Equipped Rod: {(ppm.Data?.gear?.equippedRodId ?? "(none)")}");
        GUILayout.Label($"Rod Upgrade Level: {ppm.GetEquippedRodUpgradeLevel()}");
        GUILayout.Label($"Rod Power (final): {ppm.CurrentRodPower:0.##}");

        if (Time.unscaledTime < _statusUntil && !string.IsNullOrEmpty(_status))
        {
            GUILayout.Space(4);
            GUILayout.Label(_status);
        }

        GUILayout.Space(10);

        // --- CREDITS ---
        GUILayout.Label("<b>Credits</b>", RichLabelStyle());

        GUILayout.BeginHorizontal();
        GUILayout.Label("Set:", GUILayout.Width(40));
        _creditsSet = GUILayout.TextField(_creditsSet, GUILayout.Width(120));
        if (GUILayout.Button("Apply", GUILayout.Width(80)))
        {
            if (TryParseFloat(_creditsSet, out float v))
            {
                EnsureCurrency(ppm);
                ppm.Data.currency.credits = Mathf.Max(0f, v);
                SetStatus($"Credits set to {ppm.Data.currency.credits:0}");
            }
            else SetStatus("Credits parse failed.");
        }
        if (GUILayout.Button("+1k", GUILayout.Width(60))) { AddCredits(ppm, 1000f); SetStatus("Added 1k credits."); }
        if (GUILayout.Button("+10k", GUILayout.Width(60))) { AddCredits(ppm, 10000f); SetStatus("Added 10k credits."); }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Add:", GUILayout.Width(40));
        _creditsAdd = GUILayout.TextField(_creditsAdd, GUILayout.Width(120));
        if (GUILayout.Button("Add", GUILayout.Width(80)))
        {
            if (TryParseFloat(_creditsAdd, out float v))
            {
                AddCredits(ppm, v);
                SetStatus($"Added {v:0} credits.");
            }
            else SetStatus("Credits add parse failed.");
        }
        if (GUILayout.Button("0", GUILayout.Width(60)))
        {
            EnsureCurrency(ppm);
            ppm.Data.currency.credits = 0f;
            SetStatus("Credits set to 0.");
        }
        if (GUILayout.Button("Save", GUILayout.Width(80)))
        {
            ppm.Save();
            SetStatus("Saved.");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(12);

        // --- ROD LEVEL ---
        GUILayout.Label("<b>Rod Upgrade Level (equipped)</b>", RichLabelStyle());
        GUILayout.BeginHorizontal();
        GUILayout.Label("Set:", GUILayout.Width(40));
        _rodLevelSet = GUILayout.TextField(_rodLevelSet, GUILayout.Width(120));
        if (GUILayout.Button("Apply", GUILayout.Width(80)))
        {
            if (int.TryParse(_rodLevelSet, out int lvl))
            {
                ppm.SetEquippedRodUpgradeLevel(lvl);
                ForceRodPowerRefresh(ppm);
                SetStatus($"Tried set rod upgrade level to {lvl}. Now: {ppm.GetEquippedRodUpgradeLevel()}");
            }
            else SetStatus("Rod level parse failed.");
        }

        // IMPORTANT: Don't use IncreaseEquippedRodUpgradeLevel() at all.
        // It may clamp negatives and/or have changed behavior in recent patches.
        if (GUILayout.Button("-1", GUILayout.Width(50)))
        {
            int cur = ppm.GetEquippedRodUpgradeLevel();
            ppm.SetEquippedRodUpgradeLevel(cur - 1);
            ForceRodPowerRefresh(ppm);
            SetStatus($"Rod level: {cur} -> {ppm.GetEquippedRodUpgradeLevel()}");
        }
        if (GUILayout.Button("+1", GUILayout.Width(50)))
        {
            int cur = ppm.GetEquippedRodUpgradeLevel();
            ppm.SetEquippedRodUpgradeLevel(cur + 1);
            ForceRodPowerRefresh(ppm);
            SetStatus($"Rod level: {cur} -> {ppm.GetEquippedRodUpgradeLevel()}");
        }
        if (GUILayout.Button("+5", GUILayout.Width(50)))
        {
            int cur = ppm.GetEquippedRodUpgradeLevel();
            ppm.SetEquippedRodUpgradeLevel(cur + 5);
            ForceRodPowerRefresh(ppm);
            SetStatus($"Rod level: {cur} -> {ppm.GetEquippedRodUpgradeLevel()}");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(12);

        // --- FORCE FINAL ROD POWER ---
        GUILayout.Label("<b>Force Final Rod Power (via modifier)</b>", RichLabelStyle());
        GUILayout.Label("This guarantees CurrentRodPower becomes the number you type (great when upgrades no longer affect power).");
        _forcePowerEnabled = GUILayout.Toggle(_forcePowerEnabled, "Enable");

        GUILayout.BeginHorizontal();
        GUILayout.Label("Target:", GUILayout.Width(50));
        _targetRodPower = GUILayout.TextField(_targetRodPower, GUILayout.Width(90));

        if (GUILayout.Button("Set", GUILayout.Width(80)))
        {
            if (!_forcePowerEnabled)
            {
                SetStatus("Enable the toggle first.");
            }
            else if (TryParseFloat(_targetRodPower, out float target))
            {
                float current = ppm.CurrentRodPower;
                float add = target - current;
                float mul = 1f;

                ppm.SetRodPowerModifier(this, add, mul);
                ForceRodPowerRefresh(ppm);

                SetStatus($"Forced power: {current:0.##} -> {ppm.CurrentRodPower:0.##} (target {target:0.##})");
            }
            else SetStatus("Target power parse failed.");
        }

        if (GUILayout.Button("Clear", GUILayout.Width(80)))
        {
            ppm.RemoveRodPowerModifier(this);
            ForceRodPowerRefresh(ppm);
            SetStatus("Force power cleared.");
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(12);

        // --- CLOSE ---
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Close", GUILayout.Height(28)))
            _open = false;

        if (GUILayout.Button("Save + Close", GUILayout.Height(28)))
        {
            ppm.Save();
            _open = false;
            SetStatus("Saved + closed.");
        }
        GUILayout.EndHorizontal();

        GUI.DragWindow();
    }

    private void SetStatus(string msg, float seconds = 2.0f)
    {
        _status = msg;
        _statusUntil = Time.unscaledTime + seconds;
    }

    private static void EnsureCurrency(PlayerProgressManager ppm)
    {
        if (ppm.Data == null) return;
        if (ppm.Data.currency == null)
            ppm.Data.currency = new PlayerCurrencyData();
    }

    private static void AddCredits(PlayerProgressManager ppm, float amount)
    {
        if (amount <= 0f) return;
        ppm.AddCredits(amount);
    }

    private static bool TryParseFloat(string s, out float value)
    {
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;

        return float.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private static GUIStyle RichLabelStyle()
    {
        var st = new GUIStyle(GUI.skin.label);
        st.richText = true;
        return st;
    }

    /// <summary>
    /// If PlayerProgressManager started caching rod power after recent patches,
    /// this tries to poke common refresh methods via reflection (harmless if missing).
    /// </summary>
    private static void ForceRodPowerRefresh(PlayerProgressManager ppm)
    {
        if (ppm == null) return;

        TryInvoke(ppm, "RecalculateRodPower");
        TryInvoke(ppm, "RefreshRodPower");
        TryInvoke(ppm, "RebuildRodPower");
        TryInvoke(ppm, "NotifyRodPowerChanged");
        TryInvoke(ppm, "OnRodPowerChanged");
    }

    private static void TryInvoke(object obj, string methodName)
    {
        try
        {
            var t = obj.GetType();
            var m = t.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m != null && m.GetParameters().Length == 0)
                m.Invoke(obj, null);
        }
        catch { /* ignore */ }
    }
}
#else
public sealed class DevCheatMenu : MonoBehaviour { }
#endif
