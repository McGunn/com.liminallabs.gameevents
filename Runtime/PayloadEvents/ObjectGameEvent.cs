using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>Game event carrying a Object payload.</summary>
    [CreateAssetMenu(fileName = "ObjectGameEvent", menuName = "Liminal Labs/Game Events/Object Event")]
    public class ObjectGameEvent : GameEvent<Object> { }
}
