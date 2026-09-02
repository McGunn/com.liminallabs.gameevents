using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// The project's events, listed so a stable id can be turned back into an asset - the
    /// piece a network bridge or save system needs to turn a wire/disk id into the event
    /// on this machine. Populate it in the editor (the inspector's "Collect All Events In
    /// Project" button).
    ///
    /// Two ways to use it. <see cref="TryGet"/> answers from this catalog alone. Or
    /// <see cref="Activate"/> it once - a <see cref="GameEventCatalogActivator"/> in the
    /// bootstrap scene does - and every event in it resolves through
    /// <see cref="GameEventRegistry"/>, alongside the scene events of whatever levels are
    /// loaded, which is what a bridge wants:
    ///
    ///   send:    message.eventId = gameEvent.StableId
    ///   receive: if (GameEventRegistry.TryResolve(message.eventId, out var e)) ((GameEvent)e).Raise();
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
        private bool active;

        public IReadOnlyList<GameEventBase> Events => events;

        /// <summary>Whether this catalog's events are registered with <see cref="GameEventRegistry"/>.</summary>
        public bool IsActive => active;

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

        /// <summary>
        /// Registers every event here with <see cref="GameEventRegistry"/>. Returns how many
        /// were accepted; a duplicate id or a missing one is reported by the registry as it
        /// is refused. Activating twice is harmless.
        /// </summary>
        public int Activate()
        {
            if (active) return 0;

            active = true;
            activeCatalogs.Add(this);

            int registered = 0;
            foreach (GameEventBase gameEvent in events)
            {
                if (gameEvent != null && GameEventRegistry.Register(gameEvent)) registered++;
            }

            return registered;
        }

        /// <summary>Withdraws this catalog's events from <see cref="GameEventRegistry"/>.
        /// An event another catalog also holds stays registered through that one only if it
        /// activates again; catalogs are meant to partition the project, not overlap.</summary>
        public void Deactivate()
        {
            if (!active) return;

            active = false;
            activeCatalogs.Remove(this);

            foreach (GameEventBase gameEvent in events)
            {
                if (gameEvent != null) GameEventRegistry.Unregister(gameEvent);
            }
        }

        // An asset outlives the play session, and with Enter Play Mode without domain reload
        // so does its `active` flag - while the registry it registered with is wiped. The
        // flags are wiped alongside it, so the next session starts inactive, like the first.
        private static readonly HashSet<GameEventCatalog> activeCatalogs = new HashSet<GameEventCatalog>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            foreach (GameEventCatalog catalog in activeCatalogs)
            {
                if (catalog != null) catalog.active = false;
            }
            activeCatalogs.Clear();
        }
    }
}
