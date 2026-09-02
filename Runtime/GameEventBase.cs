using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// Base of every game event asset. An event asset is a channel identity: raisers
    /// and listeners reference the same asset and never each other, so systems react
    /// to systems with no direct coupling. The asset holds no game state — an event
    /// says "something happened", it is not a variable.
    ///
    /// Raise semantics (fixed by design, pinned by tests):
    ///   - listeners fire in priority order, highest first, and in subscription order
    ///     within a priority (plain Subscribe is priority 0),
    ///   - a throwing listener is isolated (logged; the rest still fire),
    ///   - unsubscribing during a raise takes effect immediately,
    ///   - subscribing during a raise takes effect from the NEXT raise, whatever the priority,
    ///   - recursive raises are cut off at <see cref="MaxRaiseDepth"/> with an error.
    /// </summary>
    public abstract class GameEventBase : ScriptableObject
    {
        /// <summary>Deepest allowed listener-raises-event recursion before the raise
        /// is aborted with an error — the guard against event cycles.</summary>
        public const int MaxRaiseDepth = 8;

        [SerializeField, TextArea(2, 4), Tooltip("What this event means and who is expected to raise/listen. Shown in the Events Board.")]
        private string description;

        [SerializeField, HideInInspector]
        private string stableId;

        public string Description => description;

        /// <summary>
        /// A GUID minted when the asset is created and never changed — the event's
        /// identity for anything that outlives or leaves this process: network
        /// bridges, save systems, analytics. Resolve one back to its event at runtime
        /// through <see cref="GameEventRegistry"/>, which a <see cref="GameEventCatalog"/>
        /// feeds with the project's assets and a <see cref="SceneGameEvent"/> with its own.
        /// </summary>
        public string StableId => stableId;

#if UNITY_EDITOR
        protected virtual void OnValidate() => EnsureStableId();
#endif

        /// <summary>
        /// Mints the stable id if there is none. Editor-only in effect: a player cannot mint
        /// an id that would survive it, and an asset that reaches a player without one is
        /// what the setup checks exist to catch. Called by OnValidate for assets, and by a
        /// <see cref="SceneGameEvent"/> adopting an event made in code, which OnValidate may
        /// never see.
        /// </summary>
        internal void EnsureStableId()
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(stableId)) return;

            stableId = Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>Currently subscribed listeners.</summary>
        public abstract int ListenerCount { get; }

        /// <summary>Times this event has been raised this play session.</summary>
        public int TotalRaiseCount { get; private set; }

        /// <summary>Frame of the most recent raise, or -1.</summary>
        public int LastRaiseFrame { get; private set; } = -1;

        private int raiseDepth;

        protected bool IsRaising => raiseDepth > 0;

        /// <summary>Raises with the inspector/board test payload (the serialized debug
        /// value on typed events). Lets tooling raise any event polymorphically.</summary>
        public abstract void RaiseFromInspector();

        /// <summary>Formats each current listener as "Target.Method" into results (editor tooling).</summary>
        public abstract void DescribeListeners(List<string> results);

        protected abstract void ClearListenersInternal();

        protected bool BeginRaise()
        {
            if (raiseDepth >= MaxRaiseDepth)
            {
                Debug.LogError($"[GameEvents] '{name}' exceeded raise depth {MaxRaiseDepth} — a listener chain raises this event recursively. Raise aborted.", this);
                return false;
            }
            raiseDepth++;
            return true;
        }

        protected void EndRaise()
        {
            raiseDepth--;
        }

        protected void MarkRaised(string payloadDescription)
        {
            TotalRaiseCount++;
            LastRaiseFrame = Time.frameCount;
            RegisterLive(this);
            if (GameEventDiagnostics.Enabled)
            {
                GameEventDiagnostics.Record(this, payloadDescription, ListenerCount);
            }
        }

        protected static void LogListenerException(GameEventBase gameEvent, Delegate listener, Exception exception)
        {
            Debug.LogError($"[GameEvents] '{gameEvent.name}': listener {FormatListener(listener)} threw — remaining listeners still run.\n{exception}", gameEvent);
        }

        protected static string FormatListener(Delegate listener)
        {
            if (listener == null) return "(null)";
            string target = listener.Target is UnityEngine.Object obj && obj != null
                ? $"{obj.name} ({obj.GetType().Name})"
                : listener.Method.DeclaringType != null ? listener.Method.DeclaringType.Name : "static";
            return $"{target}.{listener.Method.Name}";
        }

        /// <summary>A listener with its priority, when it has one worth mentioning.</summary>
        protected static string FormatListener(Delegate listener, int priority) =>
            priority == 0 ? FormatListener(listener) : $"{FormatListener(listener)}  [priority {priority}]";

        // ---- play-session lifetime --------------------------------------------------
        // Event assets outlive play sessions (and survive Enter Play Mode without
        // domain reload), so every event that gained listeners or raise counts is
        // tracked and wiped when a new play session starts.

        private static readonly HashSet<GameEventBase> live = new HashSet<GameEventBase>();

        protected static void RegisterLive(GameEventBase gameEvent)
        {
            live.Add(gameEvent);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            foreach (GameEventBase gameEvent in live)
            {
                if (gameEvent == null) continue;
                gameEvent.ClearListenersInternal();
                gameEvent.raiseDepth = 0;
                gameEvent.TotalRaiseCount = 0;
                gameEvent.LastRaiseFrame = -1;
            }
            live.Clear();
        }
    }
}
