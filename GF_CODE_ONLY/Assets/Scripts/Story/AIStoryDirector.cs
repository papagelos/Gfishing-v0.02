using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using GalacticFishing.UI;

public class AIStoryDirector : MonoBehaviour
{
    public static AIStoryDirector Instance { get; private set; }

    // Allows other systems to know when a story popup is currently visible.
public bool IsOpen => screen != null && screen.IsOpen;


    [Header("Wiring")]
    [SerializeField] private AIStoryBook book;
    [SerializeField] private AIScreenController screen;

    [Header("Input")]
    [SerializeField] private bool closeWithEscape = true;

    // Kept for inspector compatibility. Meaning:
    // "advance/close with any KEYBOARD key or LEFT mouse click"
    [SerializeField] private bool closeWithAnyKey = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private readonly Queue<AIStoryBook.Entry> _queue = new();
    private readonly Dictionary<string, AIStoryBook.Entry> _byId = new();

    // Prevent duplicate queueing while pending/showing
    private readonly HashSet<string> _pendingOrShowing = new();

    private const string SeenPrefix = "AIStory_Seen_";
    private float _prevTimeScale = 1f;
    private AIStoryBook.Entry _current;





    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _byId.Clear();
        if (book != null)
        {
            foreach (var e in book.entries)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.id)) continue;
                if (!_byId.ContainsKey(e.id))
                    _byId.Add(e.id, e);
            }
        }
    }

    private void Update()
    {
        if (screen == null || !screen.IsOpen) return;

        bool escapePressed =
            closeWithEscape &&
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame;

        if (escapePressed)
        {
            CloseCurrentAndContinue();
            return;
        }

        if (!closeWithAnyKey) return;

        bool anyKeyboardKeyPressed =
            Keyboard.current != null &&
            Keyboard.current.anyKey != null &&
            Keyboard.current.anyKey.wasPressedThisFrame;

        bool leftMousePressed =
            Mouse.current != null &&
            Mouse.current.leftButton != null &&
            Mouse.current.leftButton.wasPressedThisFrame;

        if (anyKeyboardKeyPressed || leftMousePressed)
        {
            // Advance page if possible, otherwise close
            if (screen.TryAdvancePage())
                return;

            CloseCurrentAndContinue();
        }
    }

    public void Trigger(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        if (!_byId.TryGetValue(id, out var entry)) return;

        // show-once persistence
        if (entry.showOnce && PlayerPrefs.GetInt(SeenPrefix + id, 0) == 1)
        {
            if (debugLogs) Debug.Log($"[AIStory] Trigger ignored (already seen): {id}");
            return;
        }

        // de-dupe in-session (queued or currently on screen)
        if (_pendingOrShowing.Contains(id))
        {
            if (debugLogs) Debug.Log($"[AIStory] Trigger ignored (already pending/showing): {id}");
            return;
        }

        _pendingOrShowing.Add(id);
        _queue.Enqueue(entry);

        if (debugLogs) Debug.Log($"[AIStory] Trigger accepted: {id} (queue={_queue.Count})");

        // if nothing open, show immediately
        if (screen != null && !screen.IsOpen)
            ShowNextIfAny();
    }

    private void ShowNextIfAny()
    {
        if (_queue.Count == 0) return;

        _current = _queue.Dequeue();

        if (debugLogs) Debug.Log($"[AIStory] Showing: {_current.id}");

        if (_current.showOnce)
            PlayerPrefs.SetInt(SeenPrefix + _current.id, 1);

        if (_current.pauseGame)
        {
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        // Mirror to log
        AIMessageLogService.Instance?.AddImportant(NormalizeForLog(_current.text));

        screen.Show(_current.text);
    }

    private void CloseCurrentAndContinue()
    {
        if (_current != null && _current.pauseGame)
            Time.timeScale = _prevTimeScale;

        if (_current != null && !string.IsNullOrWhiteSpace(_current.id))
            _pendingOrShowing.Remove(_current.id);

        _current = null;

        if (screen != null)
            screen.HideImmediate();

        // show next message if queued
        ShowNextIfAny();
    }

    private static string NormalizeForLog(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        return raw.Replace("||", "\n\n").Trim();
    }
}
