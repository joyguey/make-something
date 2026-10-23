using System.Collections.Generic;

namespace RoomOrchestra
{
    /// <summary>
    /// Guarantees one intended action produces at most one musical event.
    /// Throws: every contact during one throw (bounces, ricochets onto other targets) shares an
    /// action id, and only the first accepted contact counts.
    /// Taps / presses: a repeat on the same key within <see cref="RepeatWindow"/> seconds is the same
    /// action (PICO Spatial Input can report one tap as two touches ~16 ms apart).
    /// </summary>
    public sealed class CollisionDeduplicator
    {
        const int k_MaxTrackedActions = 256;

        readonly HashSet<long> m_UsedActions = new();
        readonly Queue<long> m_Order = new();
        readonly Dictionary<string, double> m_LastRepeat = new();

        public double RepeatWindow { get; set; } = 0.2;

        /// <summary>True the first time an action produces an event; false for every later contact.</summary>
        public bool TryAcceptAction(long actionId)
        {
            if (!m_UsedActions.Add(actionId)) return false;
            m_Order.Enqueue(actionId);
            while (m_Order.Count > k_MaxTrackedActions)
                m_UsedActions.Remove(m_Order.Dequeue());
            return true;
        }

        public bool HasFired(long actionId) => m_UsedActions.Contains(actionId);

        /// <summary>False if <paramref name="key"/> was accepted less than <see cref="RepeatWindow"/> ago.</summary>
        public bool TryAcceptRepeat(string key, double now) => TryAcceptRepeat(key, now, RepeatWindow);

        /// <summary>False if <paramref name="key"/> was accepted less than <paramref name="window"/> seconds ago.</summary>
        public bool TryAcceptRepeat(string key, double now, double window)
        {
            // Tolerance so a repeat exactly one window later is accepted despite float rounding.
            if (m_LastRepeat.TryGetValue(key, out double last) && now - last < window - 1e-9)
                return false;
            m_LastRepeat[key] = now;
            return true;
        }

        public void Reset()
        {
            m_UsedActions.Clear();
            m_Order.Clear();
            m_LastRepeat.Clear();
        }
    }
}
