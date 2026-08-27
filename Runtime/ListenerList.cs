using System.Collections.Generic;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// Ordered listener storage shared by all event types, built for safe mutation
    /// mid-raise: removals during a raise null the slot (compacted after the raise
    /// unwinds) so iteration indices never shift, and additions append past the
    /// raise's count snapshot so new listeners never receive the raise that was
    /// already in flight. Duplicates are rejected. Zero allocation on the raise path.
    /// </summary>
    internal sealed class ListenerList<TDelegate> where TDelegate : class
    {
        private readonly List<TDelegate> slots = new List<TDelegate>();
        private int activeCount;
        private bool hasHoles;

        /// <summary>Live listeners (excludes holes left by mid-raise removals).</summary>
        public int Count => activeCount;

        /// <summary>Slot count to iterate over — capture BEFORE the raise loop so
        /// listeners added during the raise are excluded.</summary>
        public int SnapshotCount => slots.Count;

        /// <summary>Slot accessor for the raise loop; may be null (removed mid-raise).</summary>
        public TDelegate this[int index] => slots[index];

        /// <summary>Adds a listener; false if null or already subscribed.</summary>
        public bool Add(TDelegate listener)
        {
            if (listener == null || slots.Contains(listener)) return false;
            slots.Add(listener);
            activeCount++;
            return true;
        }

        /// <summary>Removes a listener; with <paramref name="deferred"/> the slot is
        /// nulled instead of removed so an in-flight raise keeps stable indices.</summary>
        public bool Remove(TDelegate listener, bool deferred)
        {
            int index = slots.IndexOf(listener);
            if (index < 0) return false;

            if (deferred)
            {
                slots[index] = null;
                hasHoles = true;
            }
            else
            {
                slots.RemoveAt(index);
            }
            activeCount--;
            return true;
        }

        /// <summary>Drops holes left by deferred removals. Call once no raise is in flight.</summary>
        public void Compact()
        {
            if (!hasHoles) return;
            slots.RemoveAll(IsNull);
            hasHoles = false;
        }

        private static bool IsNull(TDelegate slot) => slot == null;

        public void Clear()
        {
            slots.Clear();
            activeCount = 0;
            hasHoles = false;
        }
    }
}
