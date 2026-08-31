using System.Collections.Generic;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// A component declaring which events it raises.
    ///
    /// <b>The mirror of <see cref="IGameEventListenerInfo"/>, and the harder half.</b> Who
    /// listens to an event is discoverable without asking — a listener holds a serialized
    /// reference, so the wiring is written down. Who <i>raises</i> one is not: any code
    /// anywhere may call <c>Raise()</c>, and no amount of scanning will find it.
    ///
    /// That asymmetry is why tools that draw event wiring are so often quietly wrong. They can
    /// show every listener and only the raisers that happened to be authored in the inspector,
    /// and they present the result as if it were the whole picture. A designer then concludes
    /// nothing raises an event that fires constantly.
    ///
    /// Implementing this is how a component says so out loud. It costs one method, and in
    /// return the scene view can draw a wire from this object rather than guessing — or worse,
    /// silently omitting it.
    ///
    /// <b>Not implementing it is fine.</b> A component with a serialized event field it never
    /// declares is still drawn, as an <i>inferred</i> raiser, dashed rather than solid. And an
    /// event raised from code that touches no serialized field is still visible the moment it
    /// fires, because the Board records every raise. Between the three there is no wiring a
    /// designer cannot eventually see; only wiring they should not be told is certain.
    /// </summary>
    public interface IGameEventRaiserInfo
    {
        /// <summary>
        /// Adds the events this component may raise to <paramref name="results"/>, skipping
        /// nulls, and returns how many of its event slots are unassigned.
        ///
        /// The unassigned count is the same contract <see cref="IGameEventListenerInfo"/> uses:
        /// an empty slot is usually a half-finished wire, and the setup checks report it.
        /// </summary>
        int GetRaisedEvents(List<GameEventBase> results);
    }
}
