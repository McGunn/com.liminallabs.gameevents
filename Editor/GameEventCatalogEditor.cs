using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>Catalog inspector: one button fills it with every event in the
    /// project (sorted by name), so keeping it complete is a click, not a chore.</summary>
    [CustomEditor(typeof(GameEventCatalog))]
    public class GameEventCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(4);

            var catalog = (GameEventCatalog)target;
            if (GUILayout.Button("Collect All Events In Project"))
            {
                var found = new List<GameEventBase>();
                foreach (string guid in AssetDatabase.FindAssets("t:GameEventBase"))
                {
                    var gameEvent = AssetDatabase.LoadAssetAtPath<GameEventBase>(AssetDatabase.GUIDToAssetPath(guid));
                    if (gameEvent != null) found.Add(gameEvent);
                }
                found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

                serializedObject.Update();
                SerializedProperty list = serializedObject.FindProperty("events");
                list.arraySize = found.Count;
                for (int i = 0; i < found.Count; i++)
                {
                    list.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
                }
                serializedObject.ApplyModifiedProperties();
                catalog.RebuildLookup();
            }
            GUILayout.Label($"{catalog.Events.Count} event(s) resolvable by stable id.", EditorStyles.miniLabel);
        }
    }
}
