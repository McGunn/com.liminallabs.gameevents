using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>Listener component for <see cref="Vector2GameEvent"/> - event in, typed UnityEvent out.</summary>
    [AddComponentMenu("Liminal Labs/Game Events/Vector2 Event Listener")]
    public class Vector2GameEventListener : GameEventListener<Vector2, Vector2GameEvent> { }
}
