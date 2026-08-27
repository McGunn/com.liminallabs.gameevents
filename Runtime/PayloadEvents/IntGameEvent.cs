using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>Game event carrying a int payload.</summary>
    [CreateAssetMenu(fileName = "IntGameEvent", menuName = "Liminal Labs/Game Events/Int Event")]
    public class IntGameEvent : GameEvent<int> { }
}
