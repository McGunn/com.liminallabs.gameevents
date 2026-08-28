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

            var dropRect = new Rect(field.x, field.y, field.width - 24f, field.height);
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

            using (new EditorGUI.DisabledScope(current == null))
            {
                if (GUI.Button(pingRect, new GUIContent("◎", "Ping the event asset"), EditorStyles.miniButton))
                {
                    EditorGUIUtility.PingObject(current);
                }
            }
            EditorGUI.EndProperty();
        }

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
