using System;
using System.Collections.Generic;
using LiminalLabs.Core.Console;
using UnityEngine;

namespace LiminalLabs.GameEvents.Console
{
    /// <summary>
    /// The game events console addon.
    ///
    /// Events are the hardest thing in a project to observe, because nothing about them
    /// is visible: a raise that nobody listened to and a raise that never happened look
    /// identical from the outside. So the commands here are about making that visible -
    /// who is listening, what fired, and whether the thing you expected to be raised
    /// ever was.
    /// </summary>
    internal static class GameEventsConsoleCommands
    {
        private const string Category = "Events";
        private static bool watching;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => watching = false;

        [ConsoleCommand("events", "Every game event loaded, with listener counts.",
            Category = Category,
            Description = "An event with zero listeners is the usual answer to 'why did nothing " +
                          "happen', and it is called out here rather than left to be noticed.")]
        public static void Events(
            ConsoleContext context,
            [ConsoleParam("Only names containing this.")] string filter = null)
        {
            GameEventBase[] all = Resources.FindObjectsOfTypeAll<GameEventBase>();

            var rows = new List<KeyValuePair<string, string>>();
            foreach (GameEventBase gameEvent in all)
            {
                if (!string.IsNullOrEmpty(filter) &&
                    gameEvent.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                var detail = new System.Text.StringBuilder();
                detail.Append(gameEvent.ListenerCount == 0
                    ? ConsoleMarkup.Warn("0 listeners")
                    : ConsoleMarkup.Value(gameEvent.ListenerCount + " listener(s)"));

                detail.Append(ConsoleMarkup.Dim($"  raised {gameEvent.TotalRaiseCount}×"));

                if (gameEvent.LastRaiseFrame >= 0)
                    detail.Append(ConsoleMarkup.Dim($"  last frame {gameEvent.LastRaiseFrame}"));
                else
                    detail.Append(ConsoleMarkup.Dim("  never raised"));

                rows.Add(new KeyValuePair<string, string>(gameEvent.name, detail.ToString()));
            }

            rows.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));

            context.Heading($"{rows.Count} event(s)");
            context.Table(rows, 28);
        }

        [ConsoleCommand("event", "One event in detail, including who is listening.",
            Category = Category,
            Examples = new[] { "event OnPlayerDied" })]
        public static void Event(
            ConsoleContext context,
            [ConsoleParam("Event asset name.", Completions = nameof(EventNames))] string name)
        {
            GameEventBase gameEvent = Find(name);

            context.Heading(gameEvent.name.ToUpperInvariant());

            var rows = new List<KeyValuePair<string, string>>
            {
                Row("type", gameEvent.GetType().Name),
                Row("stable id", string.IsNullOrEmpty(gameEvent.StableId)
                    ? ConsoleMarkup.Warn("none - saves cannot reference this")
                    : gameEvent.StableId),
                Row("listeners", gameEvent.ListenerCount == 0
                    ? ConsoleMarkup.Warn("0")
                    : gameEvent.ListenerCount.ToString()),
                Row("raised", gameEvent.TotalRaiseCount + "×"),
                Row("last frame", gameEvent.LastRaiseFrame >= 0
                    ? gameEvent.LastRaiseFrame.ToString()
                    : ConsoleMarkup.Dim("never")),
            };

            if (!string.IsNullOrEmpty(gameEvent.Description)) rows.Add(Row("description", gameEvent.Description));

            context.Table(rows, 12);

            var listeners = new List<string>();
            gameEvent.DescribeListeners(listeners);

            if (listeners.Count == 0)
            {
                context.Warn("Nothing is listening. Raising this does nothing.");
                return;
            }

            context.Print(string.Empty);
            context.Heading("Listeners");
            foreach (string listener in listeners) context.Print("  " + listener);
        }

        [ConsoleCommand("event.raise", "Raises an event.", Category = Category,
            Description = "Uses the same path the inspector's raise button does, so payload " +
                          "events fire with their configured debug value.",
            Examples = new[] { "event.raise OnPlayerDied" })]
        public static string Raise(
            [ConsoleParam("Event asset name.", Completions = nameof(EventNames))] string name)
        {
            GameEventBase gameEvent = Find(name);
            int listeners = gameEvent.ListenerCount;

            gameEvent.RaiseFromInspector();

            return listeners == 0
                ? ConsoleMarkup.Warn($"Raised {gameEvent.name}, but nothing was listening.")
                : $"Raised {ConsoleMarkup.Accent(gameEvent.name)} to {listeners} listener(s).";
        }

        [ConsoleVariable("events.watch", "Records every raise for `events.log`.", Category = Category)]
        public static bool Watching
        {
            get => watching;
            set
            {
                // Diagnostics is reference-counted so several watchers can coexist -
                // toggling a bool would let this addon switch off a Board that is open.
                if (value == watching) return;
                watching = value;

                if (value) GameEventDiagnostics.AddWatcher();
                else GameEventDiagnostics.RemoveWatcher();
            }
        }

        [ConsoleCommand("events.log", "Recent raises, newest last.", Category = Category,
            Description = "Needs `events.watch true` first. Recording is off by default because " +
                          "it costs something on every raise.")]
        public static void Log(ConsoleContext context, [ConsoleParam("How many.")] int count = 25)
        {
            if (!GameEventDiagnostics.Enabled)
            {
                context.Warn("Nothing is recording. Turn it on with: events.watch true");
                return;
            }

            int total = GameEventDiagnostics.Count;
            if (total == 0)
            {
                context.Info("No raises recorded yet.");
                return;
            }

            int show = Mathf.Min(count, total);

            // Get() indexes from the newest, so walking backwards prints oldest first -
            // which is the order things happened in, and the only readable one.
            for (int i = show - 1; i >= 0; i--)
            {
                GameEventDiagnostics.RaiseRecord record = GameEventDiagnostics.Get(i);
                if (record.gameEvent == null) continue;

                var line = new System.Text.StringBuilder();
                line.Append(ConsoleMarkup.Dim($"f{record.frame,-7}"));
                line.Append(ConsoleMarkup.Accent(record.gameEvent.name));

                if (!string.IsNullOrEmpty(record.payload))
                    line.Append(ConsoleMarkup.Value("  " + record.payload));

                line.Append(record.listenerCount == 0
                    ? ConsoleMarkup.Warn("  → nobody")
                    : ConsoleMarkup.Dim($"  → {record.listenerCount}"));

                context.Print(line.ToString());
            }
        }

        [ConsoleCommand("events.silent", "Events nothing is listening to.", Category = Category,
            Description = "A listener list that emptied when a scene unloaded, or an event that " +
                          "was never wired up. Both are worth knowing about.")]
        public static void Silent(ConsoleContext context)
        {
            GameEventBase[] all = Resources.FindObjectsOfTypeAll<GameEventBase>();

            var rows = new List<KeyValuePair<string, string>>();
            foreach (GameEventBase gameEvent in all)
            {
                if (gameEvent.ListenerCount > 0) continue;

                rows.Add(new KeyValuePair<string, string>(gameEvent.name,
                    gameEvent.TotalRaiseCount > 0
                        ? ConsoleMarkup.Bad($"raised {gameEvent.TotalRaiseCount}× into nothing")
                        : ConsoleMarkup.Dim("never raised either")));
            }

            if (rows.Count == 0)
            {
                context.Success("Every loaded event has at least one listener.");
                return;
            }

            rows.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));

            context.Heading($"{rows.Count} event(s) with no listeners");
            context.Table(rows, 28);
        }

        [ConsoleCommand("events.catalog", "The event catalogs and what they contain.",
            Category = Category)]
        public static void Catalog(ConsoleContext context)
        {
            GameEventCatalog[] catalogs = Resources.FindObjectsOfTypeAll<GameEventCatalog>();

            if (catalogs.Length == 0)
            {
                context.Info("No GameEventCatalog is loaded.");
                return;
            }

            foreach (GameEventCatalog catalog in catalogs)
            {
                context.Heading(catalog.name.ToUpperInvariant());

                var rows = new List<KeyValuePair<string, string>>();
                foreach (GameEventBase gameEvent in catalog.Events)
                {
                    if (gameEvent == null)
                    {
                        rows.Add(Row(ConsoleMarkup.Bad("<missing>"),
                            ConsoleMarkup.Dim("an entry in this catalog points at nothing")));
                        continue;
                    }

                    rows.Add(Row(gameEvent.name,
                        ConsoleMarkup.Dim(gameEvent.StableId) +
                        ConsoleMarkup.Dim($"  {gameEvent.ListenerCount} listener(s)")));
                }

                context.Table(rows, 28);
            }
        }

        private static GameEventBase Find(string name)
        {
            GameEventBase[] all = Resources.FindObjectsOfTypeAll<GameEventBase>();

            foreach (GameEventBase gameEvent in all)
                if (string.Equals(gameEvent.name, name, StringComparison.OrdinalIgnoreCase)) return gameEvent;

            foreach (GameEventBase gameEvent in all)
                if (string.Equals(gameEvent.StableId, name, StringComparison.OrdinalIgnoreCase)) return gameEvent;

            foreach (GameEventBase gameEvent in all)
                if (gameEvent.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return gameEvent;

            throw new ConsoleException($"No loaded game event matching '{name}'. Try `events`.");
        }

        private static IEnumerable<string> EventNames()
        {
            foreach (GameEventBase gameEvent in Resources.FindObjectsOfTypeAll<GameEventBase>())
                yield return gameEvent.name;
        }

        private static KeyValuePair<string, string> Row(string key, string value) =>
            new KeyValuePair<string, string>(key, value);
    }
}
