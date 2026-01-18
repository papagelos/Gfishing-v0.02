using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems; // <-- for IsPointerOverGameObject
using GalacticFishing.UI;       // ReactionBarUI + GreenZoneController
using TMPro;
using UnityEngine.UI;

[System.Serializable] public class FishEvent : UnityEvent<FishIdentity> { }

public class FishingMinigameController : MonoBehaviour
{
    [Header("Assign ReactionBar")]
    [SerializeField] private CanvasGroup reactionBarGroup;
    [SerializeField] private ReactionBarUI reactionBarUI;
    [SerializeField] private GreenZoneController greenZone;

    [Header("Click Source (disabled while game is up)")]
    [SerializeField] private FishClickCaster clickCaster;

    [Header("Prompt UI (optional)")]
    [SerializeField] private TextMeshProUGUI tmpPrompt; // assign one OR the next line
    [SerializeField] private Text uiPrompt;
    [SerializeField] private string hookedMsg = "HOOKED! Wait for the beep… then CLICK!";
    [SerializeField] private string hookedBullseyeMsg = "HOOKED! HIT THE BULLSEYE!";
    [SerializeField] private string bullseyeMissMsg = "Missed the bullseye!";
    [SerializeField] private string tooSoonMsg = "Too soon!";
    [SerializeField] private string tooSlowMsg = "Too slow!";
    [SerializeField] private string successMsg = "GREAT JOB!";
    [SerializeField] private float successMsgSeconds = 1.0f;
    [SerializeField] private float failMsgSeconds = 1.0f;
[SerializeField] private string beepsLeftTemplate = "BEEPS LEFT: {0}";

[Header("Reaction Time UI (optional)")]

[Header("Tutorial (AIStory)")]

[SerializeField] private string tutorialBullseyeId = "Tutorial_Bullseye_Reaction";
private Coroutine _bullseyeRoutine;
private bool _bullseyeArmed = true;



[SerializeField] private string tutorialBeepWaitForBeepId = "Tutorial_Beep_WaitForBeep";

private Coroutine _beepRoutine;

private bool _beepArmed = false;

// Must match AIStoryDirector's prefix (used for "show once" persistence)
private const string AIStorySeenPrefix = "AIStory_Seen_";

[SerializeField] private TMP_Text reactionTimeText;


[Header("Game Message Screen (non-story)")]
[SerializeField] private GameMessagePanel gameMessagePanel;


    [Header("Bullseye")]
    [Tooltip("CanvasGroup on the bullseye UI root (alpha/interactable/blocksRaycasts controlled by this script).")]
    [SerializeField] private CanvasGroup bullseyeGroup;
    [Tooltip("RectTransform of the bullseye target graphic.")]
    [SerializeField] private RectTransform bullseyeRect;
    [Tooltip("Seconds before the bullseye disappears (unscaled time).")]
    [SerializeField] private float bullseyeDuration = 1.25f;
    [Tooltip("Score range is 0..bullseyeMaxPoints. Set this to 100 for the new system.")]
    [SerializeField] private int bullseyeMaxPoints = 100;
    [Tooltip("If true, bullseye position is randomized (using rarity bands if spawn bounds is set).")]
    [SerializeField] private bool bullseyeRandomizePosition = true;
    [Tooltip("Bounds rect used for randomized bullseye position (recommended: a full-screen panel like SafeFrame_16x9).")]
    [SerializeField] private RectTransform bullseyeSpawnBounds;
    [Tooltip("Fallback threshold if the fish doesn't have a BullseyeThreshold field yet. Interpreted as TOTAL threshold across all bullseyes.")]
    [SerializeField] private float bullseyeFallbackThreshold = 0f;
    [Tooltip("1 = linear. Higher = more punishing near edges. ~1.6 matches your example (32 -> ~17).")]
    [SerializeField] private float bullseyeScoreExponent = 1.6f;

    [Header("Bullseye Rain (multi-target, optional)")]
[SerializeField] private BullseyeRainController bullseyeRain;

[SerializeField] private int bullseyeRainTotalTargets = 12; // rain ignores rarity count
[SerializeField] private int bullseyeRainExtraBullets = 2;
[SerializeField] private TMP_Text bullseyeRainBulletsText;
[SerializeField] private string bullseyeRainBulletsTemplate = "BULLETS LEFT: {0}";
[SerializeField] private string bullseyeRainOutOfBulletsMsg = "OUT OF BULLETS!";


[Tooltip("Master enable for the multi-target falling bullseye mode.")]
[SerializeField] private bool bullseyeRainEnabled = true;

[Tooltip("Only use rain if the fish has 2+ bullseye targets.")]
[SerializeField] private bool bullseyeRainRequireAtLeast2Targets = true;

[Tooltip("If true, choose rain at random (chance below). If false, always use rain when allowed.")]
[SerializeField] private bool bullseyeRainUseRandomChance = true;

[Range(0f, 1f)]
[SerializeField] private float bullseyeRainChance = 0.35f;

[Tooltip("Which tiers are allowed to roll rain mode.")]
[SerializeField] private bool rainRare = false;
[SerializeField] private bool rainEpic = true;
[SerializeField] private bool rainLegendary = true;
[SerializeField] private bool rainUberLegendary = true;
[SerializeField] private bool rainOneOfAKind = true;

[Tooltip("UI units/sec (unscaled).")]
[SerializeField] private float bullseyeRainFallSpeed = 650f;

[Tooltip("Seconds between spawns (unscaled).")]
[SerializeField] private float bullseyeRainSpawnInterval = 1.0f;

[Tooltip("How many targets may fall off-screen before failing.")]
[SerializeField] private int bullseyeRainAllowedEscapes = 0;

[SerializeField] private string bullseyeRainEscapeFailMsg = "A target escaped!";


    [Header("Bullseye Status Text (optional)")]
    [Tooltip("Optional TMP text you can position/style freely. Shows total score, targets left, and points needed.")]
    [SerializeField] private TMP_Text bullseyeStatusText;


    [SerializeField] private string bullseyeStatusTemplate = "Current score: {0}. {1} Targets Left. You need {2} more points.";

    [Header("Bullseye Movement (unscaled)")]
    [Tooltip("If true, bullseye can move during AwaitBullseye using the per-rarity profile below.")]
    [SerializeField] private bool bullseyeEnableMovement = true;


    [Header("Bullseye Prepare Prompt (optional)")]
    [SerializeField] private CanvasGroup bullseyePrepareGroup;
    [SerializeField] private TMP_Text bullseyePrepareText;

    // Base prefix line (we will append dynamic info: points + targets)
    [SerializeField] private string bullseyePrepareMsg = "Moving Target Detected. Prepare!";
    [SerializeField] private float bullseyePrepareMinSeconds = 0f;

    // Choose which rarities should show the prepare screen (tick these in Inspector)
    [SerializeField] private bool prepareRare = false;
    [SerializeField] private bool prepareEpic = true;
    [SerializeField] private bool prepareLegendary = true;
    [SerializeField] private bool prepareUberLegendary = true;
    [SerializeField] private bool prepareOneOfAKind = true;

    // runtime
    private float _prepareAllowClickAt = 0f;

    private enum BullseyeMoveMode
    {
        None = 0,
        TopDrop = 1,          // start at top (random X), fall down
        DriftForSeconds = 2,  // start random, drift random direction for N seconds
        Wander = 3,           // start random, keep moving; change dir+speed every interval
        Random = 4            // pick one of the enabled modes below
    }

    [System.Serializable]
    private class BullseyeMovementProfile
    {
        [Tooltip("Movement mode for this rarity. Random picks from enabled options below.")]
        public BullseyeMoveMode mode = BullseyeMoveMode.None;

        [Header("Top Drop")]
        [Tooltip("Downward speed (UI local units per second).")]
        public float topDropSpeed = 650f;
        [Tooltip("If true, it stops at the bottom; if false and bounceAtEdges is true, it can bounce.")]
        public bool topDropStopAtBottom = true;

        [Header("Drift For Seconds")]
        [Tooltip("Speed while drifting (UI local units per second).")]
        public float driftSpeed = 450f;
        [Tooltip("How long to drift before stopping (seconds, unscaled).")]
        public float driftSeconds = 0.9f;

        [Header("Wander")]
        [Tooltip("Min speed for wandering (UI local units per second).")]
        public float wanderMinSpeed = 250f;
        [Tooltip("Max speed for wandering (UI local units per second).")]
        public float wanderMaxSpeed = 650f;
        [Tooltip("How often to change direction AND speed (seconds, unscaled).")]
        public float wanderChangeEverySeconds = 0.35f;

        [Header("Edges")]
        [Tooltip("If true, movement bounces off screen bounds; if false, it clamps and stops or clamps velocity component.")]
        public bool bounceAtEdges = true;

        [Header("Random mode pool")]
        public bool randomAllowTopDrop = true;
        public bool randomAllowDrift = true;
        public bool randomAllowWander = true;
    }

    [Tooltip("Movement profile for Rare fish.")]
    [SerializeField] private BullseyeMovementProfile moveRare = new BullseyeMovementProfile();
    [Tooltip("Movement profile for Epic fish.")]
    [SerializeField] private BullseyeMovementProfile moveEpic = new BullseyeMovementProfile();
    [Tooltip("Movement profile for Legendary fish.")]
    [SerializeField] private BullseyeMovementProfile moveLegendary = new BullseyeMovementProfile();
    [Tooltip("Movement profile for Uber Legendary fish.")]
    [SerializeField] private BullseyeMovementProfile moveUberLegendary = new BullseyeMovementProfile();
    [Tooltip("Movement profile for One Of A Kind fish.")]
    [SerializeField] private BullseyeMovementProfile moveOneOfAKind = new BullseyeMovementProfile();

    [Header("Bullseye Hit Feedback (X + Score)")]
    [Tooltip("CanvasGroup for the hit feedback overlay (X + score). Can be a child of BullseyeTarget.")]
    [SerializeField] private CanvasGroup bullseyeHitGroup;
    [Tooltip("RectTransform of the X marker (child of BullseyeTarget is fine).")]
    [SerializeField] private RectTransform bullseyeHitXRect;
    [Tooltip("TMP text that displays the score above the X.")]
    [SerializeField] private TMP_Text bullseyeHitScoreText;
    [Tooltip("How long the X+score remains visible after a click (unscaled).")]
    [SerializeField] private float bullseyeHitVisibleSeconds = 0.60f;
    [Tooltip("Auto-advance time if player does nothing. NOTE: this no longer blocks manual skip.")]
    [SerializeField] private float bullseyeProceedDelaySeconds = 0.18f;
    [Tooltip("Require that the click is inside the bullseye rect. If false, outside clicks score 0 and can still continue.")]
    [SerializeField] private bool requireClickInsideBullseyeRect = true;

    [Header("Bullseye Difficulty By Rarity (size multipliers)")]
    [SerializeField] private float sizeRare = 1.00f;
    [SerializeField] private float sizeEpic = 0.90f;
    [SerializeField] private float sizeLegendary = 0.80f;
    [SerializeField] private float sizeUberLegendary = 0.70f;
    [SerializeField] private float sizeOneOfAKind = 0.60f;

    [Header("Bullseye Spawn Bands (0..1 of max radius from center)")]
    [SerializeField] private Vector2 bandRare = new Vector2(0.05f, 0.25f);
    [SerializeField] private Vector2 bandEpic = new Vector2(0.20f, 0.45f);
    [SerializeField] private Vector2 bandLegendary = new Vector2(0.35f, 0.75f);
    [SerializeField] private Vector2 bandUberLegendary = new Vector2(0.45f, 0.90f);
    [SerializeField] private Vector2 bandOneOfAKind = new Vector2(0.55f, 0.95f);

    [Header("Beep SFX (optional)")]
    [SerializeField] private AudioSource beepSource;
    [SerializeField] private AudioClip beepClip;

    [Header("Visual Beep: Fish (optional)")]
    [SerializeField] private BeepFishFX beepFishFX;

    [Header("Scare Settings (on miss & fail)")]
    [SerializeField] private LayerMask fishMask = ~0;
    [SerializeField] private float searchRadius = 3f;
    [SerializeField] private GameObject scarePoofPrefab;
    [SerializeField] private float destroyDelay = 0.05f;

    [Header("Events")]
    public FishEvent OnFishHooked;
    public FishEvent OnFishFailed;

    [Header("Rarity Minigame Tuning")]
[SerializeField] private RarityMinigameTuning tuneCommon = new RarityMinigameTuning();
[SerializeField] private RarityMinigameTuning tuneUncommon = new RarityMinigameTuning();
[SerializeField] private RarityMinigameTuning tuneRare = new RarityMinigameTuning();
[SerializeField] private RarityMinigameTuning tuneEpic = new RarityMinigameTuning();
[SerializeField] private RarityMinigameTuning tuneLegendary = new RarityMinigameTuning();
[SerializeField] private RarityMinigameTuning tuneUberLegendary = new RarityMinigameTuning();
[SerializeField] private RarityMinigameTuning tuneOneOfAKind = new RarityMinigameTuning();


[Header("Tutorial (AIStory)")]
[SerializeField] private string tutorialReactionBarId = "Tutorial_ReactionBar_StopInGreen";

private Coroutine _beginRoutine;


    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private bool debugThisFishStats = false;

    [Header("UI Guard (optional)")]
    [Tooltip("Drag the CanvasGroup from 'Inventory-background' here. If present (alpha>0 and interactable/blocksRaycasts), gameplay clicks are ignored.")]
    [SerializeField] private CanvasGroup inventoryCanvasGroup;

   private enum State { Idle, BarShown, AwaitPrepareClick, AwaitBullseye, AwaitBullseyeRain, ResolvingBullseye, AwaitBeep, AwaitWindow }

    private State _state = State.Idle;

    private enum RarityTier { Common, Uncommon, Rare, Epic, Legendary, UberLegendary, OneOfAKind }

private enum MinigameMode
{
    None = 0,
    BeepOnly = 1,
    BullseyeOnly = 2,
    Both = 3
}

[System.Serializable]
private class RarityMinigameTuning
{
    public MinigameMode mode = MinigameMode.None;

    [Header("Beep")]
    [Min(0)] public int beepCount = 0;

    [Tooltip("If enabled, overrides the fish's reaction2.successWindow for this rarity.")]
public bool overrideBeepSuccessWindow = false;

[Min(0.05f)] public float beepSuccessWindowSeconds = 1.2f;

    [Header("Bullseye")]
    [Min(0)] public int bullseyeTargets = 0;

    [Tooltip("If enabled, this overrides the fish's BullseyeThreshold (TOTAL points needed across all targets).")]
    public bool overrideBullseyeThreshold = false;

    [Min(0f)] public float bullseyeThresholdTotal = 0f;
}

    private FishIdentity _current;
    private float _prevTimeScale = 1f;

    // phase-2 timing (unscaled)
    
    private float _beepAt = 0f;
    private float _windowEnd = 0f;
// reaction timing (unscaled)
private float _beepStartTime = 0f;
private float _lastReactionSeconds = -1f; // -1 = none yet
    // bullseye timing (unscaled)
    private float _bullseyeEndTime = 0f;

    // bullseye feedback timing (unscaled)
    private float _hitHideAt = 0f;
// HitFeedback "home" (rain mode temporarily moves the whole group in world space)
private Transform _hitFeedbackHomeParent;
private Vector3 _hitFeedbackHomeLocalPos;
private Quaternion _hitFeedbackHomeLocalRot;
private Vector3 _hitFeedbackHomeLocalScale;
private bool _hitFeedbackHomeCached = false;
    // rounds
    private RarityTier _tier = RarityTier.Common;
    private int _bullseyesRemaining = 0;
    private int _beepsRemaining = 0;
private int _beepsPlannedTotal = 0; // total beeps for this fish when the beep phase begins
private int _rainBulletsLeft = 0;
private bool _bullseyeStartMessageShownThisFish = false;
private bool _useBullseyeRainThisFish = false;

    // NEW: total-score bullseye system
    private int _bullseyeScoreTotal = 0;
    private float _bullseyeThresholdTotal = 0f;

    // input/frame gates
    private int _ignorePressUntilFrame = 0;
    private bool _requireMouseUpToReenable = false;
    private int _reenableCasterAtFrame = 0;

    private Vector2 _bullseyeBaseSize = new Vector2(256, 256);
    private Coroutine _resolveRoutine;

    // HookCard close request (so FlashCaught/FlashEscaped can't re-open it)
    private bool _hookCardCloseRequested = false;

    // Bullseye movement runtime
    private BullseyeMoveMode _activeMoveMode = BullseyeMoveMode.None;
    private Vector2 _moveVelocityLocal = Vector2.zero;
    private float _moveStopAt = 0f;
    private float _moveNextChangeAt = 0f;
    private bool _moveBounce = true;
    private bool _topDropStopAtBottom = true;

    private GameObject BarRootGO => reactionBarGroup ? reactionBarGroup.gameObject : null;

    private static readonly BindingFlags MemberFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private void Awake()
    {
        ClearPrompt();

        if (bullseyeRect)
            _bullseyeBaseSize = bullseyeRect.sizeDelta;

        // Keep the bullseye GO active (we hide via alpha/raycast toggles),
        // so hit feedback can still show even if it is a child.
        if (bullseyeGroup && !bullseyeGroup.gameObject.activeSelf)
            bullseyeGroup.gameObject.SetActive(true);

CacheHitFeedbackHome();
        HideBullseyeImmediate();
        HideHitFeedbackImmediate();

        StopBullseyeMovement();
        StopBullseyeRainImmediate();

        HidePrepareImmediate();
        ShowBullseyeStatus(false);
    }

    // ---------- UI guard helpers ----------
    private bool InventoryLooksOpen()
    {
        if (!inventoryCanvasGroup) return false;
        if (!inventoryCanvasGroup.gameObject.activeInHierarchy) return false;

        return inventoryCanvasGroup.alpha > 0.01f &&
               (inventoryCanvasGroup.interactable || inventoryCanvasGroup.blocksRaycasts);
    }

    private bool PointerOverAnyUI()
    {
        var es = EventSystem.current;
        if (!es) return false;

        if (es.IsPointerOverGameObject()) return true;

        try
        {
            if (Mouse.current != null && es.IsPointerOverGameObject(Mouse.current.deviceId)) return true;
        }
        catch { }

        return false;
    }

    private bool BlockGameplayNow() => InventoryLooksOpen() || PointerOverAnyUI();

    // -------- wired from FishClickCaster --------
   public void HandleFishClicked(FishIdentity fish)
{
    if (_state != State.Idle || BlockGameplayNow()) return;

    StopResolve();
    HideBullseyeImmediate();
    HideHitFeedbackImmediate();
    StopBullseyeMovement();

    _hookCardCloseRequested = false;

    _current = fish;
    _bullseyeStartMessageShownThisFish = false;
    _lastReactionSeconds = -1f;
    UpdateReactionTimeText();
    _beepStartTime = 0f;

    _beepArmed = false;
_beepAt = float.PositiveInfinity;
_windowEnd = float.PositiveInfinity;


    // Disable further fish clicking immediately
    if (clickCaster) clickCaster.enabled = false;

    // Start tutorial+minigame sequence
    if (_beginRoutine != null) StopCoroutine(_beginRoutine);
    _beginRoutine = StartCoroutine(BeginReactionBarWithTutorial());
}


private IEnumerator BeginReactionBarWithTutorial()
{
    // Important: wait 1 frame so the click that started the minigame
    // cannot instantly close the tutorial popup.
    yield return null;

    // --- ACTUAL TUTORIAL CALL (the exact ID you named in AIStoryBook) ---
    if (AIStoryDirector.Instance != null)
        AIStoryDirector.Instance.Trigger("Tutorial_ReactionBar_StopInGreen");
    // -------------------------------------------------------------------

    // Wait until the popup is closed (requires AIStoryDirector.IsOpen property)
    while (AIStoryDirector.Instance != null && AIStoryDirector.Instance.IsOpen)
        yield return null;

    // Safety: don’t start if state changed while waiting
    if (_state != State.Idle || _current == null) yield break;

    // Now start the minigame
    PauseWorld();

    ShowReactionBar();
    _ignorePressUntilFrame = Time.frameCount + 1;
    _state = State.BarShown;

    if (debugLogs) Debug.Log("[Mini] Start: bar shown (after tutorial closed)");
}
private IEnumerator BeginMinigameAfterStory()
{
    // Wait while any story popup is visible.
    // This prevents the "close popup" click from also interacting with the minigame.
    while (AIStoryDirector.Instance != null && AIStoryDirector.Instance.IsOpen)
        yield return null;

    // Safety: if something changed while we waited, don’t start.
    if (_state != State.Idle || _current == null) yield break;

    PauseWorld();

    ShowReactionBar();
    _ignorePressUntilFrame = Time.frameCount + 1;
    _state = State.BarShown;

    if (debugLogs) Debug.Log("[Mini] Start: bar shown (after story popup closed)");
}

    public void HandleMissClicked(Vector2 where)
    {
        if (_state != State.Idle || BlockGameplayNow()) return;

        var nearest = FindNearestFish(where, searchRadius);
        if (nearest) ScareAndDespawn(nearest);
    }

    private void Update()
    {
        if (_state == State.Idle && BlockGameplayNow()) return;

        // If a story popup is open, it owns input. Don't let the minigame react to clicks/keys.
if (AIStoryDirector.Instance != null && AIStoryDirector.Instance.IsOpen) return;

if (gameMessagePanel != null && gameMessagePanel.IsOpen) return;
        UpdateHitFeedback();
        UpdateBullseyeMovement();

        var mouse = Mouse.current;
        var kb = Keyboard.current;

        bool mousePress = (mouse != null && mouse.leftButton.wasPressedThisFrame);
        bool press = mousePress || (kb != null && kb.spaceKey.wasPressedThisFrame);

        switch (_state)
        {
            case State.BarShown:
                if (press && Time.frameCount >= _ignorePressUntilFrame)
                {
                    bool ok = IsMarkerInsideGreenZone();
                    if (!ok)
                    {
                        FailAndCleanup("missed green", tooSoonMsg);
                        return;
                    }

                    HideReactionBar();

                    HookCardService.ShowInProgress(_current ? _current.DisplayName : string.Empty);
                    PushThisFishStatsToHookCard();

                    if (beepFishFX && _current)
                    {
                        var sr0 = _current.GetComponentInChildren<SpriteRenderer>();
                        if (sr0 && sr0.sprite)
                            beepFishFX.Show(sr0.sprite, 999f);
                    }

                    PlanRoundsForCurrentFish();

                    if (_bullseyesRemaining > 0)
                    {
                        ShowPrompt(hookedBullseyeMsg);

                        if (ShouldShowPrepareForTier())
                        {
                            ShowPrepare(BuildBullseyePrepareMessage());
                            _ignorePressUntilFrame = Time.frameCount + 1;
                            _state = State.AwaitPrepareClick;
                        }
                        else
                        {
                            BeginBullseyePhase();
                        }
                    }
                   else if (_beepsRemaining > 0)
{
    BeginBeepPhase();
}

                    else
                    {
                        SuccessCatchNow();
                    }

                    if (debugLogs)
                        Debug.Log($"[Mini] Phase1 success → bullseyes={_bullseyesRemaining}, beeps={_beepsRemaining}, tier={_tier}");
                }
                break;

            case State.AwaitPrepareClick:
                if (press && Time.unscaledTime >= _prepareAllowClickAt && Time.frameCount >= _ignorePressUntilFrame)
                {
                    HidePrepareImmediate();
                    ShowPrompt(hookedBullseyeMsg);
                    BeginBullseyePhase();
                }
                break;

            case State.AwaitBullseye:
            if (!_bullseyeArmed) break;

                if (Time.unscaledTime > _bullseyeEndTime)
                {
                    // NEW RULE: timeout = miss = 0 points, continue to next target (no instant fail)
                    if (debugLogs) Debug.Log("[Mini] Bullseye timeout -> 0 points");
                    StopBullseyeMovement();

                    _bullseyesRemaining = Mathf.Max(0, _bullseyesRemaining - 1);
                    UpdateBullseyeStatusText();

                    if (_bullseyesRemaining > 0)
                    {
                        BeginBullseyePhase();
                    }
                    else
                    {
                        EndBullseyePhaseAndRoute();
                    }
                    return;
                }

                if (mousePress && Time.frameCount >= _ignorePressUntilFrame)
                {
                    
                   
                    StopBullseyeMovement();

                    Vector2 screen = mouse.position.ReadValue();

                    // Always try to get a local point (works even if click is outside the rect).
                    bool gotLocal = TryGetBullseyeLocalPoint(screen, out var local);

                    bool inside = IsScreenPointInsideBullseye(screen);

                    int score = 0;
                    if (gotLocal && (!requireClickInsideBullseyeRect || inside))
                    {
                        score = ComputeBullseyeScoreFromLocal(local);
                    }
                    else
                    {
                        score = 0;
                    }

                    // NEW: total-score system
                    _bullseyeScoreTotal += score;
                    _bullseyesRemaining = Mathf.Max(0, _bullseyesRemaining - 1);
                    UpdateBullseyeStatusText();

                    if (debugLogs)
                        Debug.Log($"[Mini] Bullseye attempt score={score}/{bullseyeMaxPoints}, total={_bullseyeScoreTotal}, remaining={_bullseyesRemaining}, thresholdTotal={_bullseyeThresholdTotal:0.#}");

                    // Show the bullet-hole/X at the actual click point (even if miss/outside)
                    if (gotLocal)
                        ShowHitFeedback(local, score, true);
                    else
                        ShowHitFeedback(Vector2.zero, score, false);

                    _state = State.ResolvingBullseye;
                    StartResolve(ResolveBullseyeAfterClick());
                    return;
                }
                break;


case State.AwaitBullseyeRain:
{
    if (!_bullseyeArmed) break;
    if (bullseyeRain == null || !bullseyeRain.IsRunning) break;

    // Keep your status text updated even if targets escape without clicks.
    int leftNow = bullseyeRain.TargetsLeft;
    if (_bullseyesRemaining != leftNow)
    {
        _bullseyesRemaining = leftNow;
        UpdateBullseyeStatusText();
    }

    if (mousePress && Time.frameCount >= _ignorePressUntilFrame)
{
    // ----- BULLETS: spend 1 bullet per shot -----
    if (_rainBulletsLeft <= 0)
    {
        FailAndCleanup("bullseye rain: out of bullets", bullseyeRainOutOfBulletsMsg);
        return;
    }

    _rainBulletsLeft--;
    UpdateBullseyeRainBulletsText();
    // -------------------------------------------

    Vector2 screen = mouse.position.ReadValue();

    if (bullseyeRain.TryHit(screen, out var hitRt, out var localInTarget))
    {
        int score = ComputeBullseyeScoreFromRectLocal(hitRt, localInTarget);

        _bullseyeScoreTotal += score;

        _bullseyesRemaining = bullseyeRain.TargetsLeft;
        UpdateBullseyeStatusText();

        // Non-blocking feedback (no “resolve / wait for click” here, because it’s rapid-fire).
        ShowHitFeedbackAtScreen(screen, score);
    }

    // If we just fired our last bullet and there are still targets left to resolve, fail now.
    if (_rainBulletsLeft <= 0 && bullseyeRain.TargetsLeft > 0)
    {
        FailAndCleanup("bullseye rain: out of bullets", bullseyeRainOutOfBulletsMsg);
        return;
    }
}


    break;
}



            case State.ResolvingBullseye:
                // coroutine drives progression (and can be skipped)
                break;

            case State.AwaitBeep:
            if (!_beepArmed) break;
                if (press && Time.frameCount >= _ignorePressUntilFrame)
                {
                   
                    FailAndCleanup("clicked before beep", tooSoonMsg);
                    return;
                }

                if (Time.unscaledTime >= _beepAt)
                {
                    if (beepSource && beepClip) beepSource.PlayOneShot(beepClip);
                    _beepStartTime = Time.unscaledTime;
                    _ignorePressUntilFrame = Time.frameCount + 1;
                    _state = State.AwaitWindow;

                    if (debugLogs) Debug.Log("[Mini] Beep!");
                }
                break;

            case State.AwaitWindow:
            if (!_beepArmed) break;
    if (Time.unscaledTime > _windowEnd)
    {
        FailAndCleanup("too slow after beep", tooSlowMsg);
        return;
    }

    if (press && Time.frameCount >= _ignorePressUntilFrame)
    {
        
        _lastReactionSeconds = Mathf.Max(0f, Time.unscaledTime - _beepStartTime);

        UpdateReactionTimeText();

        _beepsRemaining = Mathf.Max(0, _beepsRemaining - 1);

        if (debugLogs) Debug.Log($"[Mini] Beep hit! Remaining beeps = {_beepsRemaining}");

        if (_beepsRemaining > 0)
        {
            BeginBeepPhase();
            return;
        }

        SuccessCatchNow();
        return;
    }
    break;

            case State.Idle:
                if (_requireMouseUpToReenable &&
                    mouse != null && !mouse.leftButton.isPressed &&
                    Time.frameCount >= _reenableCasterAtFrame)
                {
                    if (clickCaster) clickCaster.enabled = true;
                    _requireMouseUpToReenable = false;

                    if (debugLogs) Debug.Log("[Mini] Click caster re-enabled");
                }
                break;
        }
    }

    private void RequestCloseHookCard()
    {
        _hookCardCloseRequested = true;
        HookCardService.Hide();
    }

    // -------------------- Rounds Planning --------------------

    private void PlanRoundsForCurrentFish()
    {
        _tier = GetCurrentFishRarityTier();

     var tune = GetTuningForTier(_tier);

_bullseyesRemaining = 0;
_beepsRemaining = 0;

if (tune != null)
{
    switch (tune.mode)
    {
        case MinigameMode.BeepOnly:
            _beepsRemaining = Mathf.Max(0, tune.beepCount);
            break;

        case MinigameMode.BullseyeOnly:
            _bullseyesRemaining = Mathf.Max(0, tune.bullseyeTargets);
            break;

        case MinigameMode.Both:
            _bullseyesRemaining = Mathf.Max(0, tune.bullseyeTargets);
            _beepsRemaining = Mathf.Max(0, tune.beepCount);
            break;

        case MinigameMode.None:
        default:
            break;
    }
}


        int overrideBull = TryGetIntFromCurrentFish(new[]
        {
            "BullseyeTargets", "bullseyeTargets", "BullseyeTargetCount", "bullseyeTargetCount"
        });

        int overrideBeep = TryGetIntFromCurrentFish(new[]
        {
            "BeepCount", "beepCount", "Beeps", "beeps"
        });

        if (overrideBull > 0) _bullseyesRemaining = overrideBull;
        if (overrideBeep > 0) _beepsRemaining = overrideBeep;

        // ✅ RULE: either bullseye OR beep for everything EXCEPT OneOfAKind
if (_tier != RarityTier.OneOfAKind)
{
    if (_bullseyesRemaining > 0)
        _beepsRemaining = 0;
    else
        _bullseyesRemaining = 0;
}

        if (_beepsRemaining < 0) _beepsRemaining = 0;
_beepsPlannedTotal = _beepsRemaining;
       

        // NEW: reset total bullseye scoring
        _bullseyeScoreTotal = 0;
        if (_bullseyesRemaining > 0)
{
    _bullseyeThresholdTotal = GetCurrentFishBullseyeThresholdTotal();

    
    if (tune != null && tune.overrideBullseyeThreshold)
        _bullseyeThresholdTotal = tune.bullseyeThresholdTotal;
}
else
{
    _bullseyeThresholdTotal = 0f;
}


        UpdateBullseyeStatusText();

        if (debugLogs)
        {
            Debug.Log($"[Mini] PlanRounds: tier={_tier}, bullseyes={_bullseyesRemaining}, beeps={_beepsRemaining}, thresholdTotal={_bullseyeThresholdTotal:0.#}");
        }
    }

    private int GetBullseyeCountForTier(RarityTier t) => t switch
{
    // Epic+ => bullseye only
    RarityTier.Epic => 1,
    RarityTier.Legendary => 2,
    RarityTier.UberLegendary => 3,

    // One Of A Kind => mixture (bullseye + beep)
    RarityTier.OneOfAKind => 10,

    _ => 0
};

private int GetBeepCountForTier(RarityTier t) => t switch
{
    // Common/Uncommon/Rare => sound only
    RarityTier.Common => 1,
    RarityTier.Uncommon => 1,
    RarityTier.Rare => 1,

    // One Of A Kind => mixture (default to 1 beep; override per fish if you want more)
    RarityTier.OneOfAKind => 1,

    _ => 0
};

private RarityMinigameTuning GetTuningForTier(RarityTier t) => t switch
{
    RarityTier.Common => tuneCommon,
    RarityTier.Uncommon => tuneUncommon,
    RarityTier.Rare => tuneRare,
    RarityTier.Epic => tuneEpic,
    RarityTier.Legendary => tuneLegendary,
    RarityTier.UberLegendary => tuneUberLegendary,
    RarityTier.OneOfAKind => tuneOneOfAKind,
    _ => tuneCommon
};



    private RarityTier GetCurrentFishRarityTier()
    {
        // ✅ Source of truth: ScriptableObject meta rarity
        if (_current != null && _current.meta != null)
            return MapRarityToTier(_current.meta.rarity);

        string raw = TryGetRarityStringFromCurrentFish();
        if (string.IsNullOrEmpty(raw)) return RarityTier.Common;

        string n = raw.ToLowerInvariant()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "");

        if (n.Contains("oneofakind")) return RarityTier.OneOfAKind;
        if (n.Contains("uberlegendary") || (n.Contains("uber") && n.Contains("legendary"))) return RarityTier.UberLegendary;
        if (n.Contains("legendary")) return RarityTier.Legendary;
        if (n.Contains("epic")) return RarityTier.Epic;
        if (n.Contains("rare")) return RarityTier.Rare;
        if (n.Contains("uncommon")) return RarityTier.Uncommon;
        return RarityTier.Common;
    }

    private static RarityTier MapRarityToTier(FishRarity r) => r switch
    {
        FishRarity.Common => RarityTier.Common,
        FishRarity.Uncommon => RarityTier.Uncommon,
        FishRarity.Rare => RarityTier.Rare,
        FishRarity.Epic => RarityTier.Epic,
        FishRarity.Legendary => RarityTier.Legendary,
        FishRarity.UberLegendary => RarityTier.UberLegendary,
        FishRarity.OneOfAKind => RarityTier.OneOfAKind,
        _ => RarityTier.Common
    };

    private string TryGetRarityStringFromCurrentFish()
    {
        if (!_current) return null;

        var t = _current.GetType();
        var names = new[] { "Rarity", "rarity", "FishRarity", "fishRarity", "Tier", "tier" };

        foreach (var n in names)
        {
            var f = t.GetField(n, MemberFlags);
            if (f != null)
            {
                var v = f.GetValue(_current);
                return v != null ? v.ToString() : null;
            }

            var p = t.GetProperty(n, MemberFlags);
            if (p != null && p.CanRead)
            {
                var v = p.GetValue(_current);
                return v != null ? v.ToString() : null;
            }
        }

        return null;
    }

    private int TryGetIntFromCurrentFish(string[] names)
    {
        if (!_current || names == null) return 0;

        var t = _current.GetType();
        foreach (var n in names)
        {
            var f = t.GetField(n, MemberFlags);
            if (f != null)
            {
                try { return System.Convert.ToInt32(f.GetValue(_current)); } catch { }
            }

            var p = t.GetProperty(n, MemberFlags);
            if (p != null && p.CanRead)
            {
                try { return System.Convert.ToInt32(p.GetValue(_current)); } catch { }
            }
        }
        return 0;
    }


private void ShowBullseyeStartMessageThenContinue()
{
    Debug.Log("[Mini] ShowBullseyeStartMessageThenContinue CALLED");

    if (gameMessagePanel == null)
        gameMessagePanel = GameMessagePanel.Instance;

    // If panel missing, or we already showed the "start" message for this fish,
    // we MUST still continue the minigame (spawn the next target).
    if (gameMessagePanel == null || _bullseyeStartMessageShownThisFish)
    {
        _bullseyeStartMessageShownThisFish = true;
        _ignorePressUntilFrame = Time.frameCount + 2;
        BeginBullseyePhase_Internal();
        return;
    }

    _bullseyeStartMessageShownThisFish = true;

    bool useRain = _useBullseyeRainThisFish;

    int targets = useRain
        ? Mathf.Max(0, bullseyeRainTotalTargets)
        : Mathf.Max(0, _bullseyesRemaining);

    int bullets = useRain
        ? (targets + Mathf.Max(0, bullseyeRainExtraBullets))
        : targets;

    // IMPORTANT:
    // - Rain mode has NO score requirement.
    // - Normal bullseye mode keeps the score requirement line.
    int scoreRequired = Mathf.CeilToInt(Mathf.Max(0f, _bullseyeThresholdTotal));

    string msg = useRain
        ? $"HIT THE BULLSEYE!\nTargets: {targets}\nBullets: {bullets}\n\n(Click or press any key to start)"
        : $"HIT THE BULLSEYE!\nTargets: {targets}\nBullets: {bullets}\nScore Required: {scoreRequired}\n\n(Click or press any key to start)";

    gameMessagePanel.Show(msg, () =>
    {
        _ignorePressUntilFrame = Time.frameCount + 2;
        BeginBullseyePhase_Internal();
    });
}



private void BeginBullseyePhase()
{
    TopTaskbarController.SetHoverRevealSuppressed(true);

    // IMPORTANT: leave BarShown immediately so Phase1 can't re-run.
    _state = State.AwaitBullseye;

    // Don't allow bullseye logic/timers to run until we explicitly arm it.
    _bullseyeArmed = false;
    _bullseyeEndTime = float.PositiveInfinity;
    _ignorePressUntilFrame = Time.frameCount + 2;

    // Keep bullseye hidden until tutorial (if any) is done.
    HideBullseyeImmediate();
    HideHitFeedbackImmediate();
    StopBullseyeMovement();

    if (_bullseyeRoutine != null) StopCoroutine(_bullseyeRoutine);
    _bullseyeRoutine = StartCoroutine(BeginBullseyePhase_WithTutorial());
}

private IEnumerator BeginBullseyePhase_WithTutorial()
{
    bool shouldShow =
        !string.IsNullOrWhiteSpace(tutorialBullseyeId) &&
        PlayerPrefs.GetInt(AIStorySeenPrefix + tutorialBullseyeId, 0) == 0 &&
        AIStoryDirector.Instance != null;

    if (shouldShow)
    {
        // Wait 1 frame so the click that advanced from green-zone
        // can't instantly close the tutorial.
        yield return null;

        AIStoryDirector.Instance.Trigger(tutorialBullseyeId);

        // Wait while the popup is visible.
        while (AIStoryDirector.Instance != null && AIStoryDirector.Instance.IsOpen)
            yield return null;

        // Eat the close-click frame.
        yield return null;
    }

    // Start the REAL bullseye phase now (timers computed after tutorial).
    _bullseyeArmed = true;
    _useBullseyeRainThisFish = ShouldUseBullseyeRainNow();
ShowBullseyeStartMessageThenContinue();

    _bullseyeRoutine = null;
}



    // -------------------- Bullseye Phase --------------------

   private void BeginBullseyePhase_Internal()
    {
        HideHitFeedbackImmediate();
        StopBullseyeMovement();

        if (!bullseyeGroup || !bullseyeRect)
        {
            if (_beepsRemaining > 0)
            {
                ShowPrompt(hookedMsg);
                BeginBeepPhase();
            }
            else
            {
                SuccessCatchNow();
            }
            TopTaskbarController.SetHoverRevealSuppressed(false);
            return;
        }

        ShowBullseyeStatus(true);
        UpdateBullseyeStatusText();

        // OPTIONAL: switch to multi-target falling bullseyes (rain mode)
// OPTIONAL: switch to multi-target falling bullseyes (rain mode)
// Use the cached decision for THIS fish (do not re-roll mid-run).
if (_useBullseyeRainThisFish)
{
    BeginBullseyeRain_Internal();
    return;
}


        ApplyBullseyeSizeForTier();

        var profile = GetMovementProfileForTier(_tier);
        BullseyeMoveMode chosenMode = ChooseMoveMode(profile);

        if (bullseyeRandomizePosition)
        {
            if (bullseyeEnableMovement && chosenMode == BullseyeMoveMode.TopDrop)
                PlaceBullseyeAtTopRandomX();
            else
                RandomizeBullseyePositionByTier();
        }

        ConfigureAndStartBullseyeMovement(profile, chosenMode);

        bullseyeGroup.alpha = 1f;
        bullseyeGroup.interactable = true;
        bullseyeGroup.blocksRaycasts = true;

        _bullseyeEndTime = Time.unscaledTime + Mathf.Max(0.05f, bullseyeDuration);
        _ignorePressUntilFrame = Time.frameCount + 1;
        _state = State.AwaitBullseye;
    }

    private void ApplyBullseyeSizeForTier()
    {
        if (!bullseyeRect) return;

        float mul = _tier switch
        {
            RarityTier.Rare => sizeRare,
            RarityTier.Epic => sizeEpic,
            RarityTier.Legendary => sizeLegendary,
            RarityTier.UberLegendary => sizeUberLegendary,
            RarityTier.OneOfAKind => sizeOneOfAKind,
            _ => 1f
        };

        bullseyeRect.sizeDelta = _bullseyeBaseSize * Mathf.Max(0.1f, mul);
    }

    private Vector2 GetBandForTier01()
    {
        return _tier switch
        {
            RarityTier.Rare => bandRare,
            RarityTier.Epic => bandEpic,
            RarityTier.Legendary => bandLegendary,
            RarityTier.UberLegendary => bandUberLegendary,
            RarityTier.OneOfAKind => bandOneOfAKind,
            _ => new Vector2(0f, 0f)
        };
    }

    private void RandomizeBullseyePositionByTier()
    {
        if (!bullseyeRect) return;
        if (!bullseyeSpawnBounds) return;

        Rect b = bullseyeSpawnBounds.rect;

        Vector2 bullSize = bullseyeRect.rect.size;

        Vector3 bullWorldScale = bullseyeRect.lossyScale;
        Vector3 boundsWorldScale = bullseyeSpawnBounds.lossyScale;

        float sx = (Mathf.Abs(boundsWorldScale.x) > 0.0001f) ? (bullWorldScale.x / boundsWorldScale.x) : bullWorldScale.x;
        float sy = (Mathf.Abs(boundsWorldScale.y) > 0.0001f) ? (bullWorldScale.y / boundsWorldScale.y) : bullWorldScale.y;

        float halfW = bullSize.x * Mathf.Abs(sx) * 0.5f;
        float halfH = bullSize.y * Mathf.Abs(sy) * 0.5f;

        float availHalfW = (b.width * 0.5f) - halfW;
        float availHalfH = (b.height * 0.5f) - halfH;

        if (availHalfW <= 5f || availHalfH <= 5f) return;

        Vector2 band = GetBandForTier01();
        float rMin = Mathf.Clamp01(band.x);
        float rMax = Mathf.Clamp01(band.y);
        if (rMax < rMin) { float tmp = rMin; rMin = rMax; rMax = tmp; }

        float r = Mathf.Sqrt(Random.Range(rMin * rMin, rMax * rMax));
        float a = Random.Range(0f, Mathf.PI * 2f);

        float x = Mathf.Cos(a) * r * availHalfW;
        float y = Mathf.Sin(a) * r * availHalfH;

        Vector3 local = new Vector3(b.center.x + x, b.center.y + y, 0f);
        Vector3 world = bullseyeSpawnBounds.TransformPoint(local);
        world.z = bullseyeRect.position.z;

        bullseyeRect.position = world;
    }

    private bool ShouldShowPrepareForTier()
    {
        return _tier switch
        {
            RarityTier.Rare => prepareRare,
            RarityTier.Epic => prepareEpic,
            RarityTier.Legendary => prepareLegendary,
            RarityTier.UberLegendary => prepareUberLegendary,
            RarityTier.OneOfAKind => prepareOneOfAKind,
            _ => false
        };
    }

    private string BuildBullseyePrepareMessage()
    {
        int pointsNeeded = Mathf.CeilToInt(_bullseyeThresholdTotal);

        string msg = $"{bullseyePrepareMsg}\nYou need to score {pointsNeeded}p";
        if (_bullseyesRemaining > 1)
            msg += $", over {_bullseyesRemaining} targets.";

        return msg;
    }

    private void ShowPrepare(string msg)
    {
        if (!bullseyePrepareGroup) return;

        bullseyePrepareGroup.gameObject.SetActive(true);
        bullseyePrepareGroup.alpha = 1f;
        bullseyePrepareGroup.interactable = false;
        bullseyePrepareGroup.blocksRaycasts = false;

        if (bullseyePrepareText) bullseyePrepareText.text = msg;

        _prepareAllowClickAt = Time.unscaledTime + Mathf.Max(0f, bullseyePrepareMinSeconds);
    }

    private void HidePrepareImmediate()
    {
        if (!bullseyePrepareGroup) return;
        bullseyePrepareGroup.alpha = 0f;
        bullseyePrepareGroup.gameObject.SetActive(false);
    }

    private void HideBullseyeImmediate()
{
    StopBullseyeRainImmediate();

    if (!bullseyeGroup) return;

    bullseyeGroup.alpha = 0f;
    bullseyeGroup.interactable = false;
    bullseyeGroup.blocksRaycasts = false;

    StopBullseyeMovement();
    ShowBullseyeStatus(false);
}

    private bool IsScreenPointInsideBullseye(Vector2 screenPos)
    {
        if (!bullseyeRect) return false;

        Camera cam = null;
        var canvas = bullseyeRect.GetComponentInParent<Canvas>();
        if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(bullseyeRect, screenPos, cam);
    }

    private bool TryGetBullseyeLocalPoint(Vector2 screenPos, out Vector2 local)
    {
        local = default;
        if (!bullseyeRect) return false;

        Camera cam = null;
        var canvas = bullseyeRect.GetComponentInParent<Canvas>();
        if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(bullseyeRect, screenPos, cam, out local);
    }

   private int ComputeBullseyeScoreFromLocal(Vector2 local)
{
    return ComputeBullseyeScoreFromRectLocal(bullseyeRect, local);
}

private int ComputeBullseyeScoreFromRectLocal(RectTransform rt, Vector2 local)
{
    if (!rt) return 0;

    float radius = Mathf.Min(rt.rect.width, rt.rect.height) * 0.5f;
    if (radius <= 1f) return 0;

    Vector2 center = rt.rect.center;
    float dist = Vector2.Distance(local, center);

    float t = Mathf.Clamp01(1f - (dist / radius));
    t = Mathf.Pow(t, Mathf.Max(0.01f, bullseyeScoreExponent));

    int score = Mathf.RoundToInt(t * bullseyeMaxPoints);
    return Mathf.Clamp(score, 0, bullseyeMaxPoints);
}


    // ✅ FIXED: skip logic no longer blocked by ProceedDelaySeconds
    private IEnumerator ResolveBullseyeAfterClick()
    {
        if (bullseyeGroup)
        {
            bullseyeGroup.interactable = false;
            bullseyeGroup.blocksRaycasts = false;
        }

        // Tiny minimum so the hit-click cannot instantly dismiss in the same moment
        const float minSkipSeconds = 0.12f;

        // Auto-advance timing (player can always skip earlier)
        float autoHold = Mathf.Max(minSkipSeconds, bullseyeProceedDelaySeconds);


        _ignorePressUntilFrame = Time.frameCount + 1;

        float start = Time.unscaledTime;

        // 1 frame safety
        yield return null;

        while (true)
        {
            bool timedOut = Time.unscaledTime >= (start + autoHold);
            bool canSkip = Time.unscaledTime >= (start + minSkipSeconds);

            if (!timedOut && canSkip && Time.frameCount >= _ignorePressUntilFrame)
            {
                if (AnyDismissPressedThisFrame())
                    break;
            }

            if (timedOut) break;
            yield return null;
        }

        HideHitFeedbackImmediate();

        if (_bullseyesRemaining > 0)
        {
            BeginBullseyePhase();
            yield break;
        }

        EndBullseyePhaseAndRoute();
    }

    private bool AnyDismissPressedThisFrame()
    {
        var mouse = Mouse.current;
        var kb = Keyboard.current;

        bool mouseAny =
            mouse != null &&
            (mouse.leftButton.wasPressedThisFrame ||
             mouse.rightButton.wasPressedThisFrame ||
             mouse.middleButton.wasPressedThisFrame ||
             (mouse.backButton != null && mouse.backButton.wasPressedThisFrame) ||
             (mouse.forwardButton != null && mouse.forwardButton.wasPressedThisFrame));

        bool keyAny =
            kb != null && kb.anyKey != null && kb.anyKey.wasPressedThisFrame;

        return mouseAny || keyAny;
    }

    private void EndBullseyePhaseAndRoute()
    {
        TopTaskbarController.SetHoverRevealSuppressed(false);

               HideBullseyeImmediate();

        bool enough = _bullseyeScoreTotal >= (_bullseyeThresholdTotal - 0.0001f);

        if (!enough)
        {
            FailAndCleanup("bullseye total too low", bullseyeMissMsg);
            return;
        }

        if (_beepsRemaining > 0)
{
    BeginBeepPhase();
    return;
}


        SuccessCatchNow();
    }

    // -------------------- Hit Feedback --------------------

private void CacheHitFeedbackHome()
{
    if (_hitFeedbackHomeCached) return;
    if (!bullseyeHitGroup) return;

    var t = bullseyeHitGroup.transform;
    _hitFeedbackHomeParent = t.parent;
    _hitFeedbackHomeLocalPos = t.localPosition;
    _hitFeedbackHomeLocalRot = t.localRotation;
    _hitFeedbackHomeLocalScale = t.localScale;
    _hitFeedbackHomeCached = true;
}

private void RestoreHitFeedbackHome()
{
    if (!_hitFeedbackHomeCached) return;
    if (!bullseyeHitGroup) return;

    var t = bullseyeHitGroup.transform;

    if (_hitFeedbackHomeParent && t.parent != _hitFeedbackHomeParent)
        t.SetParent(_hitFeedbackHomeParent, worldPositionStays: false);

    t.localPosition = _hitFeedbackHomeLocalPos;
    t.localRotation = _hitFeedbackHomeLocalRot;
    t.localScale = _hitFeedbackHomeLocalScale;
}

    private void ShowHitFeedback(Vector2 localPointInBullseye, int score, bool validLocal)
    {

CacheHitFeedbackHome();
RestoreHitFeedbackHome();

        if (!bullseyeHitGroup || !bullseyeHitXRect || !bullseyeHitScoreText) return;

        bullseyeHitGroup.ignoreParentGroups = true;

        bullseyeHitGroup.gameObject.SetActive(true);
        bullseyeHitGroup.alpha = 1f;

        if (validLocal)
            bullseyeHitXRect.anchoredPosition = localPointInBullseye;

        bullseyeHitScoreText.text = score.ToString();

        _hitHideAt = Time.unscaledTime + Mathf.Max(0.05f, bullseyeHitVisibleSeconds);
       
    }



private void ShowHitFeedbackAtScreen(Vector2 screenPos, int score)
{
    CacheHitFeedbackHome();
RestoreHitFeedbackHome();
    if (!bullseyeHitGroup || !bullseyeHitScoreText) return;

    bullseyeHitGroup.ignoreParentGroups = true;
    bullseyeHitGroup.gameObject.SetActive(true);
    bullseyeHitGroup.alpha = 1f;

    // Place the WHOLE hit-feedback group at the click point (works regardless of its hierarchy).
    RectTransform refRect = bullseyeSpawnBounds ? bullseyeSpawnBounds : (bullseyeRect ? bullseyeRect : null);
    if (refRect)
    {
        Camera cam = null;
        var canvas = refRect.GetComponentInParent<Canvas>();
        if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(refRect, screenPos, cam, out var world))
            bullseyeHitGroup.transform.position = world;
    }

    if (bullseyeHitXRect) bullseyeHitXRect.anchoredPosition = Vector2.zero;
    bullseyeHitScoreText.text = score.ToString();

    _hitHideAt = Time.unscaledTime + Mathf.Max(0.05f, bullseyeHitVisibleSeconds);
}



    private void UpdateHitFeedback()
    {
        if (_state == State.ResolvingBullseye) return;
        if (!bullseyeHitGroup || !bullseyeHitGroup.gameObject.activeSelf) return;

        if (Time.unscaledTime >= _hitHideAt)
            HideHitFeedbackImmediate();
    }

    private void HideHitFeedbackImmediate()
{
    if (!bullseyeHitGroup) return;
    bullseyeHitGroup.alpha = 0f;
    bullseyeHitGroup.gameObject.SetActive(false);

    // IMPORTANT: rain mode moves the whole group in world-space,
    // so we must put it back for normal bullseye mode.
    RestoreHitFeedbackHome();
}

    // -------------------- Bullseye Movement --------------------

    private BullseyeMovementProfile GetMovementProfileForTier(RarityTier t) => t switch
    {
        RarityTier.Rare => moveRare,
        RarityTier.Epic => moveEpic,
        RarityTier.Legendary => moveLegendary,
        RarityTier.UberLegendary => moveUberLegendary,
        RarityTier.OneOfAKind => moveOneOfAKind,
        _ => null
    };

    private BullseyeMoveMode ChooseMoveMode(BullseyeMovementProfile p)
    {
        if (!bullseyeEnableMovement) return BullseyeMoveMode.None;
        if (p == null) return BullseyeMoveMode.None;

        if (p.mode != BullseyeMoveMode.Random)
            return p.mode;

        List<BullseyeMoveMode> pool = new List<BullseyeMoveMode>(3);
        if (p.randomAllowTopDrop) pool.Add(BullseyeMoveMode.TopDrop);
        if (p.randomAllowDrift) pool.Add(BullseyeMoveMode.DriftForSeconds);
        if (p.randomAllowWander) pool.Add(BullseyeMoveMode.Wander);

        if (pool.Count == 0) return BullseyeMoveMode.None;
        return pool[Random.Range(0, pool.Count)];
    }

    private void ConfigureAndStartBullseyeMovement(BullseyeMovementProfile p, BullseyeMoveMode chosenMode)
    {
        StopBullseyeMovement();

        if (!bullseyeEnableMovement) return;
        if (chosenMode == BullseyeMoveMode.None) return;

        if (!bullseyeRect || !bullseyeSpawnBounds) return;

        if (!TryGetSpawnSafeExtents(out Rect b, out float minX, out float maxX, out float minY, out float maxY))
            return;

        _activeMoveMode = chosenMode;
        _moveVelocityLocal = Vector2.zero;
        _moveStopAt = 0f;
        _moveNextChangeAt = 0f;

        _moveBounce = (p != null) ? p.bounceAtEdges : true;
        _topDropStopAtBottom = (p != null) ? p.topDropStopAtBottom : true;

        switch (_activeMoveMode)
        {
            case BullseyeMoveMode.TopDrop:
                {
                    float speed = (p != null) ? Mathf.Max(0f, p.topDropSpeed) : 0f;
                    _moveVelocityLocal = Vector2.down * speed;
                    _moveStopAt = 0f;
                    break;
                }

            case BullseyeMoveMode.DriftForSeconds:
                {
                    float speed = (p != null) ? Mathf.Max(0f, p.driftSpeed) : 0f;
                    float dur = (p != null) ? Mathf.Max(0.01f, p.driftSeconds) : 0.25f;
                    Vector2 dir = Random.insideUnitCircle.normalized;
                    if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
                    _moveVelocityLocal = dir * speed;
                    _moveStopAt = Time.unscaledTime + dur;
                    break;
                }

            case BullseyeMoveMode.Wander:
                {
                    float minS = (p != null) ? Mathf.Max(0f, p.wanderMinSpeed) : 0f;
                    float maxS = (p != null) ? Mathf.Max(minS, p.wanderMaxSpeed) : minS;
                    float interval = (p != null) ? Mathf.Max(0.05f, p.wanderChangeEverySeconds) : 0.25f;

                    PickNewWanderVelocity(minS, maxS);
                    _moveNextChangeAt = Time.unscaledTime + interval;
                    _moveStopAt = 0f;
                    break;
                }
        }
    }

    private void StopBullseyeMovement()
    {
        _activeMoveMode = BullseyeMoveMode.None;
        _moveVelocityLocal = Vector2.zero;
        _moveStopAt = 0f;
        _moveNextChangeAt = 0f;
    }

    private void UpdateBullseyeMovement()
    {
        if (!bullseyeEnableMovement) return;
        if (_state != State.AwaitBullseye) return;
        if (_activeMoveMode == BullseyeMoveMode.None) return;


if (!_bullseyeArmed) return;


        if (!bullseyeRect || !bullseyeSpawnBounds) { StopBullseyeMovement(); return; }

        if (!TryGetSpawnSafeExtents(out Rect b, out float minX, out float maxX, out float minY, out float maxY))
        {
            StopBullseyeMovement();
            return;
        }

        if (_activeMoveMode == BullseyeMoveMode.DriftForSeconds && Time.unscaledTime >= _moveStopAt)
        {
            StopBullseyeMovement();
            return;
        }

        if (_activeMoveMode == BullseyeMoveMode.Wander)
        {
            var p = GetMovementProfileForTier(_tier);
            float minS = (p != null) ? Mathf.Max(0f, p.wanderMinSpeed) : 0f;
            float maxS = (p != null) ? Mathf.Max(minS, p.wanderMaxSpeed) : minS;
            float interval = (p != null) ? Mathf.Max(0.05f, p.wanderChangeEverySeconds) : 0.25f;

            if (Time.unscaledTime >= _moveNextChangeAt)
            {
                PickNewWanderVelocity(minS, maxS);
                _moveNextChangeAt = Time.unscaledTime + interval;
            }
        }

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        Vector2 pos = GetBullseyeLocalPosInBounds();
        pos += _moveVelocityLocal * dt;

        bool hitX = false, hitY = false;

        if (pos.x < minX) { pos.x = minX; hitX = true; }
        else if (pos.x > maxX) { pos.x = maxX; hitX = true; }

        if (pos.y < minY) { pos.y = minY; hitY = true; }
        else if (pos.y > maxY) { pos.y = maxY; hitY = true; }

        if (hitX || hitY)
        {
            if (_moveBounce)
            {
                if (hitX) _moveVelocityLocal.x *= -1f;
                if (hitY) _moveVelocityLocal.y *= -1f;
            }
            else
            {
                if (_activeMoveMode == BullseyeMoveMode.TopDrop && hitY && _topDropStopAtBottom)
                {
                    StopBullseyeMovement();
                }
                else
                {
                    if (hitX) _moveVelocityLocal.x = 0f;
                    if (hitY) _moveVelocityLocal.y = 0f;
                }
            }
        }

        SetBullseyeLocalPosInBounds(pos);
    }

    private void PickNewWanderVelocity(float minSpeed, float maxSpeed)
    {
        Vector2 dir = Random.insideUnitCircle.normalized;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

        float spd = Random.Range(minSpeed, maxSpeed);
        _moveVelocityLocal = dir * spd;
    }

    private Vector2 GetBullseyeLocalPosInBounds()
    {
        if (!bullseyeSpawnBounds || !bullseyeRect) return Vector2.zero;
        Vector3 local3 = bullseyeSpawnBounds.InverseTransformPoint(bullseyeRect.position);
        return new Vector2(local3.x, local3.y);
    }

    private void SetBullseyeLocalPosInBounds(Vector2 localPos)
    {
        if (!bullseyeSpawnBounds || !bullseyeRect) return;
        Vector3 world = bullseyeSpawnBounds.TransformPoint(new Vector3(localPos.x, localPos.y, 0f));
        world.z = bullseyeRect.position.z;
        bullseyeRect.position = world;
    }

    private bool TryGetSpawnSafeExtents(out Rect b, out float minX, out float maxX, out float minY, out float maxY)
    {
        b = default;
        minX = maxX = minY = maxY = 0f;

        if (!bullseyeSpawnBounds || !bullseyeRect) return false;

        b = bullseyeSpawnBounds.rect;

        Vector2 bullSize = bullseyeRect.rect.size;

        Vector3 bullWorldScale = bullseyeRect.lossyScale;
        Vector3 boundsWorldScale = bullseyeSpawnBounds.lossyScale;

        float sx = (Mathf.Abs(boundsWorldScale.x) > 0.0001f) ? (bullWorldScale.x / boundsWorldScale.x) : bullWorldScale.x;
        float sy = (Mathf.Abs(boundsWorldScale.y) > 0.0001f) ? (bullWorldScale.y / boundsWorldScale.y) : bullWorldScale.y;

        float halfW = bullSize.x * Mathf.Abs(sx) * 0.5f;
        float halfH = bullSize.y * Mathf.Abs(sy) * 0.5f;

        minX = b.xMin + halfW;
        maxX = b.xMax - halfW;
        minY = b.yMin + halfH;
        maxY = b.yMax - halfH;

        if (maxX - minX <= 10f) return false;
        if (maxY - minY <= 10f) return false;

        return true;
    }

    private void PlaceBullseyeAtTopRandomX()
    {
        if (!bullseyeRect || !bullseyeSpawnBounds) return;

        if (!TryGetSpawnSafeExtents(out Rect b, out float minX, out float maxX, out float minY, out float maxY))
            return;

        float x = Random.Range(minX, maxX);
        float y = maxY;
        SetBullseyeLocalPosInBounds(new Vector2(x, y));
    }

private bool ShouldUseBullseyeRainNow()
{
    if (!bullseyeRainEnabled) return false;
    if (bullseyeRain == null) return false;

    if (bullseyeRainRequireAtLeast2Targets && _bullseyesRemaining < 2)
        return false;

    bool tierOk = _tier switch
    {
        RarityTier.Rare => rainRare,
        RarityTier.Epic => rainEpic,
        RarityTier.Legendary => rainLegendary,
        RarityTier.UberLegendary => rainUberLegendary,
        RarityTier.OneOfAKind => rainOneOfAKind,
        _ => false
    };

    if (!tierOk) return false;

    if (!bullseyeRainUseRandomChance) return true;
    return UnityEngine.Random.value < Mathf.Clamp01(bullseyeRainChance);
}

private void BeginBullseyeRain_Internal()
{
    // Hide the single-target UI (but keep status text ON)
    if (bullseyeGroup)
    {
        bullseyeGroup.alpha = 0f;
        bullseyeGroup.interactable = false;
        bullseyeGroup.blocksRaycasts = false;
    }

    HideHitFeedbackImmediate();
    StopBullseyeMovement();

    // Start rain
    bullseyeRain.OnFinished -= HandleBullseyeRainFinished;
    bullseyeRain.OnFinished += HandleBullseyeRainFinished;

  int totalTargets = Mathf.Max(0, bullseyeRainTotalTargets);
  
ShowBullseyeRainBullets(true);
UpdateBullseyeRainBulletsText();


    // Keep your existing “total score threshold” system.
    // Rain mode can still use that (precision still matters).

_rainBulletsLeft = totalTargets + Mathf.Max(0, bullseyeRainExtraBullets);
ShowBullseyeRainBullets(true);
UpdateBullseyeRainBulletsText();


    bullseyeRain.Begin(
        totalTargets,
        bullseyeRainFallSpeed,
        bullseyeRainSpawnInterval,
        bullseyeRainAllowedEscapes
    );

    _bullseyesRemaining = bullseyeRain.TargetsLeft;
    UpdateBullseyeStatusText();

    _ignorePressUntilFrame = Time.frameCount + 1;
    _state = State.AwaitBullseyeRain;
}

private void HandleBullseyeRainFinished(bool ok)
{
    StopBullseyeRainImmediate();

    if (!ok)
    {
        FailAndCleanup("bullseye rain: escaped too many", bullseyeRainEscapeFailMsg);
        return;
    }

    // Rain success = bullseye phase cleared (ignore score threshold)
    _bullseyesRemaining = 0;
    UpdateBullseyeStatusText();

    // Continue flow
    if (_beepsRemaining > 0)
    {
        BeginBeepPhase();
        return;
    }

    SuccessCatchNow();
}


private void StopBullseyeRainImmediate()
{
    if (bullseyeRain == null) return;

    bullseyeRain.OnFinished -= HandleBullseyeRainFinished;
    bullseyeRain.Stop();
    ShowBullseyeRainBullets(false);
}


    // -------------------- Threshold --------------------

    private float GetCurrentFishBullseyeThresholdTotal()
    {
        if (!_current) return bullseyeFallbackThreshold;

        var names = new[]
        {
            "BullseyeThreshold",
            "bullseyeThreshold",
            "BullseyeThresholdPoints",
            "bullseyeThresholdPoints",
            "BullseyeThresholdTotal",
            "bullseyeThresholdTotal"
        };

        var t = _current.GetType();

        foreach (var n in names)
        {
            var f = t.GetField(n, MemberFlags);
            if (f != null && TryConvertFloat(f.GetValue(_current), out var fv)) return fv;

            var p = t.GetProperty(n, MemberFlags);
            if (p != null && p.CanRead && TryConvertFloat(p.GetValue(_current), out var pv)) return pv;
        }

        return bullseyeFallbackThreshold;
    }

    // -------------------- Beep Phase --------------------

private string FormatWindowForUI(float seconds)
{
    if (seconds < 1f)
        return $"{Mathf.RoundToInt(seconds * 1000f)} ms";
    return $"{seconds:0.00} sec";
}

private void BeginBeepPhase()
{
    // Leave BarShown immediately so we don't re-enter Phase1 success branch
    _state = State.AwaitBeep;

    // But do NOT let AwaitBeep logic run until we arm it later
    _beepArmed = false;
    _beepAt = float.PositiveInfinity;
    _windowEnd = float.PositiveInfinity;

    _ignorePressUntilFrame = Time.frameCount + 2;

    if (_beepRoutine != null) StopCoroutine(_beepRoutine);
    _beepRoutine = StartCoroutine(BeginBeepPhase_WithTutorial());
}


private IEnumerator BeginBeepPhase_WithTutorial()
{
    bool shouldShow =
        !string.IsNullOrWhiteSpace(tutorialBeepWaitForBeepId) &&
        PlayerPrefs.GetInt(AIStorySeenPrefix + tutorialBeepWaitForBeepId, 0) == 0 &&
        AIStoryDirector.Instance != null;

    if (shouldShow)
    {
        // Wait 1 frame so the previous click can't instantly close the tutorial.
        yield return null;

        AIStoryDirector.Instance.Trigger(tutorialBeepWaitForBeepId);

        // Wait until the story popup is closed.
        while (AIStoryDirector.Instance != null && AIStoryDirector.Instance.IsOpen)
            yield return null;

            yield return null; // eat the close click frame

    }

    // Now start the real beep logic (timers calculated AFTER tutorial closes)
    BeginBeepPhase_Internal();

    _beepRoutine = null;
}

private void BeginBeepPhase_Internal()
{
    float min = Mathf.Max(0f, _current ? _current.reaction2.beepDelayMin : 0.5f);
    float max = Mathf.Max(min, _current ? _current.reaction2.beepDelayMax : 2f);

    // This is the success window (reaction window)
    float win = Mathf.Max(0.05f, _current ? _current.reaction2.successWindow : 1.2f);

    var tune = GetTuningForTier(_tier);
    if (tune != null && tune.overrideBeepSuccessWindow)
        win = Mathf.Max(0.05f, tune.beepSuccessWindowSeconds);

    int left = Mathf.Max(0, _beepsRemaining);

    string msg =
        $"Wait for the BEEP, then click a button within {win:0.00} seconds!\n" +
        string.Format(beepsLeftTemplate, left);

    ShowPrompt(msg);
    UpdateReactionTimeText();

    float delay = Random.Range(min, max);

    _beepAt = Time.unscaledTime + delay;
    _windowEnd = _beepAt + win;
    _ignorePressUntilFrame = Time.frameCount + 1;
    _state = State.AwaitBeep;
    _beepArmed = true;


    if (debugLogs) Debug.Log($"[Mini] BeepPhase: wait {delay:0.00}s, window {win:0.00}s");
}


    // -------------------- Success / Fail --------------------

    private void SuccessCatchNow()
    {
        TopTaskbarController.SetHoverRevealSuppressed(false);
        StopResolve();

       ShowPrompt(successMsg);






        StartCoroutine(HidePromptAfter(successMsgSeconds));

        HideBullseyeImmediate();
        HideHitFeedbackImmediate();
        StopBullseyeMovement();

        if (beepFishFX) beepFishFX.HideImmediate();

_hookCardCloseRequested = true;
        HookCardService.FlashCaught(_current ? _current.DisplayName : string.Empty);
        if (_hookCardCloseRequested) HookCardService.Hide();

        ResumeWorldDeferred();
        OnFishHooked?.Invoke(_current);

        _current = null;
        _state = State.Idle;

        if (debugLogs) Debug.Log("[Mini] Success → caught");
    }

    private void FailAndCleanup(string reason, string promptOverride)
    {
       TopTaskbarController.SetHoverRevealSuppressed(false);
        StopResolve();

        if (debugLogs) Debug.Log($"[Mini] FAIL: {reason}");

        if (!string.IsNullOrEmpty(promptOverride))
        {
            ShowPrompt(promptOverride);
            StartCoroutine(HidePromptAfter(failMsgSeconds));
        }
        else ClearPrompt();

        HideReactionBar();
        HideBullseyeImmediate();
        HideHitFeedbackImmediate();
        StopBullseyeMovement();

        if (beepFishFX) beepFishFX.HideImmediate();


_hookCardCloseRequested = true;
        HookCardService.FlashEscaped(_current ? _current.DisplayName : string.Empty);
        if (_hookCardCloseRequested) HookCardService.Hide();

        ResumeWorldDeferred();

        if (_current) ScareAndDespawn(_current);
        OnFishFailed?.Invoke(_current);

        _current = null;
        _state = State.Idle;
    }

    private void StopResolve()
    {
        if (_resolveRoutine != null)
        {
            StopCoroutine(_resolveRoutine);
            _resolveRoutine = null;
        }
    }

    private void StartResolve(IEnumerator routine)
    {
        StopResolve();
        _resolveRoutine = StartCoroutine(routine);
    }

    // -------------------- Bar / Prompt helpers --------------------

    private void ShowReactionBar()
    {
        if (BarRootGO && !BarRootGO.activeSelf) BarRootGO.SetActive(true);

        if (reactionBarGroup)
        {
            reactionBarGroup.alpha = 1f;
            reactionBarGroup.interactable = true;
            reactionBarGroup.blocksRaycasts = true;
        }

        if (reactionBarUI) reactionBarUI.StartRun();
    }

    private void HideReactionBar()
    {
        if (reactionBarUI) reactionBarUI.StopRun();

        if (reactionBarGroup)
        {
            reactionBarGroup.alpha = 0f;
            reactionBarGroup.interactable = false;
            reactionBarGroup.blocksRaycasts = false;
        }
    }

    private void ShowPrompt(string msg)
    {
        if (tmpPrompt) { tmpPrompt.gameObject.SetActive(true); tmpPrompt.text = msg; }
        if (uiPrompt) { uiPrompt.gameObject.SetActive(true); uiPrompt.text = msg; }
    }

private void UpdateReactionTimeText()
{
    if (!reactionTimeText) return;

    string rt = FormatReactionTime(_lastReactionSeconds);
    if (string.IsNullOrEmpty(rt))
    {
        reactionTimeText.text = "";
        reactionTimeText.gameObject.SetActive(false);
    }
    else
    {
        reactionTimeText.text = rt;
        reactionTimeText.gameObject.SetActive(true);
    }
}


private string FormatReactionTime(float seconds)
{
    if (seconds < 0f) return null;

    if (seconds < 1.0f)
        return "REACTION TIME: " + Mathf.RoundToInt(seconds * 1000f).ToString("F0") + " ms";

    return "REACTION TIME: " + seconds.ToString("F2") + " sec";
}





   private void ClearPrompt()
{
    if (tmpPrompt) { tmpPrompt.text = ""; tmpPrompt.gameObject.SetActive(false); }
    if (uiPrompt) { uiPrompt.text = ""; uiPrompt.gameObject.SetActive(false); }

    if (reactionTimeText) { reactionTimeText.text = ""; reactionTimeText.gameObject.SetActive(false); }
}

    private IEnumerator HidePromptAfter(float seconds)
    {
        if (seconds <= 0f) { ClearPrompt(); yield break; }

        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end) yield return null;

        ClearPrompt();
    }

    // -------------------- World pause / resume --------------------

    private void PauseWorld()
    {
        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
    }

    private void ResumeWorldDeferred()
    {
        Time.timeScale = _prevTimeScale;
        _requireMouseUpToReenable = true;
        _reenableCasterAtFrame = Time.frameCount + 1;
        _ignorePressUntilFrame = Time.frameCount + 1;

        if (clickCaster) clickCaster.enabled = false;
    }

        // -------------------- Bullseye Status --------------------

    private void ShowBullseyeStatus(bool show)
    {
        if (!bullseyeStatusText) return;
        bullseyeStatusText.gameObject.SetActive(show);
    }

    private void UpdateBullseyeStatusText()
    {
        if (!bullseyeStatusText) return;

        int remaining = Mathf.Max(0, _bullseyesRemaining);
        int needed = Mathf.CeilToInt(Mathf.Max(0f, _bullseyeThresholdTotal - _bullseyeScoreTotal));

        bullseyeStatusText.text = string.Format(
            bullseyeStatusTemplate,
            _bullseyeScoreTotal,
            remaining,
            needed
        );
    }

    // -------- Bullseye Rain bullets UI --------

    private void ShowBullseyeRainBullets(bool show)
    {
        if (!bullseyeRainBulletsText) return;
        bullseyeRainBulletsText.gameObject.SetActive(show);
    }

    private void UpdateBullseyeRainBulletsText()
    {
        if (!bullseyeRainBulletsText) return;
        bullseyeRainBulletsText.text = string.Format(
            bullseyeRainBulletsTemplate,
            Mathf.Max(0, _rainBulletsLeft)
        );
    }
// -------------------- Checks / Utility --------------------

    private bool IsMarkerInsideGreenZone()
    {
        if (reactionBarUI == null || greenZone == null) return false;

        float u = reactionBarUI.MarkerNormalized01(); // 0..1
        var segs = greenZone.CurrentSegments;
        if (segs == null) return false;

        foreach (var s in segs)
        {
            float a = Mathf.Clamp01(s.start01);
            float b = Mathf.Clamp01(s.start01 + s.length01);
            if (u >= a && u <= b) return true;
        }

        return false;
    }

    private FishIdentity FindNearestFish(Vector2 worldPos, float radius)
    {
        var hits = Physics2D.OverlapCircleAll(worldPos, radius, fishMask);
        float best = float.MaxValue; FishIdentity bestFish = null;

        foreach (var h in hits)
        {
            var id = h.GetComponentInParent<FishIdentity>(); if (!id) continue;
            Vector2 closest = h.ClosestPoint(worldPos);
            float d2 = (closest - worldPos).sqrMagnitude;
            if (d2 < best) { best = d2; bestFish = id; }
        }

        return bestFish;
    }

    private void ScareAndDespawn(FishIdentity fish)
    {
        if (!fish) return;

        if (scarePoofPrefab)
        {
            var sr = fish.GetComponentInChildren<SpriteRenderer>();
            var fx = Instantiate(scarePoofPrefab, fish.transform.position, Quaternion.identity);
            var fxSr = fx.GetComponentInChildren<SpriteRenderer>();

            if (fxSr && sr)
            {
                fxSr.sortingLayerID = sr.sortingLayerID;
                fxSr.sortingOrder = sr.sortingOrder + 1;
            }
        }

        Destroy(fish.gameObject, destroyDelay);
    }

    // ----------------------------------------------------------------------
    // lightweight runtime stats reader just for THIS FISH overlay
    // ----------------------------------------------------------------------

    private void PushThisFishStatsToHookCard()
    {
        var binder = HookCardThisFishBinder.Instance;
        if (binder == null || !_current)
        {
            if (debugThisFishStats)
                Debug.Log("[Mini] PushThisFishStatsToHookCard: no binder or no current fish.");
            return;
        }

        var root = _current.gameObject;
        if (!root)
        {
            if (debugThisFishStats)
                Debug.Log("[Mini] PushThisFishStatsToHookCard: current fish has no GameObject.");
            return;
        }

        float wVal, lVal;
        bool hasW, hasL;

        hasW = TryGetRuntimeFloat(root,
            new[] { "FishWeightRuntime", "WeightRuntime" },
            new[] { "ValueKg", "valueKg", "Value", "value" },
            out wVal);

        hasL = TryGetRuntimeFloat(root,
            new[] { "FishLengthRuntime", "FishLenghtRuntime", "LengthRuntime", "LenghtRuntime" },
            new[] { "ValueCm", "valueCm", "Value", "value" },
            out lVal);

        if (debugThisFishStats)
        {
            Debug.Log($"[Mini] THIS FISH stats → hasW={hasW}, W={wVal:0.###}, hasL={hasL}, L={lVal:0.#}");
        }

        binder.SetFromThisFish(hasW ? wVal : 0f, hasL ? lVal : 0f);
    }

    private static bool TryGetRuntimeFloat(GameObject root,
        string[] typeNames,
        string[] valueNames,
        out float value)
    {
        value = 0f;

        var comp = FindCompByName(root, typeNames);
        if (!comp) return false;

        bool has;
        if (TryReadBool(comp, new[] { "HasValue", "hasValue" }, out var hv))
            has = hv;
        else
            has = true;

        if (!has) return false;

        if (TryReadFloatAny(comp, valueNames, out var val))
        {
            value = val;
            return true;
        }

        return false;
    }

    private static Component FindCompByName(GameObject root, string[] nameContainsAny)
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
                if (n.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return c;
            }
        }

        return null;
    }

    private static bool TryReadFloatAny(object obj, string[] names, out float value)
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

    private static float? TryReadFloat(System.Type type, object obj, string[] names)
    {
        float? value = null;
        TryReadFloat(type, obj, names, ref value);
        return value;
    }

    private static void TryReadFloat(System.Type type, object obj, string[] names, ref float? target)
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

    private static bool TryReadBool(object obj, string[] names, out bool value)
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

    private static bool? TryReadBool(System.Type type, object obj, string[] names)
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

    private static bool TryConvertFloat(object value, out float result)
    {
        result = default;
        if (value == null) return false;

        try
        {
            result = System.Convert.ToSingle(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
