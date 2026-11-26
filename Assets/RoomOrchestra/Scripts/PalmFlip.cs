using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Story mode's Hold gesture (owner, 2026-11-22: flip the hand palm-down and back up). Fed the palm's own up axis
    /// each frame: a flip is the hand turning clearly over (the axis's height changes sign past <see cref="Clear"/>) and
    /// back within <see cref="Within"/> seconds. Which sign is "palm up" doesn't matter: over and back is the gesture.
    /// Pure logic, tested in EditMode.
    /// </summary>
    public sealed class PalmFlip
    {
        /// <summary>How far past level the hand must turn (the axis's height, -1..1) to count as over.</summary>
        public const float Clear = 0.45f;
        /// <summary>Seconds to turn over and back.</summary>
        public const float Within = 1.4f;

        int m_Side;          // +1 / -1: the side the hand last clearly showed; 0 unknown
        int m_Rest;          // the side it rests on (where it was before turning over)
        float m_OverAt = -1f;

        /// <summary>The axis's height now (for the log).</summary>
        public float Height { get; private set; }

        public void Reset() { m_Side = 0; m_Rest = 0; m_OverAt = -1f; }

        /// <summary>One frame: the palm's up axis (any space with y up) and the time. True once when a flip completes.</summary>
        public bool Step(Vector3 palmAxis, float now)
        {
            Height = palmAxis.normalized.y;
            int side = Height > Clear ? 1 : Height < -Clear ? -1 : 0;
            if (side == 0 || side == m_Side) return false;
            if (m_Side == 0) { m_Side = m_Rest = side; return false; }
            m_Side = side;
            if (side != m_Rest)
            {
                m_OverAt = now; // turned over
                return false;
            }
            bool flip = m_OverAt >= 0f && now - m_OverAt <= Within; // and back
            m_OverAt = -1f;
            return flip;
        }

        /// <summary>The hand was left turned over too long: that side is the new resting side.</summary>
        public void Settle(float now)
        {
            if (m_OverAt >= 0f && now - m_OverAt > Within) { m_Rest = m_Side; m_OverAt = -1f; }
        }
    }
}
