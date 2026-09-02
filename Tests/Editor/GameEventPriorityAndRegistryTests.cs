using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LiminalLabs.GameEvents.Tests
{
    /// <summary>
    /// Two 0.6.0 contracts. Listener priority: higher first, subscription order within a
    /// priority, and a subscription made mid-raise still waits for the next raise however
    /// high it is. And the runtime registry: catalogs and scene hosts register what they
    /// have, an id resolves exactly while its event is alive, and a duplicate id is refused
    /// rather than silently rerouted.
    /// </summary>
    public class GameEventPriorityAndRegistryTests
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        private readonly List<GameEventBase> registered = new List<GameEventBase>();

        private T Create<T>(string name = null) where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            if (name != null) instance.name = name;
            created.Add(instance);
            return instance;
        }

        private GameObject Object_(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        /// <summary>An event with an id, as an inspected asset would have. Remembered so the
        /// registry is left as it was found, whatever a test did with it.</summary>
        private GameEvent Identified(string id)
        {
            GameEvent gameEvent = Create<GameEvent>(id);
            typeof(GameEventBase).GetField("stableId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(gameEvent, id);
            registered.Add(gameEvent);
            return gameEvent;
        }

        private GameEventCatalog Catalog(params GameEventBase[] events)
        {
            var catalog = Create<GameEventCatalog>();
            typeof(GameEventCatalog).GetField("events", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(catalog, new List<GameEventBase>(events));
            return catalog;
        }

        /// <summary>EditMode cannot enable a plain MonoBehaviour, so its lifecycle is driven by hand.</summary>
        private static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(target, null);

        [TearDown]
        public void TearDown()
        {
            foreach (GameEventBase gameEvent in registered) GameEventRegistry.Unregister(gameEvent);
            registered.Clear();

            foreach (UnityEngine.Object obj in created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            created.Clear();
        }

        // ---- priority ------------------------------------------------------------------

        [Test]
        public void Priority_RunsHigherFirst_AndKeepsSubscriptionOrderWithinAPriority()
        {
            var gameEvent = Create<GameEvent>();
            var order = new List<string>();
            gameEvent.Subscribe(() => order.Add("a0"));
            gameEvent.Subscribe(() => order.Add("b10"), 10);
            gameEvent.Subscribe(() => order.Add("c0"));
            gameEvent.Subscribe(() => order.Add("d-5"), -5);
            gameEvent.Subscribe(() => order.Add("e10"), 10);

            gameEvent.Raise();

            CollectionAssert.AreEqual(new[] { "b10", "e10", "a0", "c0", "d-5" }, order);
        }

        [Test]
        public void Priority_AppliesToTypedEvents()
        {
            var gameEvent = Create<FloatGameEvent>();
            var order = new List<string>();
            gameEvent.Subscribe(v => order.Add("plain " + v));
            gameEvent.Subscribe(v => order.Add("first " + v), 1);

            gameEvent.Raise(2f);

            CollectionAssert.AreEqual(new[] { "first 2", "plain 2" }, order);
        }

        [Test]
        public void HighPrioritySubscribeDuringRaise_WaitsForTheNextRaise_ThenRunsFirst()
        {
            var gameEvent = Create<GameEvent>();
            var order = new List<string>();
            Action late = () => order.Add("late");
            bool added = false;

            gameEvent.Subscribe(() =>
            {
                order.Add("first");
                if (added) return;
                added = true;
                gameEvent.Subscribe(late, 100);
            });
            gameEvent.Subscribe(() => order.Add("second"));

            gameEvent.Raise();
            CollectionAssert.AreEqual(new[] { "first", "second" }, order,
                "a listener added mid-raise never receives that raise, whatever its priority");
            Assert.AreEqual(3, gameEvent.ListenerCount, "but it is subscribed from the moment it asked");

            order.Clear();
            gameEvent.Raise();
            CollectionAssert.AreEqual(new[] { "late", "first", "second" }, order);
        }

        [Test]
        public void AListenerAddedAndRemovedDuringOneRaise_NeverRuns()
        {
            var gameEvent = Create<GameEvent>();
            int lateCalls = 0;
            Action late = () => lateCalls++;

            gameEvent.Subscribe(() =>
            {
                gameEvent.Subscribe(late, 5);
                gameEvent.Unsubscribe(late);
            });

            gameEvent.Raise();
            gameEvent.Raise();

            Assert.AreEqual(0, lateCalls);
            Assert.AreEqual(1, gameEvent.ListenerCount);
        }

        [Test]
        public void AHighPriorityListener_CanStopALowerOneFromSeeingThisRaise()
        {
            var gameEvent = Create<GameEvent>();
            int lowCalls = 0;
            Action low = () => lowCalls++;

            gameEvent.Subscribe(low);
            gameEvent.Subscribe(() => gameEvent.Unsubscribe(low), 10);

            gameEvent.Raise();

            Assert.AreEqual(0, lowCalls, "the guard ran first and unsubscribed it before its turn");
            Assert.AreEqual(1, gameEvent.ListenerCount);
        }

        [Test]
        public void DescribeListeners_MentionsANonZeroPriority()
        {
            var gameEvent = Create<GameEvent>();
            gameEvent.Subscribe(() => { });
            gameEvent.Subscribe(() => { }, 7);

            var described = new List<string>();
            gameEvent.DescribeListeners(described);

            Assert.AreEqual(2, described.Count);
            StringAssert.Contains("[priority 7]", described[0]);
            StringAssert.DoesNotContain("priority", described[1]);
        }

        // ---- registry ------------------------------------------------------------------

        [Test]
        public void Registry_ResolvesAnActivatedCatalog_AndForgetsADeactivatedOne()
        {
            GameEvent door = Identified("registry-door");
            GameEvent alarm = Identified("registry-alarm");
            GameEventCatalog catalog = Catalog(door, null, alarm);

            Assert.IsFalse(GameEventRegistry.TryResolve("registry-door", out _), "nothing until activated");

            Assert.AreEqual(2, catalog.Activate());
            Assert.IsTrue(catalog.IsActive);
            Assert.AreEqual(0, catalog.Activate(), "activating twice registers nothing twice");

            Assert.IsTrue(GameEventRegistry.TryResolve("registry-door", out GameEventBase found));
            Assert.AreSame(door, found);
            Assert.IsTrue(GameEventRegistry.TryResolve("registry-alarm", out found));
            Assert.AreSame(alarm, found);

            catalog.Deactivate();
            Assert.IsFalse(catalog.IsActive);
            Assert.IsFalse(GameEventRegistry.TryResolve("registry-door", out _));
            Assert.IsFalse(GameEventRegistry.TryResolve("registry-alarm", out _));
        }

        [Test]
        public void Registry_RefusesASecondEventWithTheSameId_AndKeepsTheFirst()
        {
            GameEvent original = Identified("registry-twin");
            GameEvent copy = Identified("registry-twin");

            Assert.IsTrue(GameEventRegistry.Register(original));
            Assert.IsTrue(GameEventRegistry.Register(original), "the same event again is fine");

            LogAssert.Expect(LogType.Error, new Regex("share the stable id"));
            Assert.IsFalse(GameEventRegistry.Register(copy));

            Assert.IsTrue(GameEventRegistry.TryResolve("registry-twin", out GameEventBase found));
            Assert.AreSame(original, found);

            Assert.IsFalse(GameEventRegistry.Unregister(copy), "only the registrant can withdraw an id");
            Assert.IsTrue(GameEventRegistry.TryResolve("registry-twin", out _));
        }

        [Test]
        public void Registry_RefusesAnEventWithNoId_AndSaysWhy()
        {
            var unidentified = Create<GameEvent>("Fresh");

            LogAssert.Expect(LogType.Warning, new Regex("no stable id"));
            Assert.IsFalse(GameEventRegistry.Register(unidentified));
            Assert.IsFalse(GameEventRegistry.TryResolve(null, out _));
            Assert.IsFalse(GameEventRegistry.TryResolve("", out _));
        }

        [Test]
        public void Registry_RaisesChanged_BothWays()
        {
            GameEvent gameEvent = Identified("registry-changed");
            var seen = new List<string>();
            Action<GameEventBase, bool> onChanged = (e, present) => seen.Add(e.name + (present ? "+" : "-"));
            GameEventRegistry.Changed += onChanged;

            try
            {
                GameEventRegistry.Register(gameEvent);
                GameEventRegistry.Register(gameEvent);
                GameEventRegistry.Unregister(gameEvent);
                GameEventRegistry.Unregister(gameEvent);
            }
            finally
            {
                GameEventRegistry.Changed -= onChanged;
            }

            CollectionAssert.AreEqual(new[] { "registry-changed+", "registry-changed-" }, seen);
        }

        [Test]
        public void AnEnabledHostRegistersItsSceneEvent_AndADisabledOneWithdrawsIt()
        {
            SceneGameEvent host = Object_("Host").AddComponent<SceneGameEvent>();
            GameEvent channel = Create<GameEvent>("NorthDoor");
            host.Adopt(channel);
            registered.Add(channel);
            Assert.IsFalse(string.IsNullOrEmpty(channel.StableId), "adoption minted an id to register by");

            Invoke(host, "OnEnable");
            Assert.IsTrue(GameEventRegistry.TryResolve(channel.StableId, out GameEventBase found));
            Assert.AreSame(channel, found);

            Invoke(host, "OnDisable");
            Assert.IsFalse(GameEventRegistry.TryResolve(channel.StableId, out _));
        }

        [Test]
        public void AnActivatorRegistersItsCatalogsWhileEnabled()
        {
            GameEvent gameEvent = Identified("registry-activator");
            GameEventCatalog catalog = Catalog(gameEvent);
            GameEventCatalogActivator activator = Object_("Bootstrap").AddComponent<GameEventCatalogActivator>();
            typeof(GameEventCatalogActivator).GetField("catalogs", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(activator, new List<GameEventCatalog> { catalog });

            Invoke(activator, "OnEnable");
            Assert.IsTrue(catalog.IsActive);
            Assert.IsTrue(GameEventRegistry.TryResolve("registry-activator", out _));

            Invoke(activator, "OnDisable");
            Assert.IsFalse(catalog.IsActive);
            Assert.IsFalse(GameEventRegistry.TryResolve("registry-activator", out _));
        }
    }
}
