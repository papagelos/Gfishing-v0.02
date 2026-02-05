// Assets/Scripts/Story/AIStorySceneMonitor.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using GalacticFishing.Story;

public sealed class AIStorySceneMonitor : MonoBehaviour
{
    public static AIStorySceneMonitor Instance { get; private set; }

    private const string SeenPrefix = "AIStory_SceneStart_Seen_";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // DontDestroyOnLoad only works on root GameObjects.
        if (transform.parent != null)
        {
            var holder = new GameObject(nameof(AIStorySceneMonitor) + "_Root");
            holder.transform.SetParent(null, false);
            transform.SetParent(holder.transform, false);
            DontDestroyOnLoad(holder);
        }
        else
        {
            DontDestroyOnLoad(gameObject);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        TriggerSceneStart(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene == SceneManager.GetActiveScene())
            TriggerSceneStart(scene.name);
    }

    private void TriggerSceneStart(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        string triggerId = $"Scene_Start_{sceneName}";

        // STRICT: only trigger if the entry exists in the book.
        var entry = TryFindEntry(triggerId);
        if (entry == null)
        {
            if (debugLogs)
                Debug.Log($"[AIStorySceneMonitor] No entry for '{triggerId}' in AIStoryBook. Skipping.");
            return;
        }

        // PlayerPrefs guard only if showOnce is true
        if (entry.showOnce)
        {
            string seenKey = SeenPrefix + sceneName;
            if (PlayerPrefs.GetInt(seenKey, 0) == 1)
            {
                if (debugLogs)
                    Debug.Log($"[AIStorySceneMonitor] Skipping (already seen): {triggerId}");
                return;
            }

            PlayerPrefs.SetInt(seenKey, 1);
            PlayerPrefs.Save();
        }

        if (debugLogs)
            Debug.Log($"[AIStorySceneMonitor] Triggering: {triggerId} (showOnce={entry.showOnce})");

        // IMPORTANT: choose ONE route.
        // The director already listens to StoryEvents.OnRaised, so we raise only here.
        StoryEvents.Raise(triggerId);
    }

    private static AIStoryBook.Entry TryFindEntry(string id)
    {
        var director = AIStoryDirector.Instance;
        if (director == null)
            return null;

        try
        {
            var t = director.GetType();
            var fi = t.GetField("book", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            var book = fi != null ? fi.GetValue(director) as AIStoryBook : null;
            if (book == null || book.entries == null)
                return null;

            for (int i = 0; i < book.entries.Count; i++)
            {
                var e = book.entries[i];
                if (e != null && e.id == id)
                    return e;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
