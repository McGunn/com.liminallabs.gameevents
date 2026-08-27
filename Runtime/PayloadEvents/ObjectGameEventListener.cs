using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>Listener component for <see cref="ObjectGameEvent"/> - event in, typed UnityEvent out.</summary>
    [AddComponentMenu("Liminal Labs/Game Events/Object Event Listener")]
    public class ObjectGameEventListener : GameEventListener<Object, ObjectGameEvent> { }
}
