using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// Proves, rather than assumes, that a scene-stored event survives being saved.
    ///
    /// <b>Why this exists.</b> Everything about scene events rests on one Unity behaviour: a
    /// ScriptableObject with no asset path, referenced by a component in a scene, is written
    /// into the scene file and comes back on load. That is a documented and widely used
    /// technique, and it is still an assumption — and it is the kind of assumption that fails
    /// quietly. If it did not hold, every scene event would work perfectly for a whole session
    /// and be null the next morning, with no error to explain it.
    ///
    /// So it is checked by doing it: write a scene to a temporary file with a hosted event in
    /// it, close it, open it back, and look. Under a minute, no manual steps, and it either
    /// says the ground is solid or tells you exactly what did not survive.
    ///
    /// Run it after a Unity upgrade too. This is precisely the sort of behaviour that changes
    /// under you without appearing in release notes.
    /// </summary>
    public static class SceneEventSelfCheck
    {
        private const string Folder = "Assets/LiminalLabsTemp";
        private const string ScenePath = Folder + "/SceneEventSelfCheck.unity";

        [MenuItem("Window/Liminal Labs/Game Events/Verify Scene Events Persist")]
        private static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[GameEvents] Self-check cancelled - nothing was changed.");
                return;
            }

            string previous = SceneManager.GetActiveScene().path;

            try
            {
                if (Check(out string detail)) Debug.Log("[GameEvents] " + detail);
                else Debug.LogError("[GameEvents] " + detail);
            }
            finally
            {
                Cleanup(previous);
            }
        }

        private static bool Check(out string detail)
        {
            Directory.CreateDirectory(Folder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                                                      NewSceneMode.Single);

            var go = new GameObject("SelfCheck Host");
            SceneGameEvent host = go.AddComponent<SceneGameEvent>();

            var channel = ScriptableObject.CreateInstance<StringGameEvent>();
            channel.name = "SelfCheckChannel";
            host.Adopt(channel);

            // A listener too, because the interesting question is not only whether the event
            // survives but whether the reference to it does. An event that comes back with
            // nothing pointing at it is no more use than one that does not come back.
            var listenerObject = new GameObject("SelfCheck Listener");
            StringGameEventListener listener =
                listenerObject.AddComponent<StringGameEventListener>();

            if (!GameEventWiring.Assign(listener, channel))
            {
                detail = "FAILED before saving: could not assign the event to a listener, " +
                         "which means the wiring helper is broken rather than the persistence.";
                return false;
            }

            EditorUtility.SetDirty(host);
            EditorUtility.SetDirty(listener);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                detail = "FAILED: the temporary scene could not be saved at all.";
                return false;
            }

            // Reopen from disk. Anything still in memory proves nothing; the question is
            // entirely about what the file kept.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            SceneGameEvent reopened = Object.FindAnyObjectByType<SceneGameEvent>();

            if (reopened == null)
            {
                detail = "FAILED: the host component did not survive, which means something " +
                         "much more basic is wrong than scene-stored ScriptableObjects.";
                return false;
            }

            if (!reopened.HasChannel)
            {
                detail =
                    "FAILED: the host came back but its event did not. Unity did not write " +
                    "the ScriptableObject into the scene file. Scene events cannot work in " +
                    "this Unity version as designed - the event would need to be an asset, or " +
                    "recreated and rebound on load.";
                return false;
            }

            if (reopened.Channel.name != "SelfCheckChannel")
            {
                detail = $"FAILED: the event came back named '{reopened.Channel.name}' rather " +
                         "than 'SelfCheckChannel', so its state did not survive intact.";
                return false;
            }

            StringGameEventListener reopenedListener =
                Object.FindAnyObjectByType<StringGameEventListener>();

            if (reopenedListener == null || !PointsAt(reopenedListener, reopened.Channel))
            {
                detail =
                    "PARTIAL: the event survived, but the listener's reference to it did not. " +
                    "The event is being written to the scene, and references into it are not " +
                    "being restored - wiring would come back empty.";
                return false;
            }

            detail = "Scene events persist correctly. The event survived a save and reload, " +
                     "kept its name, and the listener's reference to it was restored. The " +
                     "design is sound on this Unity version.";
            return true;
        }

        private static bool PointsAt(Component component, GameEventBase channel)
        {
            using (var serialized = new SerializedObject(component))
            {
                SerializedProperty property = serialized.GetIterator();

                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (ReferenceEquals(property.objectReferenceValue, channel)) return true;
                }
            }

            return false;
        }

        /// <summary>Put the editor back where it was, whatever happened.</summary>
        private static void Cleanup(string previous)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(ScenePath) != null)
                AssetDatabase.DeleteAsset(ScenePath);

            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);

            if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            else
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }
    }
}
