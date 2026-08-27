using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>Listener component for <see cref="BoolGameEvent"/> - event in, typed UnityEvent out.</summary>
    [AddComponentMenu("Liminal Labs/Game Events/Bool Event Listener")]
    public class BoolGameEventListener : GameEventListener<bool, BoolGameEvent> { }
}
