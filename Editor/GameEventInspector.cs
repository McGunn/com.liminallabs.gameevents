using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// Inspector for every game event asset: description and test payload, a Raise
    /// button, and in play mode the live listener list and this event's recent raise
    /// history — so an event asset always shows who hears it and when it last fired.
    /// </summary>
    [CustomEditor(typeof(GameEventBase), true)]
    public class GameEventInspector : Editor
    {
        private readonly List<string> listenerNames = new List<string>();

        void OnEnable() => GameEventDiagnostics.AddWatcher();
        void OnDisable() => GameEventDiagnostics.RemoveWatcher();

        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (targets.Length != 1) return;

            var gameEvent = (GameEventBase)target;
            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Raise", GUILayout.Width(90)))
                {
                    gameEvent.RaiseFromInspector();
                }
                GUILayout.Label(
                    EditorApplication.isPlaying
                        ? $"{gameEvent.ListenerCount} listener(s)  ·  raised {gameEvent.TotalRaiseCount}×"
                        : "edit mode — most listeners subscribe in play mode",
                    EditorStyles.miniLabel);
            }

            if (!EditorApplication.isPlaying) return;

            listenerNames.Clear();
            gameEvent.DescribeListeners(listenerNames);
            if (listenerNames.Count > 0)
            {
                EditorGUILayout.Space(4);
                GUILayout.Label("Listeners", EditorStyles.boldLabel);
                foreach (string listener in listenerNames)
                {
                    GUILayout.Label("    " + listener, EditorStyles.miniLabel);
                }
            }

            DrawRecentRaises(gameEvent);
        }

        private static void DrawRecentRaises(GameEventBase gameEvent)
        {
            bool any = false;
            int shown = 0;
            for (int i = 0; i < GameEventDiagnostics.Count && shown < 8; i++)
            {
                GameEventDiagnostics.RaiseRecord record = GameEventDiagnostics.Get(i);
                if (record.gameEvent != gameEvent) continue;
                if (!any)
                {
                    EditorGUILayout.Space(4);
                    GUILayout.Label("Recent raises (newest first)", EditorStyles.boldLabel);
                    any = true;
                }
                string payload = record.payload != null ? $"  ·  {record.payload}" : "";
                GUILayout.Label($"    frame {record.frame}  ·  t {record.time:0.00}s{payload}  ·  {record.listenerCount} listener(s)", EditorStyles.miniLabel);
                shown++;
            }
        }
    }
}
