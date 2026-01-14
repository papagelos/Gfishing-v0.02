using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TaskbarMenuDropdown : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public string id;
        public string label;
        [Tooltip("If false, shows as 'Content locked'.")]
        public bool unlocked = true;

        public UnityEngine.Events.UnityEvent onClick;
    }

    [Header("UI")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private Button entryButtonPrefab;

    [Header("Entries")]
    [SerializeField] private List<Entry> entries = new List<Entry>();

    private readonly List<GameObject> _spawned = new();

    private void Reset()
    {
        contentRoot = GetComponent<RectTransform>();
    }

    public void Rebuild()
    {
        if (!contentRoot || !entryButtonPrefab)
            return;

        // Clear old
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i]) Destroy(_spawned[i]);
        }
        _spawned.Clear();

        foreach (var e in entries)
        {
            if (e == null) continue;

            var btn = Instantiate(entryButtonPrefab, contentRoot);
            btn.gameObject.SetActive(true);

            // Text
            var text = btn.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = e.unlocked ? e.label : $"{e.label} (Content locked)";

            btn.interactable = e.unlocked;

            btn.onClick.RemoveAllListeners();
            if (e.unlocked)
            {
                // invoke assigned actions
                btn.onClick.AddListener(() => e.onClick?.Invoke());
            }

            _spawned.Add(btn.gameObject);
        }
    }
}
