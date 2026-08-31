using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// Creating and naming a scene event, and showing what is wired to it.
    ///
    /// <b>The create step has to be editor code.</b> A scene-stored event is a
    /// ScriptableObject with no asset path, brought into being with
    /// <see cref="ScriptableObject.CreateInstance(Type)"/> and then kept alive by the scene
    /// referencing it. Nothing about that can happen at runtime and be saved, so the button
    /// lives here.
    /// </summary>
    [CustomEditor(typeof(SceneGameEvent))]
    public sealed class SceneGameEventInspector : UnityEditor.Editor
    {
        private static readonly Type[] Kinds =
        {
            typeof(GameEvent), typeof(BoolGameEvent), typeof(FloatGameEvent),
            typeof(IntGameEvent), typeof(StringGameEvent), typeof(Vector2GameEvent),
            typeof(Vector3GameEvent), typeof(GameObjectGameEvent), typeof(ObjectGameEvent),
        };

        private readonly List<string> listeners = new List<string>();
        private readonly List<Component> raisers = new List<Component>();
        private readonly List<Component> receivers = new List<Component>();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var host = (SceneGameEvent)target;

            if (!host.HasChannel) DrawCreate(host);
            else DrawExisting(host);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("notes"));

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCreate(SceneGameEvent host)
        {
            EditorGUILayout.HelpBox(
                "This host is empty. Create an event and it will be stored in the scene - it " +
                "travels with the level, duplicates when you duplicate the scene, and never " +
                "appears in your project folder.",
                MessageType.Info);

            for (int i = 0; i < Kinds.Length; i++)
            {
                Type kind = Kinds[i];
                if (!GUILayout.Button("Create " + Nice(kind))) continue;

                Create(host, kind);
                break;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Or point at a project asset", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("channel"),
                                          new GUIContent("Channel"));
        }

        private void DrawExisting(SceneGameEvent host)
        {
            GameEventBase channel = host.Channel;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(channel.name, EditorStyles.boldLabel);

                GUILayout.Label(host.IsSceneStored ? "in this scene" : "PROJECT ASSET",
                                EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
            }

            if (!host.IsSceneStored)
            {
                EditorGUILayout.HelpBox(
                    "This host points at a project asset, so it is a second name for a global " +
                    "event rather than a wire of its own. That works, but every level using " +
                    "that asset shares it - which is usually not what a host is for.",
                    MessageType.Warning);
            }

            string renamed = EditorGUILayout.DelayedTextField("Name", channel.name);
            if (renamed != channel.name && !string.IsNullOrWhiteSpace(renamed))
            {
                Undo.RecordObject(channel, "Rename Scene Event");
                channel.name = renamed;
                MarkDirty(host);
            }

            EditorGUILayout.Space();

            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField(
                    $"{channel.ListenerCount} listener(s)  ·  raised {channel.TotalRaiseCount}×",
                    EditorStyles.miniLabel);

                if (GUILayout.Button("Raise")) channel.RaiseFromInspector();

                listeners.Clear();
                channel.DescribeListeners(listeners);
                for (int i = 0; i < listeners.Count; i++)
                    EditorGUILayout.LabelField("    " + listeners[i], EditorStyles.miniLabel);
            }
            else
            {
                DrawWiring(host);
            }
        }

        /// <summary>
        /// What is wired to this, found by looking at what actually points at the event.
        ///
        /// Not a cache and not a registry: the serialized fields <i>are</i> the wiring, so
        /// reading them is the only answer that cannot be stale.
        /// </summary>
        private void DrawWiring(SceneGameEvent host)
        {
            raisers.Clear();
            receivers.Clear();
            GameEventWiring.FindWired(host.Channel, raisers, receivers);

            DrawSide("Raised by", raisers, GameEventWiring.RaiserColour);
            DrawSide("Listened to by", receivers, GameEventWiring.ListenerColour);

            if (raisers.Count == 0 && receivers.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Nothing is wired to this yet. Drag from an object to another in the scene " +
                    "view with the Game Event Wiring tool, or point a listener's event field " +
                    "at this host.",
                    MessageType.None);
            }
        }

        private static void DrawSide(string label, List<Component> found, Color colour)
        {
            if (found.Count == 0) return;

            Color was = GUI.color;
            GUI.color = colour;
            EditorGUILayout.LabelField(label + " (" + found.Count + ")", EditorStyles.boldLabel);
            GUI.color = was;

            for (int i = 0; i < found.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(12f);

                    if (GUILayout.Button(found[i].gameObject.name + "  ·  " +
                                         found[i].GetType().Name,
                                         EditorStyles.miniLabel))
                    {
                        Selection.activeGameObject = found[i].gameObject;
                        EditorGUIUtility.PingObject(found[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Bring an event into being inside the scene.
        ///
        /// The three lines that matter are the undo registration, the name, and marking the
        /// scene dirty. Without the first, deleting a wire cannot be taken back. Without the
        /// last, the event exists in memory and is not written, so it is gone on reload and
        /// every reference to it becomes null - which looks exactly like the feature not
        /// working.
        /// </summary>
        private void Create(SceneGameEvent host, Type kind)
        {
            var created = (GameEventBase)ScriptableObject.CreateInstance(kind);
            created.name = host.gameObject.name;

            Undo.RegisterCreatedObjectUndo(created, "Create Scene Event");
            Undo.RecordObject(host, "Create Scene Event");

            host.Adopt(created);

            EditorUtility.SetDirty(host);
            MarkDirty(host);
        }

        private static void MarkDirty(SceneGameEvent host)
        {
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(host.gameObject.scene);
        }

        private static string Nice(Type kind)
        {
            string name = kind.Name.Replace("GameEvent", string.Empty);
            return string.IsNullOrEmpty(name) ? "Event (no payload)" : name + " Event";
        }
    }
}
