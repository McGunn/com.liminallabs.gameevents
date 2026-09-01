using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace LiminalLabs.GameEvents
{
    /// <summary>Implemented by listener components so tooling (setup checks, the
    /// Events Board) can validate scene wiring without knowing concrete types.</summary>
    public interface IGameEventListenerInfo
    {
        /// <summary>Adds the events this component observes to results (nulls skipped)
        /// and returns how many event slots are unassigned.</summary>
        int GetObservedEvents(List<GameEventBase> results);
    }

    /// <summary>
    /// Designer-facing listener for payload-less events: rows of event → UnityEvent
    /// response, wired entirely in the inspector. Subscribes on enable, unsubscribes
    /// on disable — a disabled or destroyed object can never leave a dangling
    /// listener behind.
    ///
    /// Each row subscribes with a delegate it builds once and keeps. A method group
    /// written where a delegate is expected becomes a new delegate every time, so
    /// subscribing with the method directly costs two allocations per enable/disable
    /// cycle — on a pooled object, that is every spawn. Keeping the one it subscribed
    /// with makes enabling and disabling free after the first time, and unsubscribing
    /// exact: it leaves the event it actually joined, not whatever the field says now.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Game Events/Game Event Listener")]
    public class GameEventListener : MonoBehaviour, IGameEventListenerInfo
    {
        [Serializable]
        public class Binding
        {
            [Tooltip("Event to listen for.")]
            public GameEvent gameEvent;

            [Tooltip("Invoked whenever the event is raised (while this component is enabled).")]
            public UnityEvent response;

            private Action handler;
            private GameEvent subscribedTo;

            internal void Subscribe()
            {
                if (gameEvent == null) return;

                subscribedTo = gameEvent;
                subscribedTo.Subscribe(handler ?? (handler = Invoke));
            }

            internal void Unsubscribe()
            {
                if (subscribedTo == null || handler == null) return;

                subscribedTo.Unsubscribe(handler);
                subscribedTo = null;
            }

            private void Invoke() => response?.Invoke();
        }

        [SerializeField]
        private List<Binding> bindings = new List<Binding>();

        void OnEnable()
        {
            foreach (Binding binding in bindings) binding?.Subscribe();
        }

        void OnDisable()
        {
            foreach (Binding binding in bindings) binding?.Unsubscribe();
        }

        public int GetObservedEvents(List<GameEventBase> results)
        {
            int missing = 0;
            foreach (Binding binding in bindings)
            {
                if (binding == null) continue;
                if (binding.gameEvent != null) results.Add(binding.gameEvent);
                else missing++;
            }
            return missing;
        }
    }

    /// <summary>
    /// Base for typed listener components: one event, one typed UnityEvent response.
    /// Concrete types (<see cref="FloatGameEventListener"/>, …) are one-liners. Like
    /// <see cref="GameEventListener"/>, it subscribes with one delegate kept for its
    /// lifetime, so enable and disable allocate nothing.
    /// </summary>
    public abstract class GameEventListener<T, TEvent> : MonoBehaviour, IGameEventListenerInfo
        where TEvent : GameEvent<T>
    {
        [SerializeField, Tooltip("Event to listen for.")]
        private TEvent gameEvent;

        [SerializeField, Tooltip("Invoked with the payload whenever the event is raised (while this component is enabled).")]
        private UnityEvent<T> response;

        private Action<T> handler;
        private TEvent subscribedTo;

        void OnEnable()
        {
            if (gameEvent == null) return;

            subscribedTo = gameEvent;
            subscribedTo.Subscribe(handler ?? (handler = OnRaised));
        }

        void OnDisable()
        {
            if (subscribedTo == null || handler == null) return;

            subscribedTo.Unsubscribe(handler);
            subscribedTo = null;
        }

        private void OnRaised(T value) => response?.Invoke(value);

        public int GetObservedEvents(List<GameEventBase> results)
        {
            if (gameEvent != null) { results.Add(gameEvent); return 0; }
            return 1;
        }
    }
}
