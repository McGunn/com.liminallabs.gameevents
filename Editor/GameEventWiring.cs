using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>How a component relates to an event.</summary>
    public enum WiringRole
    {
        /// <summary>Nothing to do with it.</summary>
        None = 0,

        /// <summary>Declared, via <see cref="IGameEventRaiserInfo"/>.</summary>
        Raises = 1,

        /// <summary>Declared, via <see cref="IGameEventListenerInfo"/>.</summary>
        Listens = 2,

        /// <summary>
        /// Holds a serialized reference to the event but declares nothing.
        ///
        /// Drawn differently everywhere, because the difference between "this raises the event"
        /// and "this mentions the event" is exactly the difference a wiring diagram must not
        /// blur. Most of these are raisers; some are code that only reads the reference.
        /// </summary>
        Mentions = 3,
    }

    /// <summary>
    /// Finding what is wired to what, and wiring it.
    ///
    /// Shared by the inspector and the scene-view tool so both agree, and editor-only because
    /// all of it reads serialized data rather than running anything.
    /// </summary>
    public static class GameEventWiring
    {
        /// <summary>Warm, for the side that sends.</summary>
        public static readonly Color RaiserColour = new Color(1f, 0.62f, 0.24f);

        /// <summary>Cool, for the side that receives.</summary>
        public static readonly Color ListenerColour = new Color(0.36f, 0.76f, 1f);

        /// <summary>Muted, for a reference nobody declared the meaning of.</summary>
        public static readonly Color MentionColour = new Color(0.62f, 0.62f, 0.66f);

        private static readonly List<GameEventBase> observed = new List<GameEventBase>();
        private static readonly List<Component> components = new List<Component>();

        /// <summary>The colour a role is drawn in.</summary>
        public static Color ColourOf(WiringRole role)
        {
            switch (role)
            {
                case WiringRole.Raises: return RaiserColour;
                case WiringRole.Listens: return ListenerColour;
                default: return MentionColour;
            }
        }

        /// <summary>
        /// How a component relates to an event, if at all.
        ///
        /// Declarations are believed first, because a component that says what it does is
        /// telling the truth about itself. Only when nothing is declared does this fall back to
        /// reading serialized fields, and it says so by answering
        /// <see cref="WiringRole.Mentions"/> rather than guessing a direction.
        /// </summary>
        public static WiringRole RoleOf(Component component, GameEventBase channel)
        {
            if (component == null || channel == null) return WiringRole.None;

            if (component is IGameEventRaiserInfo raiser)
            {
                observed.Clear();
                raiser.GetRaisedEvents(observed);
                if (observed.Contains(channel)) return WiringRole.Raises;
            }

            if (component is IGameEventListenerInfo listener)
            {
                observed.Clear();
                listener.GetObservedEvents(observed);
                if (observed.Contains(channel)) return WiringRole.Listens;
            }

            return References(component, channel) ? WiringRole.Mentions : WiringRole.None;
        }

        /// <summary>
        /// Whether a component holds this event in any serialized field.
        ///
        /// Walks the serialized data rather than reflecting over the type, so it finds fields
        /// on a game's own components, inside nested classes and inside arrays — wherever a
        /// designer could actually have dropped one.
        /// </summary>
        public static bool References(Component component, GameEventBase channel)
        {
            if (component == null || channel == null) return false;

            // A host is not wired to its own event; it is where the event lives. Drawing it as
            // a participant would put a wire from every channel to itself.
            if (component is SceneGameEvent) return false;

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

        /// <summary>
        /// Everything in the loaded scenes wired to an event, split by which side it is on.
        ///
        /// <see cref="WiringRole.Mentions"/> is reported as a raiser, because that is what it
        /// nearly always is - but the caller is told, so it can draw the difference.
        /// </summary>
        public static void FindWired(GameEventBase channel, List<Component> raisers,
                                     List<Component> listeners)
        {
            if (channel == null) return;

            components.Clear();
            CollectComponents(components);

            for (int i = 0; i < components.Count; i++)
            {
                switch (RoleOf(components[i], channel))
                {
                    case WiringRole.Raises:
                    case WiringRole.Mentions:
                        raisers.Add(components[i]);
                        break;

                    case WiringRole.Listens:
                        listeners.Add(components[i]);
                        break;
                }
            }
        }

        /// <summary>Every event any component in the loaded scenes refers to, scene-stored or
        /// not.</summary>
        public static void FindChannelsInUse(List<GameEventBase> results)
        {
            components.Clear();
            CollectComponents(components);

            for (int i = 0; i < components.Count; i++)
            {
                Component component = components[i];
                if (component is SceneGameEvent host)
                {
                    if (host.HasChannel && !results.Contains(host.Channel)) results.Add(host.Channel);
                    continue;
                }

                using (var serialized = new SerializedObject(component))
                {
                    SerializedProperty property = serialized.GetIterator();

                    while (property.NextVisible(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;

                        if (property.objectReferenceValue is GameEventBase found &&
                            !results.Contains(found))
                        {
                            results.Add(found);
                        }
                    }
                }
            }
        }

        /// <summary>Every component in every loaded scene, including on inactive
        /// objects.</summary>
        public static void CollectComponents(List<Component> results)
        {
#if UNITY_6000_3_OR_NEWER
            Component[] all = Object.FindObjectsByType<Component>(FindObjectsInactive.Include);
#else
            Component[] all = Object.FindObjectsByType<Component>(FindObjectsInactive.Include,
                                                                 FindObjectsSortMode.None);
#endif

            for (int i = 0; i < all.Length; i++)
                if (all[i] != null) results.Add(all[i]);
        }

        /// <summary>
        /// The event a component is already wired to, if it has exactly one.
        ///
        /// <b>What makes drag-to-connect behave.</b> Dragging a second switch onto a door must
        /// join the channel that already exists rather than making another one - otherwise
        /// many-to-many degenerates into a channel per pair, which is a UnityEvent with extra
        /// ceremony. So the tool asks this first and only creates when the answer is nothing.
        /// </summary>
        public static GameEventBase SoleChannelOf(Component component)
        {
            if (component == null) return null;

            GameEventBase only = null;

            using (var serialized = new SerializedObject(component))
            {
                SerializedProperty property = serialized.GetIterator();

                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (!(property.objectReferenceValue is GameEventBase found)) continue;

                    // Two different events means there is no single obvious one to join, and
                    // guessing would silently wire a switch to the wrong door.
                    if (only != null && only != found) return null;

                    only = found;
                }
            }

            return only;
        }

        /// <summary>
        /// Point a component's first empty event field at a channel.
        ///
        /// Returns false when there is nowhere to put it, which is a real and common answer -
        /// a door with its one event slot already filled is not a failure, it is a door that is
        /// already wired.
        /// </summary>
        public static bool Assign(Component component, GameEventBase channel)
        {
            if (component == null || channel == null) return false;

            using (var serialized = new SerializedObject(component))
            {
                SerializedProperty property = serialized.GetIterator();

                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (property.objectReferenceValue != null) continue;
                    if (!AcceptsEvent(property, channel)) continue;

                    property.objectReferenceValue = channel;
                    serialized.ApplyModifiedProperties();

                    EditorUtility.SetDirty(component);
                    if (!Application.isPlaying)
                        EditorSceneManager.MarkSceneDirty(component.gameObject.scene);

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a field would actually hold this event.
        ///
        /// Checked against the declared field type rather than assumed: dropping a
        /// StringGameEvent into a field typed for a FloatGameEvent would be silently discarded
        /// by Unity, and a wiring tool that appeared to connect two things and did not is worse
        /// than one that refuses.
        /// </summary>
        private static bool AcceptsEvent(SerializedProperty property, GameEventBase channel)
        {
            string type = property.type;
            if (string.IsNullOrEmpty(type) || !type.StartsWith("PPtr<")) return false;

            // "PPtr<$StringGameEvent>" - the name between the marker and the bracket.
            int start = type.IndexOf('$');
            if (start < 0) return false;

            string wanted = type.Substring(start + 1).TrimEnd('>');
            System.Type actual = channel.GetType();

            while (actual != null)
            {
                if (actual.Name == wanted) return true;
                actual = actual.BaseType;
            }

            return false;
        }
    }
}
