using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// Central raise history: a fixed ring buffer of recent raises across all events,
    /// powering the Events Board, the event inspector, and any game-side overlay.
    /// Recording is refcounted by watchers — with no watcher open the raise path pays
    /// a single bool check and never builds payload strings.
    /// </summary>
    public static class GameEventDiagnostics
    {
        public struct RaiseRecord
        {
            public GameEventBase gameEvent;
            public int frame;
            public float time;
            public string payload;       // null for payload-less events
            public int listenerCount;
        }

        public const int Capacity = 256;

        private static readonly RaiseRecord[] buffer = new RaiseRecord[Capacity];
        private static int head;
        private static int count;
        private static int watchers;

        /// <summary>True while at least one watcher (board, inspector, overlay) is registered.</summary>
        public static bool Enabled => watchers > 0;

        /// <summary>Bumps on every record — cheap change detection for repaints.</summary>
        public static int Version { get; private set; }

        public static int Count => count;

        /// <summary>Record by recency: 0 is the newest.</summary>
        public static RaiseRecord Get(int newestIndex)
        {
            int index = head - 1 - newestIndex;
            if (index < 0) index += Capacity;
            return buffer[index];
        }

        /// <summary>Registers a consumer of raise history (pair with <see cref="RemoveWatcher"/>).</summary>
        public static void AddWatcher() => watchers++;

        public static void RemoveWatcher() => watchers = Mathf.Max(0, watchers - 1);

        public static void Record(GameEventBase gameEvent, string payload, int listenerCount)
        {
            if (!Enabled) return;
            buffer[head] = new RaiseRecord
            {
                gameEvent = gameEvent,
                frame = Time.frameCount,
                time = Time.unscaledTime,
                payload = payload,
                listenerCount = listenerCount,
            };
            head = (head + 1) % Capacity;
            if (count < Capacity) count++;
            Version++;
        }

        // History clears per play session; watcher refcount is owned by whoever
        // registered (editor windows stay open across sessions) so it survives.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            head = 0;
            count = 0;
            Version++;
        }
    }
}
