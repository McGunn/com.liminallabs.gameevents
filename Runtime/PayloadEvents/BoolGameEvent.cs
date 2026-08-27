using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>Game event carrying a bool payload.</summary>
    [CreateAssetMenu(fileName = "BoolGameEvent", menuName = "Liminal Labs/Game Events/Bool Event")]
    public class BoolGameEvent : GameEvent<bool> { }
}
