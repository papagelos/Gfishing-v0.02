using System;

namespace GalacticFishing.Story
{
    /// <summary>
    /// Tiny event bus for story triggers. No polling.
    /// Game code raises events; AIStoryDirector listens.
    /// </summary>
    public static class StoryEvents
    {
        public struct StoryEvent
        {
            public string id;
            public int intValue;
            public string strValue;

            public StoryEvent(string id, int intValue = 0, string strValue = null)
            {
                this.id = id;
                this.intValue = intValue;
                this.strValue = strValue;
            }
        }

        public static event Action<StoryEvent> OnRaised;

        public static void Raise(string id) =>
            OnRaised?.Invoke(new StoryEvent(id));

        public static void Raise(string id, int intValue) =>
            OnRaised?.Invoke(new StoryEvent(id, intValue));

        public static void Raise(string id, string strValue) =>
            OnRaised?.Invoke(new StoryEvent(id, 0, strValue));

        public static void Raise(string id, int intValue, string strValue) =>
            OnRaised?.Invoke(new StoryEvent(id, intValue, strValue));
    }
}
