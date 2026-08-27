using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>Listener component for <see cref="IntGameEvent"/> - event in, typed UnityEvent out.</summary>
    [AddComponentMenu("Liminal Labs/Game Events/Int Event Listener")]
    public class IntGameEventListener : GameEventListener<int, IntGameEvent> { }
}
