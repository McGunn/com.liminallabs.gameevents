using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// A payload-less game event: "the door opened", "the wave ended". Raise it from
    /// code, a UnityEvent (button, animation event, timeline signal), or the
    /// inspector; listen from code via <see cref="Subscribe(Action)"/> or with a
    /// <see cref="GameEventListener"/> component. See <see cref="GameEventBase"/> for
    /// the raise-safety guarantees.
    /// </summary>
    [CreateAssetMenu(fileName = "GameEvent", menuName = "Liminal Labs/Game Events/Game Event")]
    public class GameEvent : GameEventBase
    {
        private readonly ListenerList<Action> listeners = new ListenerList<Action>();

        public override int ListenerCount => listeners.Count;

        public void Subscribe(Action listener) => Subscribe(listener, 0);

        /// <summary>
        /// Subscribes with a priority. Higher runs first; equal priorities run in
        /// subscription order; plain <see cref="Subscribe(Action)"/> is priority 0.
        ///
        /// For the one listener that must see the event before the rest - a guard that
        /// changes state the others read, an analytics tap - not for ordering everything,
        /// which is the coupling this package exists to avoid. A subscription made during a
        /// raise takes effect from the next raise whatever its priority.
        /// </summary>
        public void Subscribe(Action listener, int priority)
        {
            if (listeners.Add(listener, priority, IsRaising))
            {
                RegisterLive(this);
            }
            else if (listener != null)
            {
                Debug.LogWarning($"[GameEvents] '{name}': duplicate subscribe from {FormatListener(listener)} ignored.", this);
            }
        }

        public void Unsubscribe(Action listener)
        {
            listeners.Remove(listener, IsRaising);
        }

        public void Raise()
        {
            if (!BeginRaise()) return;
            MarkRaised(null);

            int count = listeners.SnapshotCount;
            for (int i = 0; i < count; i++)
            {
                Action listener = listeners[i];
                if (listener == null) continue;
                try
                {
                    listener();
                }
                catch (Exception exception)
                {
                    LogListenerException(this, listener, exception);
                }
            }

            EndRaise();
            if (!IsRaising) listeners.Compact();
        }

        public override void RaiseFromInspector() => Raise();

        public override void DescribeListeners(List<string> results)
        {
            for (int i = 0; i < listeners.SnapshotCount; i++)
            {
                if (listeners[i] != null) results.Add(FormatListener(listeners[i], listeners.PriorityAt(i)));
            }
        }

        protected override void ClearListenersInternal() => listeners.Clear();
    }

    /// <summary>
    /// A game event carrying a payload. Abstract — concrete payload types
    /// (<see cref="FloatGameEvent"/>, <see cref="BoolGameEvent"/>, …) are one-liners,
    /// so project-specific payloads are trivial to add. Same raise-safety guarantees
    /// as <see cref="GameEvent"/>.
    /// </summary>
    public abstract class GameEvent<T> : GameEventBase
    {
        [SerializeField, Tooltip("Payload used by the inspector / Events Board Raise button.")]
        private T debugValue;

        private readonly ListenerList<Action<T>> listeners = new ListenerList<Action<T>>();

        public override int ListenerCount => listeners.Count;

        /// <summary>The serialized test payload (used by <see cref="RaiseFromInspector"/>).</summary>
        public T DebugValue => debugValue;

        public void Subscribe(Action<T> listener) => Subscribe(listener, 0);

        /// <summary>
        /// Subscribes with a priority. Higher runs first; equal priorities run in
        /// subscription order; plain <see cref="Subscribe(Action{T})"/> is priority 0. See
        /// <see cref="GameEvent.Subscribe(Action, int)"/> for when that is worth having.
        /// </summary>
        public void Subscribe(Action<T> listener, int priority)
        {
            if (listeners.Add(listener, priority, IsRaising))
            {
                RegisterLive(this);
            }
            else if (listener != null)
            {
                Debug.LogWarning($"[GameEvents] '{name}': duplicate subscribe from {FormatListener(listener)} ignored.", this);
            }
        }

        public void Unsubscribe(Action<T> listener)
        {
            listeners.Remove(listener, IsRaising);
        }

        public void Raise(T value)
        {
            if (!BeginRaise()) return;
            MarkRaised(GameEventDiagnostics.Enabled ? (value != null ? value.ToString() : "null") : null);

            int count = listeners.SnapshotCount;
            for (int i = 0; i < count; i++)
            {
                Action<T> listener = listeners[i];
                if (listener == null) continue;
                try
                {
                    listener(value);
                }
                catch (Exception exception)
                {
                    LogListenerException(this, listener, exception);
                }
            }

            EndRaise();
            if (!IsRaising) listeners.Compact();
        }

        public override void RaiseFromInspector() => Raise(debugValue);

        public override void DescribeListeners(List<string> results)
        {
            for (int i = 0; i < listeners.SnapshotCount; i++)
            {
                if (listeners[i] != null) results.Add(FormatListener(listeners[i], listeners.PriorityAt(i)));
            }
        }

        protected override void ClearListenersInternal() => listeners.Clear();
    }
}
