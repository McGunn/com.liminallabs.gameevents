using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// The events reachable by <see cref="GameEventBase.StableId"/> right now, across the
    /// whole process: every catalog that has been activated and every scene event whose
    /// host is enabled.
    ///
    /// A <see cref="GameEventCatalog"/> answers for the project's assets and only for them:
    /// a scene event is in no catalog, because it does not exist until its scene is loaded.
    /// A bridge or a save system resolving ids therefore needs one place to ask that knows
    /// about both kinds, and this is it. It owns nothing. Catalogs and hosts register what
    /// they have and withdraw it when it goes away, so an id resolves exactly while the
    /// event it names is alive - a network message naming a door in an unloaded level
    /// resolves to nothing, which is the truth.
    ///
    /// Two different events with one id is an authoring error - a duplicated asset, almost
    /// always. The second registration is refused with an error naming both, and the first
    /// keeps answering; the Setup window finds the copy.
    /// </summary>
    public static class GameEventRegistry
    {
        private static readonly Dictionary<string, GameEventBase> byId =
            new Dictionary<string, GameEventBase>(StringComparer.Ordinal);

        /// <summary>Raised after an event becomes resolvable (true) or stops being (false).
        /// For tooling, and for a bridge that wants to know when a level's events arrive.</summary>
        public static event Action<GameEventBase, bool> Changed;

        /// <summary>How many events resolve right now.</summary>
        public static int Count => byId.Count;

        /// <summary>Every event that resolves right now. Not for the hot path.</summary>
        public static IEnumerable<GameEventBase> All => byId.Values;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            byId.Clear();
            Changed = null;
        }

        /// <summary>The event a stable id names, if it is alive and registered.</summary>
        public static bool TryResolve(string stableId, out GameEventBase gameEvent)
        {
            gameEvent = null;
            if (string.IsNullOrEmpty(stableId)) return false;
            if (!byId.TryGetValue(stableId, out gameEvent)) return false;
            if (gameEvent != null) return true;

            // Destroyed without being withdrawn. Nothing shipped does that, but a game that
            // destroys an event asset it registered by hand would otherwise resolve to a
            // corpse forever.
            byId.Remove(stableId);
            gameEvent = null;
            return false;
        }

        /// <summary>
        /// Makes an event resolvable by its stable id. True if it is registered afterwards,
        /// including when it already was; false for an event with no id or an id already
        /// taken by a different event.
        /// </summary>
        public static bool Register(GameEventBase gameEvent)
        {
            if (gameEvent == null) return false;

            string id = gameEvent.StableId;
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning(
                    $"[GameEvents] '{gameEvent.name}' has no stable id, so nothing outside this " +
                    "process can name it and it was not registered. Inspect it once in the " +
                    "editor to mint one; for a scene event, re-save the scene.", gameEvent);
                return false;
            }

            if (byId.TryGetValue(id, out GameEventBase existing) && existing != null)
            {
                if (ReferenceEquals(existing, gameEvent)) return true;

                Debug.LogError(
                    $"[GameEvents] '{gameEvent.name}' and '{existing.name}' share the stable id " +
                    $"{id} - one is a copy of the other. '{existing.name}' keeps answering; " +
                    "the Setup window can re-mint the copy's id.", gameEvent);
                return false;
            }

            byId[id] = gameEvent;
            Changed?.Invoke(gameEvent, true);
            return true;
        }

        /// <summary>Stops an event resolving. Only the event that registered an id can
        /// withdraw it, so a copy refused above cannot knock out the original.</summary>
        public static bool Unregister(GameEventBase gameEvent)
        {
            if (gameEvent == null) return false;

            string id = gameEvent.StableId;
            if (string.IsNullOrEmpty(id)) return false;
            if (!byId.TryGetValue(id, out GameEventBase existing) || !ReferenceEquals(existing, gameEvent)) return false;

            byId.Remove(id);
            Changed?.Invoke(gameEvent, false);
            return true;
        }
    }
}
