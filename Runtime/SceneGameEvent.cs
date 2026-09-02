using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// A game event that belongs to one scene instead of to the project.
    ///
    /// <b>It is not a different kind of event.</b> The thing this holds is an ordinary
    /// <see cref="GameEventBase"/> — the same type an asset is, with the same raise contract,
    /// the same listener list, the same diagnostics. The only difference is where the instance
    /// is stored: inside the scene file rather than in <c>Assets/</c>.
    ///
    /// That matters more than it sounds, because it means every field that points at an event
    /// stays exactly as it was. A listener still declares <c>[SerializeField] GameEvent</c> and
    /// a designer drags either kind into it. There is no interface, no
    /// <c>UnityEngine.Object</c> field, no drawer papering over a type hole, and nothing in the
    /// package that has to ask which sort of event it is holding.
    ///
    /// <b>What this component adds is a place.</b> A ScriptableObject has no transform and does
    /// not appear in the Hierarchy, so a scene-stored one would be invisible — impossible to
    /// select, name, or draw a wire to. Hanging it on a GameObject gives it somewhere to be,
    /// which is what lets the scene view show a switch, a door, and the channel between them as
    /// three things in space rather than two things and an inference.
    ///
    /// <b>Why scene-stored at all.</b> A project asset per door per level is clutter that
    /// outlives the level, and it does not survive the one operation level designers do most:
    /// duplicating a scene. Copy a level and its scene events copy with it, independently
    /// wired, because they live in the file that was copied.
    ///
    /// It also cannot accumulate the classic stale-subscriber bug an asset can. An asset
    /// persists between play sessions and can carry listeners from the last one; this is
    /// rebuilt from the scene every load.
    ///
    /// While its host is enabled, a scene event resolves by stable id through
    /// <see cref="GameEventRegistry"/>, beside the project's catalogued assets - so a bridge
    /// or a save that names it finds it exactly while the level is loaded.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Game Events/Scene Game Event")]
    [DisallowMultipleComponent]
    public sealed class SceneGameEvent : MonoBehaviour
    {
        [SerializeField, Tooltip("The event itself. Created for you - use the button in the " +
                                 "inspector rather than dragging a project asset in, or this " +
                                 "becomes a second name for a global event.")]
        private GameEventBase channel;

        [SerializeField, TextArea(2, 4), Tooltip("What this wire means. Shown in the Events " +
                                                 "Board and when the scene view draws it.")]
        private string notes;

        // Whether the channel is stored in this scene, written down at authoring time.
        //
        // The editor can always tell (a scene-stored event has no asset path) and a player
        // never can, so this is decided where the knowledge exists and carried into the
        // build. Without it a player would treat every hosted event as a project asset and
        // never release one - the runtime half of OnDestroy would be editor-only in practice.
        [SerializeField, HideInInspector]
        private bool ownsChannel;

        // Whether this host registered its event, so it withdraws only what it registered.
        private bool registered;

        /// <summary>The event this hosts. Null until one is created, which the inspector does
        /// on your behalf.</summary>
        public GameEventBase Channel => channel;

        /// <summary>Whether this is carrying anything.</summary>
        public bool HasChannel => channel != null;

        /// <summary>What this wire is for, in a designer's words.</summary>
        public string Notes => notes;

        /// <summary>
        /// Whether the event this holds lives in a scene rather than in the project.
        ///
        /// <b>The distinction that matters everywhere else.</b> In the editor it is read off the
        /// asset database - a scene-stored ScriptableObject has no asset path, and that is the
        /// only reliable way to tell the two apart, the type being identical by design. A player
        /// has no asset database, so it reads what the editor wrote down when the event was
        /// adopted or the scene was last validated.
        /// </summary>
        public bool IsSceneStored
        {
            get
            {
#if UNITY_EDITOR
                return IsSceneEvent(channel);
#else
                return ownsChannel && channel != null;
#endif
            }
        }

        /// <summary>
        /// Whether an event is stored in a scene rather than as a project asset.
        ///
        /// Editor-only knowledge, so it answers false in a build - where the question cannot
        /// arise for an arbitrary event, because nothing is authoring anything. A host knows
        /// about its own event through <see cref="IsSceneStored"/>, in a build too.
        /// </summary>
        public static bool IsSceneEvent(GameEventBase gameEvent)
        {
#if UNITY_EDITOR
            return gameEvent != null &&
                   string.IsNullOrEmpty(UnityEditor.AssetDatabase.GetAssetPath(gameEvent));
#else
            return false;
#endif
        }

        /// <summary>
        /// Every scene event in the loaded scenes, appended to <paramref name="results"/>.
        ///
        /// Used by the Board and the scene-view wiring, both of which previously could only see
        /// project assets and would therefore have shown a level designer exactly none of their
        /// own wiring.
        /// </summary>
        public static int Collect(List<SceneGameEvent> results)
        {
            // Version-gated the same way the rest of the project does it: the sort-mode
            // overloads were deprecated in 6000.3 and the replacements do not exist before it,
            // and this package supports 6000.0 up.
            SceneGameEvent[] found =
#if UNITY_6000_3_OR_NEWER
                FindObjectsByType<SceneGameEvent>(FindObjectsInactive.Include);
#else
                FindObjectsByType<SceneGameEvent>(FindObjectsInactive.Include,
                                                  FindObjectsSortMode.None);
#endif

            int added = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (!found[i].HasChannel) continue;

                results.Add(found[i]);
                added++;
            }

            return added;
        }

        /// <summary>
        /// The host of a given event, or null if it is an asset or nothing hosts it.
        ///
        /// Wanted by anything drawing a wire: an event knows its listeners but not where it is,
        /// and where it is, is this component.
        /// </summary>
        public static SceneGameEvent HostOf(GameEventBase gameEvent)
        {
            if (gameEvent == null) return null;

            // Its own list rather than a fresh one, because the scene view asks this while
            // drawing - once per wire, every repaint - and a scan that allocates there is a
            // tool that makes the editor stutter the moment a level gets big.
            lookup.Clear();
            Collect(lookup);

            return HostOf(gameEvent, lookup);
        }

        /// <summary>The host of an event, searched within hosts the caller already
        /// collected. Prefer this in a loop.</summary>
        public static SceneGameEvent HostOf(GameEventBase gameEvent,
                                            List<SceneGameEvent> among)
        {
            if (gameEvent == null || among == null) return null;

            for (int i = 0; i < among.Count; i++)
                if (among[i] != null && among[i].channel == gameEvent) return among[i];

            return null;
        }

        private static readonly List<SceneGameEvent> lookup = new List<SceneGameEvent>();

        /// <summary>
        /// Give this host an event.
        ///
        /// <b>Authoring only.</b> An event created while the game is running is never written
        /// to the scene, so it would work for the session and be gone on the next load, taking
        /// every reference to it with it. That is a silent failure, so it is refused loudly
        /// instead - the inspector and the wiring tool are the supported ways in.
        ///
        /// The event gets its stable id here, on the way in. An asset is minted one when it is
        /// first inspected, but an event made in code is inspected when someone happens to
        /// click it, and a save or a bridge that named it before then would have had nothing
        /// to name it by.
        /// </summary>
        public void Adopt(GameEventBase created)
        {
            if (Application.isPlaying)
            {
                Debug.LogError(
                    $"[GameEvents] '{name}' was given an event at runtime. Scene events are " +
                    "authored, not created while playing - this one would not be saved and " +
                    "would be null on the next load. Ignored.", this);
                return;
            }

            channel = created;
            ownsChannel = created != null;
            if (created != null) created.EnsureStableId();
        }

#if UNITY_EDITOR
        // Kept true to the asset database whenever the editor looks at this, so a host that
        // was pointed at a project asset by hand, or one saved before ownership was recorded,
        // carries the right answer into the next build.
        private void OnValidate()
        {
            bool owns = IsSceneEvent(channel);
            if (owns == ownsChannel) return;

            ownsChannel = owns;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        /// <summary>
        /// Registers a scene-stored event by its stable id for as long as this host is
        /// enabled, so the level's own events resolve through <see cref="GameEventRegistry"/>
        /// exactly while the level is loaded.
        ///
        /// A host pointed at a project asset registers nothing. That asset is a catalog's to
        /// register, and a host withdrawing it on disable would take it away from everyone.
        /// </summary>
        private void OnEnable()
        {
            registered = channel != null && IsSceneStored && GameEventRegistry.Register(channel);
        }

        private void OnDisable()
        {
            if (!registered) return;

            registered = false;
            GameEventRegistry.Unregister(channel);
        }

        private void OnDestroy()
        {
            // At runtime the event goes with its host: nothing outside this scene can reach a
            // scene-stored event, so a host destroyed mid-session would otherwise leave a
            // ScriptableObject alive with no way to reach or free it.
            //
            // In the editor it is deliberately left alone, for undo. Deleting the host and
            // undoing has to bring the wire back, and an event destroyed here would not
            // return. Nothing is leaked by waiting: an event nothing references any more is
            // simply not written the next time the scene is saved.
            if (channel == null || !Application.isPlaying) return;
            if (!IsSceneStored) return;

            Destroy(channel);
        }

        private void Reset() => notes = string.Empty;
    }
}
