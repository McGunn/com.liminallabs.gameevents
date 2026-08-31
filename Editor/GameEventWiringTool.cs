using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// Seeing and making event wiring in the scene, where the level is.
    ///
    /// <b>Why in the scene view rather than a window.</b> The thing a level designer wants to
    /// know is "what does <i>that</i> switch open" — and "that" is a thing they are looking at,
    /// behind a pillar, across the room. A window makes them translate between a list of names
    /// and a space; the scene view is already the space.
    ///
    /// <b>Why it is selection-scoped.</b> Drawing every wire at once is how tools like this
    /// become unusable — fifty objects is fifty curves and the level disappears behind its own
    /// diagram. So nothing is drawn until something is selected, and then only what that
    /// selection touches, one hop out.
    ///
    /// <b>What it will not pretend to know.</b> Listeners are discoverable, because a listener
    /// holds a serialized reference. Raisers are not: any code may call <c>Raise()</c>. So a
    /// component that has declared itself through <see cref="IGameEventRaiserInfo"/> is drawn
    /// solid, a component that merely holds a reference is drawn dashed, and an event raised
    /// from code nothing declared is invisible here — which is why the wire pulses when the
    /// event actually fires in play mode. A wire you never drew, lighting up, is the tool
    /// telling you the truth it could not find statically.
    /// </summary>
    [EditorTool("Game Event Wiring")]
    public sealed class GameEventWiringTool : EditorTool
    {
        private static readonly List<SceneGameEvent> hosts = new List<SceneGameEvent>();
        private static readonly List<Component> raisers = new List<Component>();
        private static readonly List<Component> listeners = new List<Component>();
        private static readonly List<GameEventBase> channels = new List<GameEventBase>();

        private GameObject dragFrom;
        private GUIContent icon;

        public override GUIContent toolbarIcon =>
            icon ?? (icon = new GUIContent(
                EditorGUIUtility.IconContent("d_Linked").image,
                "Game Event Wiring — drag between objects to connect them."));

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView)) return;

            hosts.Clear();
            SceneGameEvent.Collect(hosts);

            DrawForSelection();
            HandleDrag();
        }

        // ---- drawing -------------------------------------------------------------------

        /// <summary>
        /// Every wire the current selection is part of, and nothing else.
        ///
        /// One hop: what this raises, and who hears it. Not the whole graph, because the whole
        /// graph is the thing nobody can read.
        /// </summary>
        private void DrawForSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null) return;

            channels.Clear();
            ChannelsTouchedBy(selected, channels);

            for (int i = 0; i < channels.Count; i++) DrawChannel(channels[i], selected);
        }

        private void ChannelsTouchedBy(GameObject subject, List<GameEventBase> results)
        {
            Component[] onIt = subject.GetComponents<Component>();

            for (int i = 0; i < onIt.Length; i++)
            {
                if (onIt[i] is SceneGameEvent host)
                {
                    if (host.HasChannel && !results.Contains(host.Channel)) results.Add(host.Channel);
                    continue;
                }

                GameEventBase found = GameEventWiring.SoleChannelOf(onIt[i]);
                if (found != null && !results.Contains(found)) results.Add(found);
            }
        }

        private void DrawChannel(GameEventBase channel, GameObject selected)
        {
            raisers.Clear();
            listeners.Clear();
            GameEventWiring.FindWired(channel, raisers, listeners);

            SceneGameEvent host = SceneGameEvent.HostOf(channel, hosts);

            // Where the wires meet. A hosted channel has a place of its own; a project asset
            // does not, so its wires converge above whatever is selected - which is honest,
            // because a global event genuinely is not anywhere in this level.
            Vector3 hub = host != null
                ? host.transform.position
                : selected.transform.position + Vector3.up * 2.5f;

            float alpha = host != null ? 1f : 0.75f;

            DrawHub(channel, host, hub, alpha);

            for (int i = 0; i < raisers.Count; i++)
            {
                bool declared = raisers[i] is IGameEventRaiserInfo;
                DrawWire(raisers[i].transform.position, hub,
                         GameEventWiring.RaiserColour, alpha, declared, channel);
            }

            for (int i = 0; i < listeners.Count; i++)
            {
                DrawWire(hub, listeners[i].transform.position,
                         GameEventWiring.ListenerColour, alpha, true, channel);
            }
        }

        private static void DrawHub(GameEventBase channel, SceneGameEvent host, Vector3 at,
                                    float alpha)
        {
            Color colour = host != null ? Color.white : new Color(0.7f, 0.85f, 1f);
            colour.a = alpha;

            Handles.color = colour;
            float size = HandleUtility.GetHandleSize(at) * 0.08f;
            Handles.SphereHandleCap(0, at, Quaternion.identity, size, EventType.Repaint);

            var label = new GUIContent(channel.name +
                                       (host == null ? "   (global)" : string.Empty));

            Handles.Label(at + Vector3.up * size * 2f, label, Style(colour));
        }

        /// <summary>
        /// One wire.
        ///
        /// Curved rather than straight so that two objects at the same height do not draw a
        /// line through the geometry between them, and so overlapping wires stay tellable
        /// apart. Dashed when the raiser never declared itself - see the note on the class.
        /// </summary>
        private static void DrawWire(Vector3 from, Vector3 to, Color colour, float alpha,
                                     bool declared, GameEventBase channel)
        {
            Color drawn = colour;
            drawn.a = alpha;

            // Fires recently are worth seeing. The Board already records every raise with its
            // frame, so a wire can light up as the event actually travels - which is the only
            // way to see a raiser that lives in code and declares nothing.
            if (Application.isPlaying && channel.LastRaiseFrame >= 0)
            {
                int age = Time.frameCount - channel.LastRaiseFrame;
                if (age >= 0 && age < 30)
                {
                    float heat = 1f - (age / 30f);
                    drawn = Color.Lerp(drawn, Color.white, heat);
                }
            }

            Handles.color = drawn;

            Vector3 lift = Vector3.up * Mathf.Max(0.35f, Vector3.Distance(from, to) * 0.18f);

            if (declared)
            {
                Handles.DrawBezier(from, to, from + lift, to + lift, drawn, null, 3f);
                return;
            }

            Handles.DrawDottedLine(from, to, 4f);
        }

        private static GUIStyle Style(Color colour)
        {
            var style = new GUIStyle(EditorStyles.miniLabel);
            style.normal.textColor = colour;
            return style;
        }

        // ---- connecting ------------------------------------------------------------------

        /// <summary>
        /// Drag from one object to another to wire them.
        ///
        /// The whole reason the tool exists: creating a channel by hand is four steps and a
        /// UnityEvent is one drag, so a channel that also costs one drag is the only version
        /// anybody will use.
        /// </summary>
        private void HandleDrag()
        {
            Event current = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);

            switch (current.GetTypeForControl(control))
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(control);
                    break;

                case EventType.MouseDown:
                    if (current.button != 0) break;

                    dragFrom = HandleUtility.PickGameObject(current.mousePosition, false);
                    if (dragFrom != null)
                    {
                        GUIUtility.hotControl = control;
                        current.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != control || dragFrom == null) break;

                    DrawPending(current.mousePosition);
                    current.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != control) break;

                    GameObject onto = HandleUtility.PickGameObject(current.mousePosition, false);
                    if (dragFrom != null && onto != null && onto != dragFrom) Connect(dragFrom, onto);
                    else if (dragFrom != null && onto == dragFrom) Selection.activeGameObject = dragFrom;

                    dragFrom = null;
                    GUIUtility.hotControl = 0;
                    current.Use();
                    break;
            }
        }

        private void DrawPending(Vector2 mouse)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
            Vector3 from = dragFrom.transform.position;
            Vector3 to = ray.GetPoint(HandleUtility.GetHandleSize(from) * 6f);

            Handles.color = GameEventWiring.RaiserColour;
            Handles.DrawDottedLine(from, to, 3f);

            SceneView.RepaintAll();
        }

        /// <summary>
        /// Wire two objects together, reusing a channel when there is one to reuse.
        ///
        /// The reuse rule is what makes many-to-many fall out naturally: a second switch
        /// dragged onto the same door joins the door's existing channel instead of starting a
        /// private one. Without it the tool would quietly build a channel per pair, which is a
        /// UnityEvent with more steps and none of the benefit.
        /// </summary>
        private static void Connect(GameObject from, GameObject to)
        {
            GameEventBase channel = ExistingChannel(to) ?? ExistingChannel(from);
            bool created = false;

            if (channel == null)
            {
                channel = CreateHostedChannel(from, to);
                created = true;
            }

            if (channel == null) return;

            bool wiredRaiser = AssignToAny(from, channel);
            bool wiredListener = AssignToAny(to, channel);

            if (wiredRaiser || wiredListener) return;

            // Nothing on either object had a free slot for it. Say so rather than leaving the
            // designer to wonder why the drag did nothing - and clean up a channel that was
            // created purely for a connection that could not be made.
            if (created)
            {
                SceneGameEvent host = SceneGameEvent.HostOf(channel);
                if (host != null) Undo.DestroyObjectImmediate(host.gameObject);
            }

            Debug.LogWarning(
                $"[GameEvents] Nothing on '{from.name}' or '{to.name}' has a free event slot " +
                "to wire. Add a Game Event Listener to the receiving object, or a component " +
                "that raises an event to the sending one.", to);
        }

        private static GameEventBase ExistingChannel(GameObject subject)
        {
            Component[] onIt = subject.GetComponents<Component>();

            for (int i = 0; i < onIt.Length; i++)
            {
                if (onIt[i] is SceneGameEvent host && host.HasChannel) return host.Channel;

                GameEventBase found = GameEventWiring.SoleChannelOf(onIt[i]);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>Make a channel, on its own object, placed between the two things it
        /// joins so the wire reads as a path rather than a detour.</summary>
        private static GameEventBase CreateHostedChannel(GameObject from, GameObject to)
        {
            var go = new GameObject($"{from.name} to {to.name}");
            Undo.RegisterCreatedObjectUndo(go, "Create Scene Event");

            go.transform.position =
                Vector3.Lerp(from.transform.position, to.transform.position, 0.5f) +
                Vector3.up * 0.75f;

            SceneGameEvent host = Undo.AddComponent<SceneGameEvent>(go);

            var channel = ScriptableObject.CreateInstance<GameEvent>();
            channel.name = go.name;
            Undo.RegisterCreatedObjectUndo(channel, "Create Scene Event");

            host.Adopt(channel);
            EditorUtility.SetDirty(host);

            return channel;
        }

        private static bool AssignToAny(GameObject subject, GameEventBase channel)
        {
            Component[] onIt = subject.GetComponents<Component>();

            for (int i = 0; i < onIt.Length; i++)
            {
                if (onIt[i] is SceneGameEvent) continue;
                if (GameEventWiring.Assign(onIt[i], channel)) return true;
            }

            return false;
        }
    }
}
