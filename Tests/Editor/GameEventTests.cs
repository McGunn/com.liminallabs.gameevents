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
    /// Pins the raise-safety contract of GameEventBase — subscription order,
    /// exception isolation, mid-raise mutation semantics, and the recursion guard.
    /// These are decided semantics: a red test is a bug or a breaking change.
    /// </summary>
    public class GameEventTests
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        private T Create<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            created.Add(instance);
            return instance;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object obj in created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            created.Clear();
        }

        [Test]
        public void Listeners_FireInSubscriptionOrder()
        {
            var gameEvent = Create<GameEvent>();
            var order = new List<int>();
            gameEvent.Subscribe(() => order.Add(1));
            gameEvent.Subscribe(() => order.Add(2));
            gameEvent.Subscribe(() => order.Add(3));

            gameEvent.Raise();
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, order);
        }

        [Test]
        public void DuplicateSubscribe_IsIgnoredWithWarning()
        {
            var gameEvent = Create<GameEvent>();
            int calls = 0;
            Action listener = () => calls++;

            gameEvent.Subscribe(listener);
            LogAssert.Expect(LogType.Warning, new Regex("duplicate subscribe"));
            gameEvent.Subscribe(listener);

            gameEvent.Raise();
            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, gameEvent.ListenerCount);
        }

        [Test]
        public void ThrowingListener_IsIsolated_RestStillFire()
        {
            var gameEvent = Create<GameEvent>();
            bool secondFired = false;
            gameEvent.Subscribe(() => throw new InvalidOperationException("boom"));
            gameEvent.Subscribe(() => secondFired = true);

            LogAssert.Expect(LogType.Error, new Regex("threw"));
            gameEvent.Raise();
            Assert.IsTrue(secondFired, "the listener after the throwing one must still run");
        }

        [Test]
        public void UnsubscribeDuringRaise_TakesEffectImmediately()
        {
            var gameEvent = Create<GameEvent>();
            bool lateFired = false;
            Action late = () => lateFired = true;
            gameEvent.Subscribe(() => gameEvent.Unsubscribe(late));
            gameEvent.Subscribe(late);

            gameEvent.Raise();
            Assert.IsFalse(lateFired, "a listener removed mid-raise (before its turn) must not fire");
            Assert.AreEqual(1, gameEvent.ListenerCount);
        }

        [Test]
        public void SelfUnsubscribeDuringRaise_IsSafe()
        {
            var gameEvent = Create<GameEvent>();
            int calls = 0;
            Action once = null;
            once = () => { calls++; gameEvent.Unsubscribe(once); };
            gameEvent.Subscribe(once);

            gameEvent.Raise();
            gameEvent.Raise();
            Assert.AreEqual(1, calls, "a one-shot self-removing listener fires exactly once");
            Assert.AreEqual(0, gameEvent.ListenerCount);
        }

        [Test]
        public void SubscribeDuringRaise_TakesEffectNextRaise()
        {
            var gameEvent = Create<GameEvent>();
            int newListenerCalls = 0;
            Action added = () => newListenerCalls++;
            gameEvent.Subscribe(() => gameEvent.Subscribe(added));

            gameEvent.Raise();
            Assert.AreEqual(0, newListenerCalls, "a listener added mid-raise must not receive the in-flight raise");

            LogAssert.Expect(LogType.Warning, new Regex("duplicate subscribe"));
            gameEvent.Raise();   // outer listener re-subscribes 'added' (duplicate, warned)
            Assert.AreEqual(1, newListenerCalls);
        }

        [Test]
        public void RecursiveRaise_IsCutOffAtMaxDepth()
        {
            var gameEvent = Create<GameEvent>();
            int calls = 0;
            gameEvent.Subscribe(() => { calls++; gameEvent.Raise(); });

            LogAssert.Expect(LogType.Error, new Regex("exceeded raise depth"));
            gameEvent.Raise();
            Assert.AreEqual(GameEventBase.MaxRaiseDepth, calls, "recursion stops at the depth guard, no stack overflow");
        }

        [Test]
        public void TypedEvent_DeliversPayload()
        {
            var gameEvent = Create<FloatGameEvent>();
            float received = 0f;
            gameEvent.Subscribe(value => received = value);

            gameEvent.Raise(3.5f);
            Assert.AreEqual(3.5f, received);
        }

        [Test]
        public void RaiseFromInspector_UsesSerializedDebugValue()
        {
            var gameEvent = Create<IntGameEvent>();
            typeof(GameEvent<int>)
                .GetField("debugValue", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(gameEvent, 42);

            int received = 0;
            gameEvent.Subscribe(value => received = value);
            gameEvent.RaiseFromInspector();
            Assert.AreEqual(42, received);
        }

        [Test]
        public void UnsubscribeNeverSubscribed_IsANoOp()
        {
            var gameEvent = Create<GameEvent>();
            gameEvent.Unsubscribe(() => { });
            gameEvent.Raise();   // no listeners: still fine
            Assert.AreEqual(0, gameEvent.ListenerCount);
        }

        [Test]
        public void Catalog_ResolvesByStableId()
        {
            var a = Create<GameEvent>();
            var b = Create<FloatGameEvent>();
            typeof(GameEventBase).GetField("stableId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(a, "id-a");
            typeof(GameEventBase).GetField("stableId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(b, "id-b");
            Assert.AreEqual("id-a", a.StableId);

            var catalog = Create<GameEventCatalog>();
            typeof(GameEventCatalog).GetField("events", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(catalog, new List<GameEventBase> { a, b, null });
            catalog.RebuildLookup();

            Assert.IsTrue(catalog.TryGet("id-b", out GameEventBase resolved));
            Assert.AreEqual(b, resolved);
            Assert.IsFalse(catalog.TryGet("missing", out _));
            Assert.IsFalse(catalog.TryGet(null, out _));
        }

        [Test]
        public void RaiseBookkeeping_TracksCountAndListeners()
        {
            var gameEvent = Create<GameEvent>();
            Action a = () => { }, b = () => { };
            gameEvent.Subscribe(a);
            gameEvent.Subscribe(b);
            Assert.AreEqual(2, gameEvent.ListenerCount);

            gameEvent.Raise();
            gameEvent.Raise();
            Assert.AreEqual(2, gameEvent.TotalRaiseCount);

            gameEvent.Unsubscribe(a);
            Assert.AreEqual(1, gameEvent.ListenerCount);
        }
    }
}
