using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LiminalLabs.Core.Editor;

namespace LiminalLabs.GameEvents
{
    /// <summary>Scene wiring checks: listener components with unassigned event slots
    /// are the silent no-ops of event systems — surface every one.</summary>
    public sealed class GameEventSceneCheck : ILiminalSetupCheck
    {
        public string Category => "Game Events";
        public int Order => 0;
        private const int MaxRows = 10;

        public void Run(LiminalSetupReport report)
        {
            var observed = new List<GameEventBase>();
            int listenerComponents = 0, brokenRows = 0, suppressed = 0;

            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (behaviour is not IGameEventListenerInfo info) continue;
                listenerComponents++;

                observed.Clear();
                int missing = info.GetObservedEvents(observed);
                if (missing == 0) continue;

                brokenRows++;
                if (brokenRows <= MaxRows)
                {
                    MonoBehaviour captured = behaviour;
                    report.Warn($"'{behaviour.gameObject.name}' has {missing} listener slot(s) with no event assigned",
                        "Those rows silently never fire.",
                        () => { Selection.activeObject = captured; EditorGUIUtility.PingObject(captured); }, "Select");
                }
                else suppressed++;
            }

            if (suppressed > 0)
            {
                report.Warn($"…and {suppressed} more listener component(s) with unassigned events", "Fix the ones above and re-run.");
            }
            if (brokenRows == 0 && listenerComponents > 0)
            {
                report.Pass($"{listenerComponents} listener component(s) in the open scene wire cleanly");
            }
        }
    }

    /// <summary>Project-wide event asset audit.</summary>
    public sealed class GameEventAssetCheck : ILiminalSetupCheck
    {
        public string Category => "Game Events";
        public int Order => 1;

        public void Run(LiminalSetupReport report)
        {
            var byName = new Dictionary<string, GameEventBase>();
            int total = 0, duplicates = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:GameEventBase"))
            {
                var gameEvent = AssetDatabase.LoadAssetAtPath<GameEventBase>(AssetDatabase.GUIDToAssetPath(guid));
                if (gameEvent == null) continue;
                total++;

                if (byName.TryGetValue(gameEvent.name, out GameEventBase existing))
                {
                    duplicates++;
                    GameEventBase captured = gameEvent;
                    report.Warn($"Two events named '{gameEvent.name}'",
                        "Identical names are indistinguishable in object pickers — rename one.",
                        () => { Selection.activeObject = captured; EditorGUIUtility.PingObject(captured); }, "Select");
                }
                else
                {
                    byName[gameEvent.name] = gameEvent;
                }
            }

            if (duplicates == 0 && total > 0)
            {
                report.Pass($"{total} game event(s), all uniquely named");
            }
        }
    }
}
