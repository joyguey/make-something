using System;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Story mode's flick (owner, 2026-11-22: flick the friend floating above the open left palm with the right hand).
    /// Fed the right hand's position every frame: a flick is a quick move (above <see cref="MinSpeed"/>) that starts
    /// near the palm friend (within <see cref="Reach"/>), and it fires once, toward where the hand is moving. A short
    /// cooldown stops one swipe firing twice. Pure logic (world positions in, a direction out), tested in EditMode.
    /// </summary>
    public sealed class FlickDetector
    {
        public const float MinSpeed = 1.1f, Reach = 0.22f, Cooldown = 0.4f, Window = 0.35f;

        Vector3 m_Last, m_Velocity;
        bool m_Have;
        float m_NearUntil = -1f, m_ReadyAt;

        /// <summary>The hand's smoothed speed now (m/s).</summary>
        public float Speed => m_Velocity.magnitude;

        /// <summary>One frame: the right hand's position, the palm friend's position, the time. True with the flick's
        /// direction when a flick fires.</summary>
        public bool Step(Vector3 hand, Vector3 friend, float now, float dt, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (dt <= 0f) return false;
            if (m_Have) m_Velocity = Vector3.Lerp(m_Velocity, (hand - m_Last) / dt, 0.6f);
            m_Last = hand; m_Have = true;
            if (Vector3.Distance(hand, friend) <= Reach) m_NearUntil = now + Window;
            if (now < m_ReadyAt || now > m_NearUntil || Speed < MinSpeed) return false;
            direction = m_Velocity.normalized;
            m_ReadyAt = now + Cooldown;
            m_NearUntil = -1f;
            return true;
        }

        /// <summary>Gently turns a flick toward the nearest target within <paramref name="maxDegrees"/> (aim assist).</summary>
        public static Vector3 Assist(Vector3 from, Vector3 direction, Vector3[] targets, float maxDegrees, float pull = 0.6f)
        {
            Vector3 best = direction; float bestAngle = maxDegrees;
            foreach (var t in targets)
            {
                var to = (t - from).normalized;
                float a = Vector3.Angle(direction, to);
                if (a < bestAngle) { bestAngle = a; best = to; }
            }
            return Vector3.Slerp(direction, best, pull).normalized;
        }
    }
}
