using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>Game event carrying a string payload.</summary>
    [CreateAssetMenu(fileName = "StringGameEvent", menuName = "Liminal Labs/Game Events/String Event")]
    public class StringGameEvent : GameEvent<string> { }
}
