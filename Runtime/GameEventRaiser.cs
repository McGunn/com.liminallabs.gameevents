using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>When a <see cref="GameEventRaiser"/> fires on its own.</summary>
    public enum RaiseWhen
    {
        /// <summary>Only when something calls <see cref="GameEventRaiser.Raise"/> — a
        /// UnityEvent on a button, an interaction, a line of code. The default, because a
        /// component that fires by itself is the surprising one.</summary>
        Called = 0,

        /// <summary>As it becomes enabled. For a trigger that arms something the moment a
        /// level loads. The first time, it waits until the scene has finished enabling
        /// everything, so the listeners in the same level are all there to hear it;
        /// being re-enabled later fires at once.</summary>
        Enabled = 1,

        /// <summary>When something enters its trigger collider.</summary>
        TriggerEntered = 2,

        /// <summary>When something leaves its trigger collider.</summary>
        TriggerExited = 3,
    }

    /// <summary>
    /// The other half of a wire: something that raises.
    ///
    /// <b>Why this did not exist and should have.</b> The package shipped listeners in nine
    /// flavours and nothing at all that raises, so every wire a designer made had to end at a
    /// listener and begin in somebody's code. That is fine for <c>PlayerDied</c>, which a
    /// health script raises, and useless for a switch opening a door — the archetypal thing a
    /// level designer wants, and the one case with no component for it.
    ///
    /// It also left <see cref="IGameEventRaiserInfo"/> with no implementors, so the scene-view
    /// wiring tool could only ever draw dashed lines: it had nothing that had declared itself
    /// as a raiser, only things that happened to hold a reference.
    ///
    /// <b>Payload-less on purpose.</b> Level wiring — this switch, that door — is a signal, not
    /// a value. A raiser per payload type would be eight more components in the Add Component
    /// menu serving a case that wants none of them; a game with a value to send has a script
    /// with the value in it, and can call <c>Raise</c> on the typed event directly.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Game Events/Game Event Raiser")]
    public sealed class GameEventRaiser : MonoBehaviour, IGameEventRaiserInfo
    {
        [SerializeField, Tooltip("What to raise. Either a project asset or an event hosted in " +
                                 "this scene - they are the same type.")]
        private GameEvent gameEvent;

        [SerializeField, Tooltip("When it fires by itself. Called means never - something has " +
                                 "to ask.")]
        private RaiseWhen when = RaiseWhen.Called;

        [SerializeField, Tooltip("Only these layers count for the trigger options.")]
        private LayerMask triggerLayers = ~0;

        [SerializeField, Tooltip("Fire at most once, then stop. For a one-shot: a door that " +
                                 "opens, an alarm that has already gone off.")]
        private bool once;

        private bool spent;
        private bool started;

        /// <summary>What this raises. Null until something is assigned.</summary>
        public GameEvent Event => gameEvent;

        /// <summary>Whether a <see cref="once"/> raiser has already gone.</summary>
        public bool Spent => spent;

        /// <summary>
        /// Raise it.
        ///
        /// Public and parameterless so a UnityEvent can call it — which is how a switch, a
        /// button, an interaction or an animation event drives this without any of them
        /// knowing what a game event is.
        /// </summary>
        public void Raise()
        {
            if (gameEvent == null || (once && spent)) return;

            spent = true;
            gameEvent.Raise();
        }

        /// <summary>Let it fire again, for a one-shot that a checkpoint reload should
        /// re-arm.</summary>
        public void Rearm() => spent = false;

        public int GetRaisedEvents(List<GameEventBase> results)
        {
            if (gameEvent == null) return 1;

            results.Add(gameEvent);
            return 0;
        }

        /// <summary>
        /// The first <see cref="RaiseWhen.Enabled"/> raise happens here, not in OnEnable.
        ///
        /// A scene enables its objects in an order nobody controls, so a raise from OnEnable
        /// can go out before the listener two objects down has subscribed — and the switch
        /// that arms on load opens a door that was not yet listening, with nothing logged,
        /// because an event with no listeners is not an error. Start runs after every object
        /// in the scene has had its OnEnable, so the first raise reaches all of them. Only the
        /// first: a component re-enabled mid-session is enabled into a world that is already
        /// running, and fires at once.
        /// </summary>
        private void Start()
        {
            started = true;
            if (when == RaiseWhen.Enabled) Raise();
        }

        private void OnEnable()
        {
            if (started && when == RaiseWhen.Enabled) Raise();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (when == RaiseWhen.TriggerEntered && Admits(other)) Raise();
        }

        private void OnTriggerExit(Collider other)
        {
            if (when == RaiseWhen.TriggerExited && Admits(other)) Raise();
        }

        private bool Admits(Collider other) =>
            other != null && (triggerLayers.value & (1 << other.gameObject.layer)) != 0;
    }
}
