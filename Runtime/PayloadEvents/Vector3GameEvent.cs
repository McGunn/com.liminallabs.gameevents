using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>Game event carrying a Vector3 payload.</summary>
    [CreateAssetMenu(fileName = "Vector3GameEvent", menuName = "Liminal Labs/Game Events/Vector3 Event")]
    public class Vector3GameEvent : GameEvent<Vector3> { }
}
