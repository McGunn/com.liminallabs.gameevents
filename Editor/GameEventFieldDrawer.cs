using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// Draws every game-event field as a searchable dropdown of the project's events
    /// of that type (with each event's description as its tooltip), a ping button,
    /// and a "Create New…" entry that makes the asset in place — no duplicating and
    /// renaming, no hunting through the object picker.
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
                var dropdown = new GameEventDropdown(new AdvancedDropdownState(), FieldEventType(), property);
                dropdown.Show(dropRect);
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

        private Type FieldEventType()
        {
            Type type = fieldInfo.FieldType;
            if (type.IsArray) type = type.GetElementType();
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) type = type.GetGenericArguments()[0];
            return typeof(GameEventBase).IsAssignableFrom(type) ? type : typeof(GameEventBase);
        }
    }

    /// <summary>The searchable event picker behind <see cref="GameEventFieldDrawer"/>.</summary>
    internal sealed class GameEventDropdown : AdvancedDropdown
    {
        private readonly Type eventType;
        private readonly SerializedObject serializedObject;
        private readonly string propertyPath;
        private readonly Dictionary<int, Action> actions = new Dictionary<int, Action>();
        private int nextId = 1;

        public GameEventDropdown(AdvancedDropdownState state, Type eventType, SerializedProperty property) : base(state)
        {
            this.eventType = eventType;
            serializedObject = property.serializedObject;
            propertyPath = property.propertyPath;
            minimumSize = new Vector2(260f, 320f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem(NiceTypeName(eventType));

            root.AddChild(Item("None", () => Assign(null)));
            root.AddSeparator();

            var events = new List<GameEventBase>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{eventType.Name}"))
            {
                var gameEvent = AssetDatabase.LoadAssetAtPath<GameEventBase>(AssetDatabase.GUIDToAssetPath(guid));
                if (gameEvent != null && eventType.IsInstanceOfType(gameEvent)) events.Add(gameEvent);
            }
            events.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            bool showTypeSuffix = eventType.IsAbstract || eventType == typeof(GameEvent);
            foreach (GameEventBase gameEvent in events)
            {
                GameEventBase captured = gameEvent;
                string name = showTypeSuffix && gameEvent.GetType() != typeof(GameEvent)
                    ? $"{gameEvent.name}  ({NiceTypeName(gameEvent.GetType())})"
                    : gameEvent.name;
                root.AddChild(Item(name, () => Assign(captured)));
            }
            if (events.Count == 0)
            {
                root.AddChild(new AdvancedDropdownItem("(no events of this type yet)") { enabled = false, id = nextId++ });
            }

            root.AddSeparator();
            var concretes = ConcreteTypes();
            if (concretes.Count == 1)
            {
                Type only = concretes[0];
                root.AddChild(Item($"Create New {NiceTypeName(only)}…", () => CreateNew(only)));
            }
            else
            {
                var create = new AdvancedDropdownItem("Create New…") { id = nextId++ };
                foreach (Type concrete in concretes)
                {
                    Type captured = concrete;
                    create.AddChild(Item(NiceTypeName(captured), () => CreateNew(captured)));
                }
                root.AddChild(create);
            }
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (actions.TryGetValue(item.id, out Action action)) action();
        }

        private AdvancedDropdownItem Item(string name, Action action)
        {
            var item = new AdvancedDropdownItem(name) { id = nextId++ };
            actions[item.id] = action;
            return item;
        }

        private List<Type> ConcreteTypes()
        {
            var types = new List<Type>();
            if (!eventType.IsAbstract && !eventType.ContainsGenericParameters) types.Add(eventType);
            foreach (Type type in TypeCache.GetTypesDerivedFrom(eventType))
            {
                if (!type.IsAbstract && !type.ContainsGenericParameters && !types.Contains(type)) types.Add(type);
            }
            types.Sort((a, b) => string.CompareOrdinal(NiceTypeName(a), NiceTypeName(b)));
            return types;
        }

        private void Assign(GameEventBase value)
        {
            if (serializedObject.targetObject == null) return;   // inspector went away
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null) return;
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
        }

        private void CreateNew(Type concrete)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                $"Create {NiceTypeName(concrete)} Event", $"New{concrete.Name}", "asset",
                "Name the new event asset.", SuggestFolder(concrete));
            if (string.IsNullOrEmpty(path)) return;

            var asset = ScriptableObject.CreateInstance(concrete);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Assign((GameEventBase)asset);
            EditorGUIUtility.PingObject(asset);
        }

        /// <summary>New events default beside their siblings: the folder of an existing
        /// event of the same type, else any event, else Assets.</summary>
        private string SuggestFolder(Type concrete)
        {
            foreach (string typeName in new[] { concrete.Name, nameof(GameEventBase) })
            {
                string[] guids = AssetDatabase.FindAssets($"t:{typeName}");
                if (guids.Length > 0)
                {
                    string existing = AssetDatabase.GUIDToAssetPath(guids[0]);
                    if (existing.StartsWith("Assets/")) return Path.GetDirectoryName(existing).Replace('\\', '/');
                }
            }
            return "Assets";
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
