using System;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>One frame of the right hand for a flick: the index fingertip, the thumb tip, the palm, and the friend.</summary>
    public struct FlickHand
    {
        public Vector3 Tip, Thumb, Palm, Friend;
    }

    /// <summary>
    /// Story mode's flick (owner, 2026-11-22: flick the friend on the open left palm with the right hand; headset
    /// 2026-11-25: "Do you know what a flick should look like?"). A real flick, as when flicking a marble: the index
    /// finger is <b>loaded</b> (curled, or held against the thumb) beside the friend, then <b>snaps out</b>, fast,
    /// relative to the hand itself; the hand barely moves. So it reads the fingertip's speed away from the palm, not the
    /// hand's speed through the room (a whole-hand swipe is not a flick). It fires once, in the direction the finger
    /// snapped. Then the finger must load again before the next flick (no double fires). A jump faster than
    /// <see cref="MaxSnap"/> is tracking catching up, never a flick. Pure logic, tested in EditMode.
    /// </summary>
    public sealed class FlickDetector
    {
        /// <summary>How near the friend the fingertip or thumb must be (m).</summary>
        public const float Reach = 0.17f;
        /// <summary>Loaded: the index tip within this of the thumb tip (m), or curled within <see cref="LoadCurl"/> of the palm.</summary>
        public const float LoadGap = 0.035f, LoadCurl = 0.075f;
        /// <summary>The snap: the fingertip moving away from the palm at least this fast (m/s).</summary>
        public const float MinSnap = 0.55f, MaxSnap = 6f;
        /// <summary>A snap counts within this long of the finger being loaded and near the friend (s).</summary>
        public const float LoadMemory = 0.35f, NearMemory = 0.25f, Cooldown = 0.3f;

        Vector3 m_LastRel, m_RelVelocity;
        bool m_Have, m_Armed = true, m_WasNear;
        float m_LoadedAt = -9f, m_NearAt = -9f, m_ReadyAt, m_Peak;

        /// <summary>The finger's speed away from the palm now (m/s; negative when curling in).</summary>
        public float Speed { get; private set; }
        /// <summary>The finger is loaded beside the friend: ready to flick (for the "ready" glow).</summary>
        public bool Near { get; private set; }
        /// <summary>False from a flick until the finger loads again.</summary>
        public bool Armed => m_Armed;
        /// <summary>Last frame's thumb gap and curl (m), for the log.</summary>
        public float Gap { get; private set; }
        public float Curl { get; private set; }

        /// <summary>Set when the finger was loaded beside the friend and left without a flick: its fastest snap (m/s).</summary>
        public float MissedPeak { get; private set; } = -1f;

        /// <summary>No hand this frame: forget the motion, so its return isn't a jump.</summary>
        public void Lost() { m_Have = false; m_RelVelocity = Vector3.zero; Near = false; }

        /// <summary>One frame. True with the flick's direction when a flick fires.</summary>
        public bool Step(FlickHand h, float now, float dt, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (dt <= 0f) return false;
            var rel = h.Tip - h.Palm;
            if (m_Have)
            {
                var raw = (rel - m_LastRel) / dt;
                if (raw.magnitude > MaxSnap) { m_LastRel = rel; m_RelVelocity = Vector3.zero; return false; } // a tracking jump
                m_RelVelocity = Vector3.Lerp(m_RelVelocity, raw, 0.7f);
            }
            m_LastRel = rel; m_Have = true;
            Gap = Vector3.Distance(h.Tip, h.Thumb);
            Curl = rel.magnitude;
            Speed = rel.sqrMagnitude > 1e-8f ? Vector3.Dot(m_RelVelocity, rel.normalized) : 0f;
            bool loaded = Gap < LoadGap || Curl < LoadCurl;
            bool near = Mathf.Min(Vector3.Distance(h.Tip, h.Friend), Vector3.Distance(h.Thumb, h.Friend)) <= Reach;
            if (near) m_NearAt = now;
            if (loaded) { m_LoadedAt = now; if (near) m_Armed = true; } // loading beside the friend readies the next flick
            bool ready = m_Armed && now - m_LoadedAt <= LoadMemory && now - m_NearAt <= NearMemory;
            Near = ready;
            if (ready) m_Peak = Math.Max(m_Peak, Speed);
            else if (m_WasNear) { MissedPeak = m_Peak; m_Peak = 0f; }
            m_WasNear = ready;
            if (!ready || loaded || now < m_ReadyAt || Speed < MinSnap) return false;
            // the snap: the way the finger shot out, leaning toward where it points
            direction = (m_RelVelocity.normalized * 0.7f + rel.normalized * 0.3f).normalized;
            m_ReadyAt = now + Cooldown;
            m_Armed = false;
            m_WasNear = false;
            Near = false;
            m_Peak = 0f;
            return true;
        }

        /// <summary>Reads and clears <see cref="MissedPeak"/>.</summary>
        public bool TakeMiss(out float peak)
        {
            peak = MissedPeak;
            MissedPeak = -1f;
            return peak >= 0f;
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
