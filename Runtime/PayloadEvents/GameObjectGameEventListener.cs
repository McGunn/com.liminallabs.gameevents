using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>Listener component for <see cref="GameObjectGameEvent"/> - event in, typed UnityEvent out.</summary>
    [AddComponentMenu("Liminal Labs/Game Events/GameObject Event Listener")]
    public class GameObjectGameEventListener : GameEventListener<GameObject, GameObjectGameEvent> { }
}
