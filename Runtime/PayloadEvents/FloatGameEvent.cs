using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>Game event carrying a float payload.</summary>
    [CreateAssetMenu(fileName = "FloatGameEvent", menuName = "Liminal Labs/Game Events/Float Event")]
    public class FloatGameEvent : GameEvent<float> { }
}
