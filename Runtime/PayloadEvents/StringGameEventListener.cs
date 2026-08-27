using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>Listener component for <see cref="StringGameEvent"/> - event in, typed UnityEvent out.</summary>
    [AddComponentMenu("Liminal Labs/Game Events/String Event Listener")]
    public class StringGameEventListener : GameEventListener<string, StringGameEvent> { }
}
