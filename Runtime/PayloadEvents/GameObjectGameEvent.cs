using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>Game event carrying a GameObject payload.</summary>
    [CreateAssetMenu(fileName = "GameObjectGameEvent", menuName = "Liminal Labs/Game Events/GameObject Event")]
    public class GameObjectGameEvent : GameEvent<GameObject> { }
}
