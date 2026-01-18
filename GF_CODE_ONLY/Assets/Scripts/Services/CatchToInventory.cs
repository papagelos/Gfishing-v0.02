using System;
using System.Reflection;
using UnityEngine;
using GalacticFishing;          // Fish, FishRegistry, FishMeta, FishMetaIndex
using GalacticFishing.UI;       // FloatingTextManager
using GalacticFishing.Progress; // PlayerProgressManager

/// <summary>
/// Hooks FishingMinigameController success into stats persistence and
/// auto-rewards:
/// - Credits are granted immediately based on FishMeta.Sellvalue and size.
/// - Optional material drops via FishMeta.MaterialDropId / MaterialDropQuantity.
/// - THIS FISH card still updates.
/// Attach this to an always-alive object (e.g. your "Systems" GameObject).
/// </summary>
public class CatchToInventory : MonoBehaviour
{
    [Header("Links")]
    [SerializeField] private FishingMinigameController minigame;
    [SerializeField] private FishRegistry registry;

    [Tooltip("Optional: if registry unassigned, we try to fetch it from an InventoryGridController in the scene.")]
    [SerializeField] private InventoryGridController inventoryGrid;

    [Tooltip("CanvasGroup on Inventory-background (used to detect when the inventory window is visible).")]
    [SerializeField] private CanvasGroup inventoryBackgroundGroup;

    [Header("Hook Card (THIS FISH stats)")]
    [Tooltip("Optional direct reference. If left empty, we'll use HookCardThisFishBinder.Instance.")]
    [SerializeField] private HookCardThisFishBinder hookCardThisFishBinder;

    [Header("Economy / Rewards")]
    [Tooltip("Optional: used to look up FishMeta (Sellvalue, MaterialDropId, etc.) from a Fish species.")]
    [SerializeField] private FishMetaIndex metaIndex;

    [Tooltip("If true, credits are automatically awarded on catch based on FishMeta.Sellvalue and fish length.")]
    [SerializeField] private bool awardCoinsOnCatch = true;

    [Tooltip("If true, material drops are processed from FishMeta.MaterialDropId / MaterialDropQuantity.")]
    [SerializeField] private bool awardMaterialsOnCatch = true;

    [Tooltip("Global multiplier on top of Sellvalue * fishLengthMeters. 1.0 = no bonus. Upgrades can change this later.")]
    [SerializeField] private float sellPriceMultiplier = 1.0f;

    [Header("Debug")]
    [SerializeField] private bool logs = false;

    [Header("Economy Debug (last catch)")]
    [SerializeField, Tooltip("Last raw (unrounded) sale value computed for a catch.")]
    private float lastSaleRaw;

    [SerializeField, Tooltip("Last number of credits granted for a catch.")]
    private int lastAwardedCoins;

    [SerializeField, Tooltip("Last material ID awarded for a catch.")]
    private string lastMaterialId;

    [SerializeField, Tooltip("Last material quantity awarded for a catch.")]
    private int lastMaterialQuantity;

    [Header("Stats Reading")]
    [Tooltip("If true, only accept stats from runtime components; no identity fallbacks.")]
    [SerializeField] private bool strictRuntimeStatsOnly = true;

    [Tooltip("Include stat source tags (which component supplied W/L/Q) when logging.")]
    [SerializeField] private bool logStatSources = true;

    [Header("Inventory UI Refresh")]
    [Tooltip("If true, refresh the inventory grid whenever the inventory window becomes visible.")]
    [SerializeField] private bool autoRefreshInventoryOnOpen = true;

    [Tooltip("CanvasGroup alpha above this is treated as 'visible'.")]
    [SerializeField] private float visibleThreshold = 0.5f;

    [Header("Floating text (optional)")]
    [Tooltip("Show floating text when a fish is successfully caught.")]
    [SerializeField] private bool showCatchFloatingText = true;

    [Tooltip("World-space offset from the MAIN CAMERA where catch text will spawn.\nX = left/right, Y = up/down, Z = distance in front of camera.")]
    [SerializeField] private Vector3 catchTextWorldOffset = new Vector3(0f, 0f, 5f);

    [Tooltip("Color of the floating text for catches.")]
    [SerializeField] private Color catchTextColor = Color.white;

    // internal state for visibility edge-detect
    bool _inventoryWasVisible;

    // ---- subscription (supports C# event or UnityEvent) ----
    EventInfo _eventInfo;
    Delegate _eventHandlerDelegate;
    FieldInfo _unityEventField;
    object _unityEventInstance;
    Delegate _unityActionDelegate;

    static readonly BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    // ---- events so other systems can listen to rewards ----
    /// <summary>
    /// Fired whenever credits are awarded automatically on catch.
    /// Args: (Fish species, registryId, rawPrice, creditsAwarded).
    /// </summary>
    public static event Action<Fish, int, float, int> OnCoinsAwarded;

    /// <summary>
    /// Fired whenever a material drop is processed.
    /// Args: (Fish species, registryId, materialId, quantity).
    /// </summary>
    public static event Action<Fish, int, string, int> OnMaterialAwarded;

    private void Awake()
    {
        if (!minigame)
            minigame = FindFirstObjectByType<FishingMinigameController>();

        if (!inventoryGrid)
            inventoryGrid = FindFirstObjectByType<InventoryGridController>();

        if (!registry)
        {
            // Try to steal the registry reference off InventoryGridController via property/field.
            if (inventoryGrid)
            {
                var t = inventoryGrid.GetType();

                var p = t.GetProperty("FishRegistry", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (p != null && typeof(FishRegistry).IsAssignableFrom(p.PropertyType))
                {
                    registry = p.GetValue(inventoryGrid) as FishRegistry;
                }
                else
                {
                    var f = t.GetField("FishRegistry", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (f != null && typeof(FishRegistry).IsAssignableFrom(f.FieldType))
                        registry = f.GetValue(inventoryGrid) as FishRegistry;
                }
            }
        }

        if (registry && !InventoryService.IsInitialized)
            InventoryService.Initialize(registry);

        // Try to auto-wire the canvas group if missing.
        if (!inventoryBackgroundGroup && inventoryGrid)
            inventoryBackgroundGroup = inventoryGrid.GetComponent<CanvasGroup>();

        // Try to auto-find a meta index if not wired yet.
        if (!metaIndex)
            metaIndex = FindFirstObjectByType<FishMetaIndex>();
    }

    private void OnEnable()
    {
        if (minigame == null)
        {
            Debug.LogWarning("[CatchToInventory] No FishingMinigameController found.");
            return;
        }

        var t = minigame.GetType();

        // Try C# event OnFishHooked
        _eventInfo = t.GetEvent("OnFishHooked", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        if (_eventInfo != null)
        {
            _eventHandlerDelegate = Delegate.CreateDelegate(_eventInfo.EventHandlerType, this, nameof(OnFishHooked));
            _eventInfo.AddEventHandler(minigame, _eventHandlerDelegate);
            if (logs) Debug.Log("[CatchToInventory] Subscribed to OnFishHooked (C# event).");
            return;
        }

        // Fallback UnityEvent field OnFishHooked
        _unityEventField = t.GetField("OnFishHooked", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        if (_unityEventField != null)
        {
            _unityEventInstance = _unityEventField.GetValue(minigame);
            if (_unityEventInstance != null)
            {
                var ueType = _unityEventInstance.GetType();
                var add = ueType.GetMethod("AddListener", BindingFlags.Public | BindingFlags.Instance);
                if (add != null)
                {
                    var unityActionType = typeof(UnityEngine.Events.UnityAction<>).MakeGenericType(typeof(FishIdentity));
                    _unityActionDelegate = Delegate.CreateDelegate(unityActionType, this, nameof(OnFishHooked));
                    add.Invoke(_unityEventInstance, new object[] { _unityActionDelegate });
                    if (logs) Debug.Log("[CatchToInventory] Subscribed to OnFishHooked (UnityEvent).");
                    return;
                }
            }
        }

        Debug.LogWarning("[CatchToInventory] Could not subscribe to OnFishHooked.");
    }

    private void OnDisable()
    {
        if (_eventInfo != null && _eventHandlerDelegate != null && minigame != null)
        {
            _eventInfo.RemoveEventHandler(minigame, _eventHandlerDelegate);
            _eventInfo = null;
            _eventHandlerDelegate = null;
        }

        if (_unityEventField != null && _unityEventInstance != null && _unityActionDelegate != null)
        {
            var ueType = _unityEventInstance.GetType();
            var remove = ueType.GetMethod("RemoveListener", BindingFlags.Public | BindingFlags.Instance);
            remove?.Invoke(_unityEventInstance, new object[] { _unityActionDelegate });
            _unityEventField = null;
            _unityEventInstance = null;
            _unityActionDelegate = null;
        }
    }

    private void Update()
    {
        if (!autoRefreshInventoryOnOpen) return;
        if (!inventoryBackgroundGroup || !inventoryGrid) return;

        bool visibleNow = IsInventoryVisible();

        // Inventory just became visible -> always refresh the grid.
        if (visibleNow && !_inventoryWasVisible)
        {
            ForceInventoryGridRefresh();
        }

        _inventoryWasVisible = visibleNow;
    }

    // ---- event handler ----
    private void OnFishHooked(FishIdentity identity)
    {
        if (identity == null) return;

        if (!InventoryService.IsInitialized && registry != null)
            InventoryService.Initialize(registry);

        // id resolution (as before)
        int id = ResolveFishId(identity, registry, out string dbgName, out string dbgSpriteName);
        if (id < 0)
        {
            Debug.LogWarning($"[CatchToInventory] Could not resolve fish id for caught fish. name='{dbgName ?? "?"}' sprite='{dbgSpriteName ?? "?"}'.");
            SafeDespawnCaughtFish(identity);
            return;
        }

        // Need fish species (used for toasts + economy)
        Fish species = (registry != null && id >= 0 && id < registry.fishes.Count)
            ? registry.fishes[id]
            : null;

        // Read runtime stats (weight / length / quality etc.)
        var stats = ReadRuntimeStats(identity, strictRuntimeStatsOnly,
            out var wSrc, out var lSrc, out var qSrc);

        if (logs)
        {
            Debug.Log(
                $"[CatchToInventory] Runtime stats → hasW={stats.hasWeight}, W={stats.weightKg:0.###}, " +
                $"hasL={stats.hasLength}, L={stats.lengthCm:0.#}"
            );
        }

        // ---- THIS FISH → HookCard binder ----
        HookCardThisFishBinder binder =
            hookCardThisFishBinder != null ? hookCardThisFishBinder : HookCardThisFishBinder.Instance;

        if (binder != null)
        {
            float w = stats.hasWeight ? stats.weightKg : 0f;
            float l = stats.hasLength ? stats.lengthCm : 0f;

            if (logs)
                Debug.Log($"[CatchToInventory] Sending THIS FISH to binder '{binder.gameObject.name}' → W={w}, L={l}");

            binder.SetFromThisFish(w, l);
        }
        else if (logs)
        {
            Debug.LogWarning("[CatchToInventory] No HookCardThisFishBinder found (field or Instance). THIS FISH overlay will stay at placeholder.");
        }

        // -----------------------------------------------------------
        // A) STAT RECORDING
        // -----------------------------------------------------------

        // Capture previous PERSONAL bests BEFORE recording this catch.
        float? prevBestW = null;
        float? prevBestL = null;

        if (InventoryStatsService.TryGetWeightRecord(id, out var prevWRec))
            prevBestW = prevWRec.weightKg;

        if (InventoryStatsService.TryGetLengthRecord(id, out var prevLRec))
            prevBestL = prevLRec.lengthCm;

        // Record the catch (creates/updates the new best in the stats service).
        InventoryStatsService.RecordCatch(id, stats);

        // IMPORTANT ORDER FIX:
        // Notify record toast watchers BEFORE we nudge InventoryService.OnChanged.
        // Otherwise the watcher may refresh its internal snapshot first, making
        // "previous == new" and suppressing the toast.
        NotifyRecordToastWatchers_ReflectionSafe(id, species, stats, prevBestW, prevBestL);

        // Some watcher systems/UI still wake up on InventoryService.OnChanged,
        // so we nudge AFTER toast notify.
        NudgeInventoryChanged(id);

        // -----------------------------------------------------------
        // B) ECONOMY: SALE + MATERIAL DROP
        // -----------------------------------------------------------

        // Reset debug values
        lastSaleRaw = 0f;
        lastAwardedCoins = 0;
        lastMaterialId = null;
        lastMaterialQuantity = 0;

        FishMeta meta = null;
        if (metaIndex != null && species != null)
        {
            try { meta = metaIndex.FindByFish(species) as FishMeta; }
            catch (Exception ex)
            {
                if (logs) Debug.LogWarning("[CatchToInventory] metaIndex.FindByFish threw: " + ex.Message, this);
            }
        }

        // Boat / upgrade multiplier (we piggyback on quantityFactorMultiplier for now)
        float upgradeSellMultiplier = 1.0f;
        var ppm = PlayerProgressManager.Instance;
        if (ppm != null && ppm.TryBuildCurrentBoatStats(out var boatStats))
        {
            upgradeSellMultiplier = Mathf.Max(0.01f, boatStats.quantityFactorMultiplier);
        }

        // ---- Credits ----
        if (awardCoinsOnCatch && meta != null)
        {
            // If we didn't get a proper runtime length, treat as 1m fish (100cm)
            float lengthCm = stats.hasLength && stats.lengthCm > 0f ? stats.lengthCm : 100f;
            float combinedMultiplier = sellPriceMultiplier * upgradeSellMultiplier;

            float rawPrice = FishPricing.CalculateCustomPrice(
                meta,
                species,
                lengthCm,
                combinedMultiplier
            );
            lastSaleRaw = rawPrice;

            if (rawPrice > 0.01f && ppm != null)
            {
                int credits = Mathf.Max(1, Mathf.RoundToInt(rawPrice));
                ppm.AddCredits(credits);
                lastAwardedCoins = credits;

                if (logs)
                    Debug.Log($"[CatchToInventory] Auto credits: fish='{dbgName}' id={id} len={lengthCm:0.#}cm price={rawPrice:0.##} → +{credits} Cr (mult={combinedMultiplier:0.###}).", this);

                OnCoinsAwarded?.Invoke(species, id, rawPrice, credits);
            }
        }
        else if (awardCoinsOnCatch && meta == null && logs)
        {
            Debug.LogWarning($"[CatchToInventory] No FishMeta found for fish '{dbgName}' id={id}; no credits awarded.", this);
        }

        // ---- Materials ----
        if (awardMaterialsOnCatch && meta != null)
        {
            if (TryReadStringAny(meta, new[] { "MaterialDropId", "materialDropId" }, out string materialId) &&
                !string.IsNullOrWhiteSpace(materialId))
            {
                float qtyFloat = 0f;
                // Prefer float quantity, but fall back to int if needed
                if (!TryReadFloatAny(meta, new[] { "MaterialDropQuantity", "materialDropQuantity" }, out qtyFloat))
                {
                    if (TryReadIntAny(meta, new[] { "MaterialDropQuantity", "materialDropQuantity" }, out int qtyInt))
                        qtyFloat = qtyInt;
                }

                if (qtyFloat <= 0f) qtyFloat = 1f;
                int finalQty = Mathf.Max(1, Mathf.RoundToInt(qtyFloat));

                lastMaterialId = materialId;
                lastMaterialQuantity = finalQty;

                if (logs)
                    Debug.Log($"[CatchToInventory] Material drop: fish='{dbgName}' id={id} → +{finalQty} x '{materialId}'.", this);

                // Hook your material inventory system here later:
                // ItemService.Instance?.Add(materialId, finalQty);

                OnMaterialAwarded?.Invoke(species, id, materialId, finalQty);
            }
        }

        // -----------------------------------------------------------
        // C) INVENTORY: DO NOT ADD FISH TO INVENTORY COUNT
        // -----------------------------------------------------------
        // InventoryService.Add(id, 1);   <-- intentionally removed (fish are auto-sold)

        // If the inventory is currently visible, we might still want to refresh it
        // (e.g. if UI depends on RecordCatch stats).
        if (inventoryGrid != null && inventoryBackgroundGroup != null && IsInventoryVisible())
        {
            ForceInventoryGridRefresh();
        }

        // -----------------------------------------------------------
        // D) FLOATING TEXT (SHOW CREDITS EARNED)
        // -----------------------------------------------------------
        var ftm = FloatingTextManager.Instance;
        if (ftm != null && showCatchFloatingText && awardCoinsOnCatch && lastAwardedCoins > 0)
        {
            string msg = $"+{lastAwardedCoins:N0} Credits";

            Camera cam = Camera.main;
            Vector3 worldPos = cam
                ? cam.transform.position + cam.transform.forward * catchTextWorldOffset.z
                  + cam.transform.right * catchTextWorldOffset.x
                  + cam.transform.up * catchTextWorldOffset.y
                : Vector3.zero;

            ftm.SpawnWorld(msg, worldPos, catchTextColor);
        }

        // -----------------------------------------------------------
        // E) FINAL DEBUG + DESPAWN
        // -----------------------------------------------------------
        if (logs)
        {
            string sw = stats.hasWeight ? stats.weightKg.ToString("0.###") + "kg" : "—";
            string sl = stats.hasLength ? stats.lengthCm.ToString("0.#") + "cm" : "—";
            string sq = stats.hasQuality ? stats.quality.ToString() : "—";
            string sale = lastAwardedCoins > 0 ? $" SOLD for {lastAwardedCoins:N0} Cr." : "";
            string src = logStatSources ? $" src(W:{wSrc}, L:{lSrc}, Q:{qSrc})" : "";
            Debug.Log($"[CatchToInventory] Hooked '{dbgName}' id={id} W={sw} L={sl} Q={sq}{src}{sale}", this);
        }

        SafeDespawnCaughtFish(identity);
    }

    // ------------------------------------------------------------------
    // Notify RecordToastWatcher in a way that never breaks compilation,
    // regardless of the watcher’s current NotifyCatch signature.
    // ------------------------------------------------------------------
    private static void NotifyRecordToastWatchers_ReflectionSafe(
        int registryId,
        Fish fish,
        InventoryStatsService.RuntimeStats stats,
        float? prevBestWeightKg,
        float? prevBestLengthCm)
    {
        // Find watchers (include inactive just in case).
#pragma warning disable 0618
        var watchers = UnityEngine.Object.FindObjectsOfType<RecordToastWatcher>(true);
#pragma warning restore 0618
        if (watchers == null || watchers.Length == 0) return;

        float? newW = stats.hasWeight ? stats.weightKg : (float?)null;
        float? newL = stats.hasLength ? stats.lengthCm : (float?)null;
        bool hadPrevious = prevBestWeightKg.HasValue || prevBestLengthCm.HasValue;

        foreach (var w in watchers)
        {
            if (!w) continue;

            try
            {
                var wt = w.GetType();
                var methods = wt.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                MethodInfo best = null;
                object[] bestArgs = null;

                foreach (var m in methods)
                {
                    if (!string.Equals(m.Name, "NotifyCatch", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var ps = m.GetParameters();
                    var args = BuildNotifyCatchArgs(ps, registryId, fish, stats, prevBestWeightKg, prevBestLengthCm, newW, newL, hadPrevious);
                    if (args == null) continue;

                    // Prefer the most "specific" overload (more params usually means more context).
                    if (best == null || ps.Length > best.GetParameters().Length)
                    {
                        best = m;
                        bestArgs = args;
                    }
                }

                if (best != null)
                {
                    best.Invoke(w, bestArgs);
                }
            }
            catch
            {
                // Never break gameplay if toast system throws.
            }
        }
    }

    private static object[] BuildNotifyCatchArgs(
        ParameterInfo[] ps,
        int registryId,
        Fish fish,
        InventoryStatsService.RuntimeStats stats,
        float? prevW,
        float? prevL,
        float? newW,
        float? newL,
        bool hadPrevious)
    {
        if (ps == null) return null;

        object[] args = new object[ps.Length];

        // Track which "float slots" have been used if names are unhelpful.
        int floatPrevUsed = 0; // 0 -> none, 1 -> weight used, 2 -> both used
        int floatNewUsed = 0;

        for (int i = 0; i < ps.Length; i++)
        {
            var p = ps[i];
            var pt = p.ParameterType;
            string pn = p.Name ?? "";

            // int / registry id
            if (pt == typeof(int))
            {
                args[i] = registryId;
                continue;
            }

            // Fish
            if (typeof(Fish).IsAssignableFrom(pt))
            {
                args[i] = fish;
                continue;
            }

            // RuntimeStats
            if (pt == typeof(InventoryStatsService.RuntimeStats))
            {
                args[i] = stats;
                continue;
            }

            // Nullable float or float (prev/new weight/length)
            if (pt == typeof(float?) || pt == typeof(float))
            {
                bool wantsPrev = pn.IndexOf("prev", StringComparison.OrdinalIgnoreCase) >= 0
                                 || pn.IndexOf("old", StringComparison.OrdinalIgnoreCase) >= 0
                                 || pn.IndexOf("compare", StringComparison.OrdinalIgnoreCase) >= 0;

                bool wantsNew = pn.IndexOf("new", StringComparison.OrdinalIgnoreCase) >= 0
                                || pn.IndexOf("current", StringComparison.OrdinalIgnoreCase) >= 0
                                || pn.IndexOf("catch", StringComparison.OrdinalIgnoreCase) >= 0;

                bool wantsWeight = pn.IndexOf("weight", StringComparison.OrdinalIgnoreCase) >= 0
                                   || pn.IndexOf("kg", StringComparison.OrdinalIgnoreCase) >= 0;

                bool wantsLength = pn.IndexOf("length", StringComparison.OrdinalIgnoreCase) >= 0
                                   || pn.IndexOf("len", StringComparison.OrdinalIgnoreCase) >= 0
                                   || pn.IndexOf("cm", StringComparison.OrdinalIgnoreCase) >= 0;

                float? chosen = null;

                if (wantsPrev || (!wantsNew && (pn.Length > 0)))
                {
                    if (wantsWeight) chosen = prevW;
                    else if (wantsLength) chosen = prevL;
                    else
                    {
                        // Unnamed: fill prev weight then prev length
                        if (floatPrevUsed == 0) { chosen = prevW; floatPrevUsed = 1; }
                        else if (floatPrevUsed == 1) { chosen = prevL; floatPrevUsed = 2; }
                    }
                }
                else if (wantsNew)
                {
                    if (wantsWeight) chosen = newW;
                    else if (wantsLength) chosen = newL;
                    else
                    {
                        // Unnamed: fill new weight then new length
                        if (floatNewUsed == 0) { chosen = newW; floatNewUsed = 1; }
                        else if (floatNewUsed == 1) { chosen = newL; floatNewUsed = 2; }
                    }
                }
                else
                {
                    // If parameter name gives no clue, try prevs first, then news.
                    if (floatPrevUsed == 0) { chosen = prevW; floatPrevUsed = 1; }
                    else if (floatPrevUsed == 1) { chosen = prevL; floatPrevUsed = 2; }
                    else if (floatNewUsed == 0) { chosen = newW; floatNewUsed = 1; }
                    else if (floatNewUsed == 1) { chosen = newL; floatNewUsed = 2; }
                }

                if (pt == typeof(float))
                    args[i] = chosen.HasValue ? chosen.Value : 0f;
                else
                    args[i] = chosen;

                continue;
            }

            // bool flags (hadPrevious / isNew / etc.)
            if (pt == typeof(bool))
            {
                bool isNewSpecies = !hadPrevious;
                if (pn.IndexOf("new", StringComparison.OrdinalIgnoreCase) >= 0 && pn.IndexOf("species", StringComparison.OrdinalIgnoreCase) >= 0)
                    args[i] = isNewSpecies;
                else if (pn.IndexOf("had", StringComparison.OrdinalIgnoreCase) >= 0 && pn.IndexOf("previous", StringComparison.OrdinalIgnoreCase) >= 0)
                    args[i] = hadPrevious;
                else
                    args[i] = hadPrevious; // safe default

                continue;
            }

            // Unknown parameter type: give default
            args[i] = pt.IsValueType ? Activator.CreateInstance(pt) : null;
        }

        // If an overload requires Fish and we don't have it, still allow it (it will get null).
        // If an overload has some weird non-optional ref/out param, skip.
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].ParameterType.IsByRef) return null;
        }

        return args;
    }

    // ------------------------------------------------------------------
    // Inventory change "nudge" so other systems listening to InventoryService.OnChanged still tick.
    // ------------------------------------------------------------------
    private void NudgeInventoryChanged(int fishId)
    {
        // 1) Preferred: call InventoryService.Add(id, 0) if it exists.
        // Some implementations still fire OnChanged even for 0.
        try
        {
            var invType = typeof(InventoryService);
            var add = invType.GetMethod("Add", StaticFlags, null, new[] { typeof(int), typeof(int) }, null);
            if (add != null)
            {
                add.Invoke(null, new object[] { fishId, 0 });
                if (logs) Debug.Log($"[CatchToInventory] NudgeInventoryChanged: called InventoryService.Add({fishId}, 0).");
                return;
            }
        }
        catch (Exception ex)
        {
            if (logs) Debug.LogWarning("[CatchToInventory] NudgeInventoryChanged Add(id,0) failed: " + ex.Message, this);
        }

        // 2) Fallback: directly invoke InventoryService.OnChanged delegate via reflection.
        try
        {
            var invType = typeof(InventoryService);

            FieldInfo backing = invType.GetField("OnChanged", StaticFlags);
            if (backing == null)
            {
                var fields = invType.GetFields(StaticFlags);
                foreach (var f in fields)
                {
                    if (!typeof(Delegate).IsAssignableFrom(f.FieldType)) continue;
                    if (f.Name.IndexOf("OnChanged", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        backing = f;
                        break;
                    }
                }
            }

            var del = backing != null ? backing.GetValue(null) as Delegate : null;
            if (del == null)
            {
                if (logs) Debug.LogWarning("[CatchToInventory] NudgeInventoryChanged: could not find InventoryService.OnChanged delegate to invoke.", this);
                return;
            }

            var parms = del.Method.GetParameters();
            object[] args;

            if (parms.Length == 0)
            {
                args = Array.Empty<object>();
            }
            else if (parms.Length == 1)
            {
                args = new object[] { CoerceArg(parms[0].ParameterType, fishId) };
            }
            else if (parms.Length == 2)
            {
                args = new object[]
                {
                    CoerceArg(parms[0].ParameterType, fishId),
                    CoerceArg(parms[1].ParameterType, 0)
                };
            }
            else
            {
                args = new object[parms.Length];
                for (int i = 0; i < parms.Length; i++)
                {
                    args[i] = parms[i].ParameterType.IsValueType ? Activator.CreateInstance(parms[i].ParameterType) : null;
                }
            }

            del.DynamicInvoke(args);
            if (logs) Debug.Log("[CatchToInventory] NudgeInventoryChanged: invoked InventoryService.OnChanged via reflection.");
        }
        catch (Exception ex)
        {
            if (logs) Debug.LogWarning("[CatchToInventory] NudgeInventoryChanged reflection invoke failed: " + ex.Message, this);
        }
    }

    private static object CoerceArg(Type t, int intValue)
    {
        if (t == typeof(int)) return intValue;
        if (t == typeof(float)) return (float)intValue;
        if (t == typeof(double)) return (double)intValue;
        if (t == typeof(long)) return (long)intValue;
        if (t == typeof(short)) return (short)intValue;
        if (t == typeof(byte)) return (byte)Mathf.Clamp(intValue, 0, 255);
        if (t == typeof(uint)) return (uint)Mathf.Max(0, intValue);
        if (t == typeof(bool)) return intValue != 0;

        return t.IsValueType ? Activator.CreateInstance(t) : null;
    }

    // ---------- inventory refresh helpers ----------
    bool IsInventoryVisible()
    {
        if (!inventoryBackgroundGroup) return false;

        return inventoryBackgroundGroup.gameObject.activeInHierarchy &&
               inventoryBackgroundGroup.alpha > visibleThreshold;
    }

    void ForceInventoryGridRefresh()
    {
        if (inventoryGrid == null)
            return;

        inventoryGrid.NextPage();
        inventoryGrid.PrevPage();
    }

    // ---------- runtime stat helpers ----------
    InventoryStatsService.RuntimeStats ReadRuntimeStats(FishIdentity identity, bool strictOnly, out string wSrc, out string lSrc, out string qSrc)
    {
        wSrc = lSrc = qSrc = "none";

        if (!identity)
        {
            return default;
        }

        float? w = null, l = null;
        int? q = null;

        var root = identity.gameObject;
        if (root)
        {
            if (TryGetRuntimeFloat(root,
                    new[] { "FishWeightRuntime", "WeightRuntime" },
                    new[] { "ValueKg", "valueKg", "Value", "value" },
                    out var wVal, out var wHas, out var wFound) && wHas)
            {
                w = wVal;
                wSrc = wFound;
            }

            if (TryGetRuntimeFloat(root,
                    new[] { "FishLengthRuntime", "FishLenghtRuntime", "LengthRuntime", "LenghtRuntime" },
                    new[] { "ValueCm", "valueCm", "Value", "value" },
                    out var lVal, out var lHas, out var lFound) && lHas)
            {
                l = lVal;
                lSrc = lFound;
            }

            if (TryGetRuntimeInt(root,
                    new[] { "FishQualityRuntime", "QualityRuntime" },
                    new[] { "Value", "value" },
                    out var qVal, out var qHas, out var qFound) && qHas)
            {
                q = qVal;
                qSrc = qFound;
            }

            if (!strictOnly && (!w.HasValue || !l.HasValue || !q.HasValue))
            {
                var inst = FindCompByName(root, new[] { "FishInstance" });
                if (inst)
                {
                    if (!w.HasValue && TryReadBool(inst, new[] { "HasWeight", "hasWeight" }, out var hasW) && hasW &&
                        TryReadFloatAny(inst, new[] { "WeightKg", "weightKg" }, out var instW))
                    {
                        w = instW;
                        wSrc = inst.GetType().Name;
                    }

                    if (!l.HasValue && TryReadBool(inst, new[] { "HasLength", "hasLength" }, out var hasL) && hasL &&
                        TryReadFloatAny(inst, new[] { "LengthCm", "lengthCm" }, out var instL))
                    {
                        l = instL;
                        lSrc = inst.GetType().Name;
                    }

                    if (!q.HasValue && TryReadBool(inst, new[] { "HasQuality", "hasQuality" }, out var hasQ) && hasQ &&
                        TryReadIntAny(inst, new[] { "Quality", "quality" }, out var instQ))
                    {
                        q = instQ;
                        qSrc = inst.GetType().Name;
                    }
                }
            }
        }

        return InventoryStatsService.RuntimeStats.From(w, l, q);
    }

    // ---------- helpers (unchanged id resolution) ----------
    int ResolveFishId(FishIdentity identity, FishRegistry reg, out string dbgName, out string dbgSpriteName)
    {
        dbgName = TryGetDisplayName(identity);
        dbgSpriteName = TryGetSprite(identity)?.name;

        var def = TryGetFishDefFromIdentity(identity);
        if (def != null)
        {
            int id = InventoryService.GetId(def);
            if (id >= 0) return id;
        }

        int idByName = ResolveFishIdByName(dbgName, reg);
        if (idByName >= 0) return idByName;

        var spr = TryGetSprite(identity);
        if (spr)
        {
            int idBySprite = ResolveFishIdBySprite(spr, reg);
            if (idBySprite >= 0) return idBySprite;
        }

        return -1;
    }

    Fish TryGetFishDefFromIdentity(object identity)
    {
        if (identity == null) return null;

        var t = identity.GetType();
        foreach (var n in new[] { "Fish", "Definition", "Def", "FishDef", "Data" })
        {
            var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (p != null && typeof(Fish).IsAssignableFrom(p.PropertyType))
                return p.GetValue(identity) as Fish;

            var f = t.GetField(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null && typeof(Fish).IsAssignableFrom(f.FieldType))
                return f.GetValue(identity) as Fish;
        }

        return null;
    }

    int ResolveFishIdByName(string displayName, FishRegistry reg)
    {
        if (reg == null || string.IsNullOrWhiteSpace(displayName)) return -1;

        string target = Norm(displayName);

        for (int i = 0; i < reg.fishes.Count; i++)
        {
            var f = reg.fishes[i];
            var d = string.IsNullOrEmpty(f.displayName) ? f.name : f.displayName;
            if (Norm(d) == target) return i;
        }

        target = Norm(TrimSuffix(displayName));
        for (int i = 0; i < reg.fishes.Count; i++)
        {
            var f = reg.fishes[i];
            var d = string.IsNullOrEmpty(f.displayName) ? f.name : f.displayName;
            if (Norm(TrimSuffix(d)) == target) return i;
        }

        return -1;
    }

    int ResolveFishIdBySprite(Sprite spr, FishRegistry reg)
    {
        if (!spr || reg == null) return -1;

        for (int i = 0; i < reg.fishes.Count; i++)
            if (reg.fishes[i] && reg.fishes[i].sprite == spr) return i;

        string target = spr.name;
        for (int i = 0; i < reg.fishes.Count; i++)
        {
            var s = reg.fishes[i] ? reg.fishes[i].sprite : null;
            if (s && string.Equals(s.name, target, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    string TrimSuffix(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        s = s.Replace(" (Clone)", "", StringComparison.OrdinalIgnoreCase);
        int us = s.LastIndexOf('_');
        if (us > 0)
        {
            var tail = s.Substring(us + 1);
            if (int.TryParse(tail, out _)) return s.Substring(0, us);
        }
        return s;
    }

    string Norm(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var filtered = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
            if (char.IsLetterOrDigit(c)) filtered.Append(char.ToLowerInvariant(c));
        return filtered.ToString();
    }

    string TryGetDisplayName(object identity)
    {
        var t = identity.GetType();
        foreach (var n in new[] { "DisplayName", "displayName", "Name", "name" })
        {
            var p = t.GetProperty(n, MemberFlags);
            if (p != null && p.PropertyType == typeof(string))
            {
                var s = p.GetValue(identity) as string;
                if (!string.IsNullOrEmpty(s)) return s;
            }

            var f = t.GetField(n, MemberFlags);
            if (f != null && f.FieldType == typeof(string))
            {
                var s = f.GetValue(identity) as string;
                if (!string.IsNullOrEmpty(s)) return s;
            }
        }
        return (identity as Component)?.gameObject.name ?? identity.ToString();
    }

    Sprite TryGetSprite(Component identity)
    {
        if (!identity) return null;

        var sr = identity.GetComponentInChildren<SpriteRenderer>();
        if (sr && sr.sprite) return sr.sprite;

        var img = identity.GetComponentInChildren<UnityEngine.UI.Image>();
        if (img && img.sprite) return img.sprite;

        return null;
    }

    static Component FindCompByName(GameObject root, string[] nameContainsAny)
    {
        if (!root || nameContainsAny == null) return null;
        var comps = root.GetComponentsInChildren<Component>(true);

        foreach (var c in comps)
        {
            if (!c) continue;
            var n = c.GetType().Name;
            foreach (var key in nameContainsAny)
            {
                if (string.IsNullOrEmpty(key)) continue;
                if (n.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return c;
            }
        }
        return null;
    }

    bool TryGetRuntimeFloat(GameObject root, string[] typeNames, string[] valueNames, out float value, out bool has, out string source)
    {
        value = 0f;
        has = false;
        source = "none";

        var comp = FindCompByName(root, typeNames);
        if (!comp) return false;

        if (TryReadBool(comp, new[] { "HasValue", "hasValue" }, out var hv))
            has = hv;
        else
            has = true;

        if (TryReadFloatAny(comp, valueNames, out var val))
        {
            value = val;
            source = comp.GetType().Name;
            return true;
        }
        return false;
    }

    bool TryGetRuntimeInt(GameObject root, string[] typeNames, string[] valueNames, out int value, out bool has, out string source)
    {
        value = 0;
        has = false;
        source = "none";

        var comp = FindCompByName(root, typeNames);
        if (!comp) return false;

        if (TryReadBool(comp, new[] { "HasValue", "hasValue" }, out var hv))
            has = hv;
        else
            has = true;

        if (TryReadIntAny(comp, valueNames, out var val))
        {
            value = val;
            source = comp.GetType().Name;
            return true;
        }
        return false;
    }

    // ---------- generic reflection helpers for meta & others ----------

    static bool TryReadFloatAny(object obj, string[] names, out float value)
    {
        value = 0f;
        if (obj == null) return false;

        var result = TryReadFloat(obj.GetType(), obj, names);
        if (result.HasValue)
        {
            value = result.Value;
            return true;
        }
        return false;
    }

    static bool TryReadIntAny(object obj, string[] names, out int value)
    {
        value = 0;
        if (obj == null) return false;

        var result = TryReadInt(obj.GetType(), obj, names);
        if (result.HasValue)
        {
            value = result.Value;
            return true;
        }
        return false;
    }

    static bool TryReadStringAny(object obj, string[] names, out string value)
    {
        value = null;
        if (obj == null || names == null) return false;

        var type = obj.GetType();

        foreach (var name in names)
        {
            if (string.IsNullOrEmpty(name)) continue;

            var field = type.GetField(name, MemberFlags);
            if (field != null && field.FieldType == typeof(string))
            {
                try
                {
                    var raw = field.GetValue(obj) as string;
                    if (!string.IsNullOrEmpty(raw))
                    {
                        value = raw;
                        return true;
                    }
                }
                catch { }
            }

            var prop = type.GetProperty(name, MemberFlags);
            if (prop != null && prop.PropertyType == typeof(string) && prop.CanRead)
            {
                try
                {
                    var raw = prop.GetValue(obj, null) as string;
                    if (!string.IsNullOrEmpty(raw))
                    {
                        value = raw;
                        return true;
                    }
                }
                catch { }
            }
        }

        return false;
    }

    static bool TryReadBool(object obj, string[] names, out bool value)
    {
        value = false;
        if (obj == null) return false;

        var result = TryReadBool(obj.GetType(), obj, names);
        if (result.HasValue)
        {
            value = result.Value;
            return true;
        }
        return false;
    }

    static float? TryReadFloat(Type type, object obj, string[] names)
    {
        float? value = null;
        TryReadFloat(type, obj, names, ref value);
        return value;
    }

    static void TryReadFloat(Type type, object obj, string[] names, ref float? target)
    {
        if (target.HasValue || type == null || obj == null || names == null) return;

        foreach (var name in names)
        {
            if (string.IsNullOrEmpty(name)) continue;

            var field = type.GetField(name, MemberFlags);
            if (field != null && TryConvertFloat(field.GetValue(obj), out var fv))
            {
                target = fv;
                return;
            }

            var prop = type.GetProperty(name, MemberFlags);
            if (prop != null && prop.CanRead && TryConvertFloat(prop.GetValue(obj), out var pv))
            {
                target = pv;
                return;
            }
        }
    }

    static int? TryReadInt(Type type, object obj, string[] names)
    {
        int? value = null;
        TryReadInt(type, obj, names, ref value);
        return value;
    }

    static void TryReadInt(Type type, object obj, string[] names, ref int? target)
    {
        if (target.HasValue || type == null || obj == null || names == null) return;

        foreach (var name in names)
        {
            if (string.IsNullOrEmpty(name)) continue;

            var field = type.GetField(name, MemberFlags);
            if (field != null && TryConvertInt(field.GetValue(obj), out var iv))
            {
                target = iv;
                return;
            }

            var prop = type.GetProperty(name, MemberFlags);
            if (prop != null && prop.CanRead && TryConvertInt(prop.GetValue(obj), out var pv))
            {
                target = pv;
                return;
            }
        }
    }

    static bool? TryReadBool(Type type, object obj, string[] names)
    {
        if (type == null || obj == null || names == null) return null;

        foreach (var name in names)
        {
            if (string.IsNullOrEmpty(name)) continue;

            var field = type.GetField(name, MemberFlags);
            if (field != null && field.FieldType == typeof(bool))
            {
                try { return (bool)field.GetValue(obj); } catch { }
            }

            var prop = type.GetProperty(name, MemberFlags);
            if (prop != null && prop.PropertyType == typeof(bool) && prop.CanRead)
            {
                try { return (bool)prop.GetValue(obj); } catch { }
            }
        }

        return null;
    }

    static bool TryConvertFloat(object value, out float result)
    {
        result = default;
        if (value == null) return false;

        try
        {
            result = Convert.ToSingle(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    static bool TryConvertInt(object value, out int result)
    {
        result = default;
        if (value == null) return false;

        try
        {
            result = Convert.ToInt32(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    static void SafeDespawnCaughtFish(FishIdentity identity)
    {
        if (!identity) return;

        var go = identity.gameObject;
        if (!go) return;

        if (go.GetComponent<FishCaughtFlag>()) return;
        go.AddComponent<FishCaughtFlag>();

        var controller = go.GetComponentInChildren<FishController>();
        if (controller) controller.enabled = false;

        foreach (var col in go.GetComponentsInChildren<Collider2D>(true))
        {
            col.enabled = false;
        }

        UnityEngine.Object.Destroy(go, 0.1f);
    }

    sealed class FishCaughtFlag : MonoBehaviour { }
}
