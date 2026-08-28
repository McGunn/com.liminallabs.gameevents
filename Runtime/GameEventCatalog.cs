using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// Runtime lookup from <see cref="GameEventBase.StableId"/> to event asset — the
    /// piece a network bridge or save system needs to turn a wire/disk id back into
    /// the event on this machine. Populate it in the editor (the inspector's
    /// "Collect All Events In Project" button) and reference it from your bridge:
    ///
    ///   send:    message.eventId = gameEvent.StableId
    ///   receive: if (catalog.TryGet(message.eventId, out var e)) ((GameEvent)e).Raise();
    ///
    /// Bridges must mark remote-originated raises (a simple bool while raising) so
    /// they don't re-forward them in a loop.
    /// </summary>
    [CreateAssetMenu(fileName = "GameEventCatalog", menuName = "Liminal Labs/Game Events/Event Catalog")]
    public class GameEventCatalog : ScriptableObject
    {
        [SerializeField, Tooltip("Events resolvable by stable id. The inspector can collect every event in the project.")]
        private List<GameEventBase> events = new List<GameEventBase>();

        private Dictionary<string, GameEventBase> byId;

        public IReadOnlyList<GameEventBase> Events => events;

        /// <summary>Resolves a stable id to its event, or false. First entry wins on
        /// duplicate ids (the Setup window flags duplicates).</summary>
        public bool TryGet(string stableId, out GameEventBase gameEvent)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                gameEvent = null;
                return false;
            }
            if (byId == null) RebuildLookup();
            return byId.TryGetValue(stableId, out gameEvent) && gameEvent != null;
        }

        public void RebuildLookup()
        {
            byId = new Dictionary<string, GameEventBase>();
            foreach (GameEventBase gameEvent in events)
            {
                if (gameEvent == null || string.IsNullOrEmpty(gameEvent.StableId)) continue;
                if (!byId.ContainsKey(gameEvent.StableId)) byId[gameEvent.StableId] = gameEvent;
            }
        }
    }
}
