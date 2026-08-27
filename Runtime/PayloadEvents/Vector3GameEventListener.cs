using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>Listener component for <see cref="Vector3GameEvent"/> - event in, typed UnityEvent out.</summary>
    [AddComponentMenu("Liminal Labs/Game Events/Vector3 Event Listener")]
    public class Vector3GameEventListener : GameEventListener<Vector3, Vector3GameEvent> { }
}
