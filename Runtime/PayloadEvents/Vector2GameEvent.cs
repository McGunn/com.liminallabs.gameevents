using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>Game event carrying a Vector2 payload.</summary>
    [CreateAssetMenu(fileName = "Vector2GameEvent", menuName = "Liminal Labs/Game Events/Vector2 Event")]
    public class Vector2GameEvent : GameEvent<Vector2> { }
}
