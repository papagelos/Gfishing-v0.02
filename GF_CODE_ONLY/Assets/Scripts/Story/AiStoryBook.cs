using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Galactic Fishing/AI/Story Book", fileName = "AIStoryBook")]
public class AIStoryBook : ScriptableObject
{
    public List<Entry> entries = new List<Entry>();

    [Serializable]
    public class Entry
    {
        [Tooltip("Unique key, e.g. RodPower_100, LakeUnlocked_2")]
        public string id;

        [TextArea(4, 12)]
        public string text;

        [Tooltip("If true, this message can only ever show once (persisted).")]
        public bool showOnce = true;

        [Tooltip("If true, Time.timeScale=0 while this is on screen.")]
        public bool pauseGame = true;
    }
}
