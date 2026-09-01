using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LiminalLabs.GameEvents.Tests
{
    /// <summary>
    /// Scene events, and the wiring discovery built on them.
    ///
    /// <b>What these cannot cover.</b> The load-bearing claim of the whole feature — that a
    /// scene-stored ScriptableObject survives a scene being saved and reopened — is not
    /// testable here, because it needs a scene written to disk and read back. That is what
    /// <c>Window > Liminal Labs > Game Events > Verify Scene Events Persist</c> is for, and it
    /// should be run after any Unity upgrade. These tests cover everything on this side of that
    /// question.
    /// </summary>
    public class SceneGameEventTests
    {
        private readonly List<Object> created = new List<Object>();

        private T Event<T>(string name) where T : GameEventBase
        {
            var instance = ScriptableObject.CreateInstance<T>();
            instance.name = name;
            created.Add(instance);
            return instance;
        }

        private GameObject Object_(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
                if (created[i] != null) Object.DestroyImmediate(created[i]);

            created.Clear();
        }

        // ---- hosting -------------------------------------------------------------------

        [Test]
        public void AHostStartsEmpty()
        {
            SceneGameEvent host = Object_("Host").AddComponent<SceneGameEvent>();

            Assert.IsFalse(host.HasChannel);
            Assert.IsNull(host.Channel);
        }

        [Test]
        public void AnAdoptedEventIsHeldAndReadsAsSceneStored()
        {
            SceneGameEvent host = Object_("Host").AddComponent<SceneGameEvent>();
            GameEvent channel = Event<GameEvent>("NorthDoor");

            host.Adopt(channel);

            Assert.IsTrue(host.HasChannel);
            Assert.AreEqual(channel, host.Channel);
            Assert.IsTrue(host.IsSceneStored,
                          "an instance with no asset path is stored in the scene, not the project");
        }

        /// <summary>An event made in code has no inspector visit to mint its id, and a bridge or
        /// a save that names events needs one from the moment it exists.</summary>
        [Test]
        public void AnAdoptedEventHasAStableId()
        {
            SceneGameEvent host = Object_("Host").AddComponent<SceneGameEvent>();
            GameEvent channel = Event<GameEvent>("NorthDoor");

            host.Adopt(channel);

            Assert.IsFalse(string.IsNullOrEmpty(channel.StableId),
                           "adopting mints the id an asset would get from its first inspection");
        }

        /// <summary>The only reliable way to tell a scene event from an asset is the absence of
        /// an asset path - the types are identical on purpose.</summary>
        [Test]
        public void AnEventWithNoAssetPathIsASceneEvent()
        {
            Assert.IsTrue(SceneGameEvent.IsSceneEvent(Event<GameEvent>("Made")));
            Assert.IsFalse(SceneGameEvent.IsSceneEvent(null), "and nothing is not a scene event");
        }

        [Test]
        public void CollectFindsHostsThatActuallyHoldSomething()
        {
            SceneGameEvent full = Object_("Full").AddComponent<SceneGameEvent>();
            full.Adopt(Event<GameEvent>("Wired"));

            Object_("Empty").AddComponent<SceneGameEvent>();

            var found = new List<SceneGameEvent>();
            int count = SceneGameEvent.Collect(found);

            Assert.AreEqual(found.Count, count, "the return is what was added, not what exists");
            Assert.Contains(full, found);

            for (int i = 0; i < found.Count; i++)
                Assert.IsTrue(found[i].HasChannel, "an empty host is not a channel");
        }

        [Test]
        public void HostOfFindsTheObjectAnEventLivesOn()
        {
            SceneGameEvent host = Object_("Host").AddComponent<SceneGameEvent>();
            GameEvent channel = Event<GameEvent>("Wired");
            host.Adopt(channel);

            Assert.AreEqual(host, SceneGameEvent.HostOf(channel));
            Assert.IsNull(SceneGameEvent.HostOf(Event<GameEvent>("Homeless")),
                          "an event nothing hosts has no host");
        }

        // ---- wiring discovery -----------------------------------------------------------

        /// <summary>
        /// A listener is found through what it declares, not through a guess about its type.
        /// </summary>
        [Test]
        public void AListenerIsFoundAsAListener()
        {
            StringGameEvent channel = Event<StringGameEvent>("Spoken");
            StringGameEventListener listener =
                Object_("Ear").AddComponent<StringGameEventListener>();

            Assert.IsTrue(GameEventWiring.Assign(listener, channel), "assignable");
            Assert.AreEqual(WiringRole.Listens, GameEventWiring.RoleOf(listener, channel));
        }

        /// <summary>A component that has nothing to do with the event says so.</summary>
        [Test]
        public void SomethingUnrelatedHasNoRole()
        {
            GameEvent channel = Event<GameEvent>("Unrelated");
            var light = Object_("Light").AddComponent<Light>();

            Assert.AreEqual(WiringRole.None, GameEventWiring.RoleOf(light, channel));
        }

        /// <summary>A host is where an event lives, not a participant in it - otherwise every
        /// channel would draw a wire to itself.</summary>
        [Test]
        public void AHostIsNotWiredToItsOwnEvent()
        {
            SceneGameEvent host = Object_("Host").AddComponent<SceneGameEvent>();
            GameEvent channel = Event<GameEvent>("Own");
            host.Adopt(channel);

            Assert.AreEqual(WiringRole.None, GameEventWiring.RoleOf(host, channel));
        }

        [Test]
        public void FindWiredSeparatesTheTwoSides()
        {
            StringGameEvent channel = Event<StringGameEvent>("Alarm");

            StringGameEventListener listener =
                Object_("Siren").AddComponent<StringGameEventListener>();
            GameEventWiring.Assign(listener, channel);

            var raisers = new List<Component>();
            var listeners = new List<Component>();
            GameEventWiring.FindWired(channel, raisers, listeners);

            Assert.Contains(listener, listeners);
            Assert.IsFalse(raisers.Contains(listener), "and not counted on both sides");
        }

        // ---- assignment, which is where the subtle bug lives -----------------------------

        /// <summary>
        /// A field will not take an event of the wrong type.
        ///
        /// The failure this prevents is nasty: Unity silently discards a mismatched object
        /// reference, so a wiring tool would report a connection it did not make and the
        /// designer would be left with a switch that does nothing and a wire that says it
        /// should.
        /// </summary>
        [Test]
        public void AFieldRefusesAnEventOfTheWrongType()
        {
            StringGameEventListener listener =
                Object_("Ear").AddComponent<StringGameEventListener>();

            Assert.IsFalse(GameEventWiring.Assign(listener, Event<FloatGameEvent>("Number")),
                           "a float event does not belong in a string listener");
            Assert.IsFalse(GameEventWiring.References(listener, Event<FloatGameEvent>("Number")));
        }

        /// <summary>A slot that is already wired is not quietly overwritten - the second drag
        /// finds nowhere to go, which is a real answer.</summary>
        [Test]
        public void AFilledSlotIsNotOverwritten()
        {
            StringGameEventListener listener =
                Object_("Ear").AddComponent<StringGameEventListener>();

            StringGameEvent first = Event<StringGameEvent>("First");
            StringGameEvent second = Event<StringGameEvent>("Second");

            Assert.IsTrue(GameEventWiring.Assign(listener, first));
            Assert.IsFalse(GameEventWiring.Assign(listener, second), "no free slot left");
            Assert.IsTrue(GameEventWiring.References(listener, first), "and the first survived");
        }

        /// <summary>
        /// The rule that makes drag-to-connect reuse a channel rather than breed them.
        /// </summary>
        [Test]
        public void SoleChannelIsTheOneWhenThereIsExactlyOne()
        {
            StringGameEventListener listener =
                Object_("Ear").AddComponent<StringGameEventListener>();

            Assert.IsNull(GameEventWiring.SoleChannelOf(listener), "nothing wired yet");

            StringGameEvent channel = Event<StringGameEvent>("Only");
            GameEventWiring.Assign(listener, channel);

            Assert.AreEqual(channel, GameEventWiring.SoleChannelOf(listener));
        }

        /// <summary>Two different events means there is no single obvious one to join, and
        /// guessing would wire a switch to the wrong door.</summary>
        [Test]
        public void SoleChannelIsNothingWhenThereAreTwo()
        {
            GameEventListener listener = Object_("Ear").AddComponent<GameEventListener>();

            var serialized = new UnityEditor.SerializedObject(listener);
            UnityEditor.SerializedProperty bindings = serialized.FindProperty("bindings");

            if (bindings == null || !bindings.isArray)
            {
                Assert.Ignore("GameEventListener no longer keeps a bindings array; " +
                              "this test needs rewriting against its new shape.");
                return;
            }

            bindings.arraySize = 2;
            serialized.ApplyModifiedProperties();

            GameEventWiring.Assign(listener, Event<GameEvent>("One"));
            GameEventWiring.Assign(listener, Event<GameEvent>("Two"));

            Assert.IsNull(GameEventWiring.SoleChannelOf(listener),
                          "two different events is not one obvious channel");
        }
    }
}
