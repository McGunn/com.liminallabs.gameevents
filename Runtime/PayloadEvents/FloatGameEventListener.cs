using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>Listener component for <see cref="FloatGameEvent"/> - event in, typed UnityEvent out.</summary>
    [AddComponentMenu("Liminal Labs/Game Events/Float Event Listener")]
    public class FloatGameEventListener : GameEventListener<float, FloatGameEvent> { }
}
