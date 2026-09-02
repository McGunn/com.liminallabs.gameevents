using System;
using System.Collections.Generic;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// Ordered listener storage shared by all event types.
    ///
    /// Order is by priority, highest first, and by subscription order within a priority -
    /// so the one listener that must see an event before the rest (a guard that changes
    /// state the others read, an analytics tap) asks for that, instead of the whole scene
    /// racing enable order to get it by accident.
    ///
    /// Built for safe mutation mid-raise: a removal during a raise nulls the slot (compacted
    /// once the raise unwinds) so the indices the raise is walking never shift, and an
    /// addition during a raise is held aside and merged into place afterwards - so a new
    /// listener neither receives the raise already in flight nor shifts it, however high its
    /// priority. Duplicates are rejected. Zero allocation on the raise path.
    /// </summary>
    internal sealed class ListenerList<TDelegate> where TDelegate : class
    {
        private struct Slot
        {
            public TDelegate Listener;
            public int Priority;
        }

        private static readonly Predicate<Slot> IsHole = slot => slot.Listener == null;

        private readonly List<Slot> slots = new List<Slot>();
        private readonly List<Slot> pending = new List<Slot>();
        private int activeCount;
        private bool hasHoles;

        /// <summary>Live listeners: excludes holes left by mid-raise removals, includes
        /// listeners added mid-raise that are still waiting to be merged.</summary>
        public int Count => activeCount;

        /// <summary>Slot count to iterate over — capture BEFORE the raise loop so
        /// listeners added during the raise are excluded.</summary>
        public int SnapshotCount => slots.Count;

        /// <summary>Slot accessor for the raise loop; may be null (removed mid-raise).</summary>
        public TDelegate this[int index] => slots[index].Listener;

        /// <summary>The priority a slot was subscribed with. For tooling.</summary>
        public int PriorityAt(int index) => slots[index].Priority;

        /// <summary>
        /// Adds a listener; false if null or already subscribed. With
        /// <paramref name="deferred"/> (a raise is in flight) the listener is held aside and
        /// merged into place by <see cref="Compact"/>, once the raise has unwound.
        /// </summary>
        public bool Add(TDelegate listener, int priority, bool deferred)
        {
            if (listener == null || Contains(listener)) return false;

            var slot = new Slot { Listener = listener, Priority = priority };
            if (deferred)
            {
                pending.Add(slot);
            }
            else
            {
                Compact();
                Insert(slot);
            }

            activeCount++;
            return true;
        }

        /// <summary>Removes a listener; with <paramref name="deferred"/> the slot is
        /// nulled instead of removed so an in-flight raise keeps stable indices.</summary>
        public bool Remove(TDelegate listener, bool deferred)
        {
            if (listener == null) return false;

            int index = IndexOf(slots, listener);
            if (index >= 0)
            {
                if (deferred)
                {
                    slots[index] = default;
                    hasHoles = true;
                }
                else
                {
                    slots.RemoveAt(index);
                }

                activeCount--;
                return true;
            }

            // Still waiting to be merged, so no raise is walking it and it can simply go.
            index = IndexOf(pending, listener);
            if (index < 0) return false;

            pending.RemoveAt(index);
            activeCount--;
            return true;
        }

        /// <summary>Drops holes left by deferred removals and merges deferred additions
        /// into place. Call once no raise is in flight.</summary>
        public void Compact()
        {
            if (hasHoles)
            {
                slots.RemoveAll(IsHole);
                hasHoles = false;
            }

            if (pending.Count == 0) return;

            for (int i = 0; i < pending.Count; i++) Insert(pending[i]);
            pending.Clear();
        }

        public void Clear()
        {
            slots.Clear();
            pending.Clear();
            activeCount = 0;
            hasHoles = false;
        }

        /// <summary>After the last live slot at this priority or higher: higher priorities
        /// run first, and equal priorities keep subscription order.</summary>
        private void Insert(Slot slot)
        {
            int at = slots.Count;
            while (at > 0)
            {
                Slot before = slots[at - 1];
                if (before.Listener != null && before.Priority >= slot.Priority) break;
                at--;
            }

            slots.Insert(at, slot);
        }

        private bool Contains(TDelegate listener) =>
            IndexOf(slots, listener) >= 0 || IndexOf(pending, listener) >= 0;

        /// <summary>Delegate equality, not reference equality: a method group written twice
        /// is two delegate objects naming one listener, and unsubscribing has to find it.</summary>
        private static int IndexOf(List<Slot> list, TDelegate listener)
        {
            for (int i = 0; i < list.Count; i++)
            {
                TDelegate candidate = list[i].Listener;
                if (candidate != null && candidate.Equals(listener)) return i;
            }

            return -1;
        }
    }
}
