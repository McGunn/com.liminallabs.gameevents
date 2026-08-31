using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using LiminalLabs.Core.Editor;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// Draws every game-event field as a searchable dropdown of the project's events
    /// of that type (with each event's description as its tooltip), a ping button,
    /// and a "Create New…" entry that makes the asset in place — no duplicating and
    /// renaming, no hunting through the object picker. Backed by the shared
    /// <see cref="LiminalAssetDropdown"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(GameEventBase), true)]
    public class GameEventFieldDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect field = EditorGUI.PrefixLabel(position, label);
            var current = property.objectReferenceValue as GameEventBase;

            var dropRect = new Rect(field.x, field.y, field.width - 48f, field.height);
            var sceneRect = new Rect(field.xMax - 46f, field.y, 22f, field.height);
            var pingRect = new Rect(field.xMax - 22f, field.y, 22f, field.height);

            string display = current != null ? current.name : "None";
            string tooltip = current != null && !string.IsNullOrEmpty(current.Description)
                ? current.Description
                : "Pick a game event (type to search), or create one in place.";
            if (EditorGUI.DropdownButton(dropRect, new GUIContent(display, tooltip), FocusType.Keyboard))
            {
                Type eventType = LiminalAssetDropdown.FieldAssetType(fieldInfo, typeof(GameEventBase));
                SerializedObject serializedObject = property.serializedObject;
                string path = property.propertyPath;
                new LiminalAssetDropdown(new AdvancedDropdownState(), eventType,
                    picked => LiminalAssetDropdown.Assign(serializedObject, path, picked),
                    NiceTypeName).Show(dropRect);
            }

            DrawSceneEvents(sceneRect, property);

            using (new EditorGUI.DisabledScope(current == null))
            {
                if (GUI.Button(pingRect, new GUIContent("◎", "Ping the event asset"), EditorStyles.miniButton))
                {
                    EditorGUIUtility.PingObject(current);
                }
            }
            EditorGUI.EndProperty();
        }

        /// <summary>
        /// A second, smaller way in: the events hosted in the open scenes.
        ///
        /// Separate from the main dropdown because that one is backed by the shared asset
        /// dropdown, which searches the project - and a scene-stored event has no asset path,
        /// so no amount of searching the project will ever find it. Rather than teach the
        /// shared control about scenes, this offers them alongside.
        ///
        /// Disabled with a plain tooltip when the scene has none, so the button explains its
        /// own emptiness instead of looking broken.
        /// </summary>
        private void DrawSceneEvents(Rect rect, SerializedProperty property)
        {
            hosts.Clear();
            SceneGameEvent.Collect(hosts);

            Type wanted = LiminalAssetDropdown.FieldAssetType(fieldInfo, typeof(GameEventBase));

            int usable = 0;
            for (int i = 0; i < hosts.Count; i++)
                if (wanted.IsInstanceOfType(hosts[i].Channel)) usable++;

            string tip = usable > 0
                ? "Pick an event hosted in this scene"
                : "No events of this type in the open scenes. Add a Scene Game Event to make one.";

            using (new EditorGUI.DisabledScope(usable == 0))
            {
                if (!GUI.Button(rect, new GUIContent("⌂", tip), EditorStyles.miniButton)) return;
            }

            var menu = new GenericMenu();
            SerializedObject owner = property.serializedObject;
            string path = property.propertyPath;

            for (int i = 0; i < hosts.Count; i++)
            {
                SceneGameEvent host = hosts[i];
                if (!wanted.IsInstanceOfType(host.Channel)) continue;

                string label = host.Channel.name + "  (" + host.gameObject.name + ")";
                menu.AddItem(new GUIContent(label), false,
                             () => LiminalAssetDropdown.Assign(owner, path, host.Channel));
            }

            menu.DropDown(rect);
        }

        private static readonly System.Collections.Generic.List<SceneGameEvent> hosts =
            new System.Collections.Generic.List<SceneGameEvent>();

        internal static string NiceTypeName(Type type)
        {
            if (type == typeof(GameEvent)) return "Game Event";
            if (type == typeof(GameEventBase)) return "Game Event (any)";
            string name = type.Name;
            int cut = name.IndexOf("GameEvent", StringComparison.Ordinal);
            return cut > 0 ? $"{name.Substring(0, cut)} Event" : name;
        }
    }
}
