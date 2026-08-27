using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// Code-first receiver base for a payload-less event: inherit, implement
    /// <see cref="OnEventRaised"/>, done — the subscription lifecycle (subscribe on
    /// enable, unsubscribe on disable, null-event tolerated) is owned here so no
    /// component ever hand-rolls it or leaks a dangling listener. The designer-first
    /// counterpart is the <see cref="GameEventListener"/> component.
    /// </summary>
    public abstract class GameEventReceiver : MonoBehaviour, IGameEventListenerInfo
    {
        [SerializeField, Tooltip("Event this component reacts to.")]
        private GameEvent gameEvent;

        /// <summary>The observed event (may be null).</summary>
        protected GameEvent Event => gameEvent;

        /// <summary>Override to add enable logic; always call base — it subscribes.</summary>
        protected virtual void OnEnable()
        {
            if (gameEvent != null) gameEvent.Subscribe(OnEventRaised);
        }

        /// <summary>Override to add disable logic; always call base — it unsubscribes.</summary>
        protected virtual void OnDisable()
        {
            if (gameEvent != null) gameEvent.Unsubscribe(OnEventRaised);
        }

        /// <summary>Called whenever the event is raised (while enabled).</summary>
        protected abstract void OnEventRaised();

        public int GetObservedEvents(List<GameEventBase> results)
        {
            // An empty event is a supported pattern here (driven externally), so it
            // is never reported as a broken slot — unlike listener component rows.
            if (gameEvent != null) results.Add(gameEvent);
            return 0;
        }
    }

    /// <summary>
    /// Code-first receiver base for a typed event: inherit with the payload and
    /// event type (e.g. <c>MyHealthBar : GameEventReceiver&lt;float, FloatGameEvent&gt;</c>),
    /// implement <see cref="OnEventRaised(T)"/>. Leaving the event unassigned is
    /// valid — a component can expose its reaction method publicly and be driven by
    /// a listener component instead (the demo's third lamp does exactly this).
    /// </summary>
    public abstract class GameEventReceiver<T, TEvent> : MonoBehaviour, IGameEventListenerInfo
        where TEvent : GameEvent<T>
    {
        [SerializeField, Tooltip("Event this component reacts to. May be left empty when something else (e.g. a listener component) drives this component directly.")]
        private TEvent gameEvent;

        /// <summary>The observed event (may be null).</summary>
        protected TEvent Event => gameEvent;

        /// <summary>Override to add enable logic; always call base — it subscribes.</summary>
        protected virtual void OnEnable()
        {
            if (gameEvent != null) gameEvent.Subscribe(OnEventRaised);
        }

        /// <summary>Override to add disable logic; always call base — it unsubscribes.</summary>
        protected virtual void OnDisable()
        {
            if (gameEvent != null) gameEvent.Unsubscribe(OnEventRaised);
        }

        /// <summary>Called with the payload whenever the event is raised (while enabled).</summary>
        protected abstract void OnEventRaised(T value);

        public int GetObservedEvents(List<GameEventBase> results)
        {
            // An empty event is a supported pattern here (driven externally), so it
            // is never reported as a broken slot — unlike listener component rows.
            if (gameEvent != null) results.Add(gameEvent);
            return 0;
        }
    }
}
