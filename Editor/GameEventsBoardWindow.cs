using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// The Events Board — every game event in the project in one place. Left: the
    /// event roster with live listener/raise counts. Right: the selected event's
    /// description, an editable test payload with a Raise button, live listeners,
    /// its raise history, and (after a reference scan) every scene, prefab, and
    /// asset that points at it — including the orphans nothing references. With no
    /// selection, the right side is the live activity feed across all events.
    /// </summary>
    public sealed class GameEventsBoardWindow : EditorWindow
    {
        private readonly System.Collections.Generic.List<SceneGameEvent> sceneHosts =
            new System.Collections.Generic.List<SceneGameEvent>();

        private const float ListWidth = 280f;

        private readonly List<GameEventBase> events = new List<GameEventBase>();
        private readonly List<string> listenerNames = new List<string>();
        private Dictionary<GameEventBase, List<string>> referencers;
        private GameEventBase selected;
        private UnityEditor.Editor payloadEditor;
        private Vector2 listScroll, detailScroll;
        private string search = "";
        private int lastSeenVersion = -1;

        [MenuItem("Window/Liminal Labs/Game Events/Events and Activity", priority = 60)]
        public static void Open()
        {
            var window = GetWindow<GameEventsBoardWindow>();
            window.titleContent = new GUIContent("Game Events");
            window.minSize = new Vector2(640, 360);
            window.RefreshEvents();
        }

        void OnEnable()
        {
            GameEventDiagnostics.AddWatcher();
            RefreshEvents();
        }

        void OnDisable()
        {
            GameEventDiagnostics.RemoveWatcher();
            if (payloadEditor != null) DestroyImmediate(payloadEditor);
        }

        void OnProjectChange()
        {
            RefreshEvents();
            referencers = null;   // asset moves/deletes invalidate the scan
        }

        void Update()
        {
            // Repaint only when new raises arrived (cheap version check).
            if (EditorApplication.isPlaying && GameEventDiagnostics.Version != lastSeenVersion)
            {
                lastSeenVersion = GameEventDiagnostics.Version;
                Repaint();
            }
        }

        private void RefreshEvents()
        {
            events.Clear();
            // Scene-hosted events first, because a level designer's own wiring is the thing
            // they came here to find - and until now the Board could see only project assets,
            // which meant it showed them exactly none of it.
            sceneHosts.Clear();
            SceneGameEvent.Collect(sceneHosts);

            for (int i = 0; i < sceneHosts.Count; i++)
            {
                GameEventBase hosted = sceneHosts[i].Channel;
                if (hosted != null && !events.Contains(hosted)) events.Add(hosted);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:GameEventBase"))
            {
                var gameEvent = AssetDatabase.LoadAssetAtPath<GameEventBase>(AssetDatabase.GUIDToAssetPath(guid));
                if (gameEvent != null) events.Add(gameEvent);
            }
            events.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            Repaint();
        }

        // ---- chrome -----------------------------------------------------------------

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60))) RefreshEvents();
                if (GUILayout.Button("Scan References", EditorStyles.toolbarButton, GUILayout.Width(110))) ScanReferences();
                GUILayout.FlexibleSpace();
                search = GUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            }

            if (events.Count == 0)
            {
                EditorGUILayout.HelpBox("No game events yet. Create one via Assets > Create > Liminal Labs > Game Events.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawEventList();
                DrawDetail();
            }
        }

        private void DrawEventList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
            {
                listScroll = EditorGUILayout.BeginScrollView(listScroll);
                foreach (GameEventBase gameEvent in events)
                {
                    if (!string.IsNullOrEmpty(search) &&
                        gameEvent.name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUIStyle style = selected == gameEvent ? EditorStyles.boldLabel : EditorStyles.label;
                        if (GUILayout.Button(gameEvent.name, style))
                        {
                            Select(gameEvent);
                        }
                        GUILayout.FlexibleSpace();
                        string status = EditorApplication.isPlaying
                            ? $"{gameEvent.ListenerCount} ⟨ {gameEvent.TotalRaiseCount}×"
                            : PayloadLabel(gameEvent);
                        GUILayout.Label(status, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
                    }
                }
                EditorGUILayout.EndScrollView();
                GUILayout.Label(EditorApplication.isPlaying ? "listeners ⟨ raises" : $"{events.Count} event(s)", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void Select(GameEventBase gameEvent)
        {
            selected = selected == gameEvent ? null : gameEvent;
            if (payloadEditor != null) DestroyImmediate(payloadEditor);
            payloadEditor = null;
            if (selected != null) EditorGUIUtility.PingObject(selected);
        }

        private static string PayloadLabel(GameEventBase gameEvent)
        {
            string typeName = gameEvent.GetType().Name;
            if (typeName == nameof(GameEvent)) return "void";
            int cut = typeName.IndexOf("GameEvent", System.StringComparison.Ordinal);
            return cut > 0 ? typeName.Substring(0, cut).ToLowerInvariant() : typeName;
        }

        // ---- detail -----------------------------------------------------------------

        private void DrawDetail()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                if (selected == null) DrawActivityFeed();
                else DrawSelectedEvent();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSelectedEvent()
        {
            GUILayout.Label($"{selected.name}  ·  {PayloadLabel(selected)}", EditorStyles.largeLabel);
            if (!string.IsNullOrEmpty(selected.Description))
            {
                GUILayout.Label(selected.Description, EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.Space(4);

            // Test payload (typed events serialize a debugValue) + Raise.
            UnityEditor.Editor.CreateCachedEditor(selected, null, ref payloadEditor);
            var serialized = payloadEditor.serializedObject;
            serialized.Update();
            SerializedProperty debugValue = serialized.FindProperty("debugValue");
            if (debugValue != null)
            {
                EditorGUILayout.PropertyField(debugValue, new GUIContent("Test Payload"), true);
                serialized.ApplyModifiedProperties();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Raise", GUILayout.Width(90)))
                {
                    selected.RaiseFromInspector();
                }
                GUILayout.Label(
                    EditorApplication.isPlaying
                        ? $"{selected.ListenerCount} listener(s)  ·  raised {selected.TotalRaiseCount}×"
                        : "edit mode — most listeners subscribe in play mode",
                    EditorStyles.miniLabel);
            }

            if (EditorApplication.isPlaying)
            {
                listenerNames.Clear();
                selected.DescribeListeners(listenerNames);
                if (listenerNames.Count > 0)
                {
                    EditorGUILayout.Space(4);
                    GUILayout.Label("Listeners", EditorStyles.boldLabel);
                    foreach (string listener in listenerNames) GUILayout.Label("    " + listener, EditorStyles.miniLabel);
                }

                DrawHistory(selected, 12);
            }

            DrawReferences();
        }

        private void DrawActivityFeed()
        {
            GUILayout.Label("Activity (newest first)", EditorStyles.boldLabel);
            if (GameEventDiagnostics.Count == 0)
            {
                GUILayout.Label(EditorApplication.isPlaying
                    ? "No raises yet this session."
                    : "Enter play mode to watch raises live, or select an event on the left.", EditorStyles.miniLabel);
                return;
            }
            for (int i = 0; i < GameEventDiagnostics.Count && i < 30; i++)
            {
                GameEventDiagnostics.RaiseRecord record = GameEventDiagnostics.Get(i);
                string eventName = record.gameEvent != null ? record.gameEvent.name : "(destroyed)";
                string payload = record.payload != null ? $"  ·  {record.payload}" : "";
                GUILayout.Label($"frame {record.frame}  ·  {eventName}{payload}  ·  {record.listenerCount} listener(s)", EditorStyles.miniLabel);
            }
        }

        private void DrawHistory(GameEventBase gameEvent, int max)
        {
            bool any = false;
            int shown = 0;
            for (int i = 0; i < GameEventDiagnostics.Count && shown < max; i++)
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

        // ---- reference index --------------------------------------------------------

        private void DrawReferences()
        {
            EditorGUILayout.Space(6);
            GUILayout.Label("Referenced by", EditorStyles.boldLabel);
            if (referencers == null)
            {
                GUILayout.Label("    Press Scan References in the toolbar to index scenes, prefabs, and assets.", EditorStyles.miniLabel);
                return;
            }
            if (!referencers.TryGetValue(selected, out List<string> paths) || paths.Count == 0)
            {
                EditorGUILayout.HelpBox("Nothing references this event — a raiser or listener wired purely in code, or an orphan.", MessageType.Warning);
                return;
            }
            int shown = 0;
            foreach (string path in paths)
            {
                if (shown++ >= 20)
                {
                    GUILayout.Label($"    …and {paths.Count - 20} more", EditorStyles.miniLabel);
                    break;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("    " + path, EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(40)))
                    {
                        EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path));
                    }
                }
            }
        }

        private void ScanReferences()
        {
            var pathToEvent = new Dictionary<string, GameEventBase>();
            referencers = new Dictionary<GameEventBase, List<string>>();
            foreach (GameEventBase gameEvent in events)
            {
                string path = AssetDatabase.GetAssetPath(gameEvent);
                if (string.IsNullOrEmpty(path)) continue;
                pathToEvent[path] = gameEvent;
                referencers[gameEvent] = new List<string>();
            }

            string[] all = AssetDatabase.GetAllAssetPaths();
            try
            {
                for (int i = 0; i < all.Length; i++)
                {
                    string path = all[i];
                    if (!path.EndsWith(".prefab") && !path.EndsWith(".unity") && !path.EndsWith(".asset")) continue;
                    if (pathToEvent.ContainsKey(path)) continue;
                    if ((i & 127) == 0)
                    {
                        EditorUtility.DisplayProgressBar("Game Events", "Scanning references…", (float)i / all.Length);
                    }
                    foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                    {
                        if (pathToEvent.TryGetValue(dependency, out GameEventBase gameEvent))
                        {
                            referencers[gameEvent].Add(path);
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            Repaint();
        }
    }
}
