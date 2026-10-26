using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Makes a character feel alive: it breathes and sways when idle, leans and jiggles like jelly when
    /// it is dragged, thrown or carried round its orbit, splats on impact and hops on cue. Only the
    /// <c>visual</c> child moves (the root belongs to physics, the orbit or the tray). Motion starts a few
    /// seconds after load, never at startup: PICO Spatial keeps the exported pose of objects changed
    /// before its renderer connects. Asleep (parked inside a post, hub or lamp) it rests in its authored
    /// pose and writes nothing.
    /// </summary>
    public class JellyBody : MonoBehaviour
    {
        public const float StartDelaySeconds = 3f;

        public static bool ReducedMotion { get; set; }

        [SerializeField] Transform visual;
        [SerializeField] OrbType character;
        [SerializeField] bool awake = true;

        CharacterMotion m_Motion;
        Vector3 m_HomePosition, m_HomeScale;
        Quaternion m_HomeRotation;
        bool m_Captured;
        Vector3 m_LastPosition, m_Velocity;
        Vector2 m_Lean, m_LeanVelocity;     // degrees about the root's x (forward/back) and z (sideways)
        float m_Squash, m_SquashVelocity;   // + squashed, - stretched
        float m_HopStart = -10f, m_Phase;
        bool m_ResetMotion = true;

        public Transform Visual => visual;
        public bool IsAwake => awake;
        public bool IsHopping => Time.time - m_HopStart < m_Motion.HopSeconds;
        public Vector2 Lean => m_Lean;
        public float Squash => m_Squash;

        public void Configure(Transform look, OrbType type, bool startAwake = true)
        {
            visual = look;
            character = type;
            awake = startAwake;
        }

        void Capture()
        {
            if (m_Captured || visual == null) return;
            m_Captured = true;
            m_Motion = CharacterMotion.For(character);
            m_HomePosition = visual.localPosition;
            m_HomeRotation = visual.localRotation;
            m_HomeScale = visual.localScale;
            m_LastPosition = transform.position;
            m_Phase = Random.value * 10f; // characters do not breathe in lockstep
        }

        /// <summary>Wakes the character (on the ring, in the hand) or rests it in its authored pose (parked).</summary>
        public void SetAwake(bool on)
        {
            if (awake == on) return;
            awake = on;
            m_ResetMotion = true; // it was carried or teleported here: not a fling
            if (on || !m_Captured) return;
            m_Lean = m_LeanVelocity = Vector2.zero;
            m_Squash = m_SquashVelocity = 0f;
            m_HopStart = -10f;
            Write(m_HomePosition, m_HomeRotation, m_HomeScale);
        }

        /// <summary>A happy hop (squash, jump, stretch, land).</summary>
        public void Hop()
        {
            Capture();
            if (ReducedMotion || !awake) return;
            m_HopStart = Time.time;
            m_SquashVelocity += 3f;
        }

        /// <summary>A splat or poke: <paramref name="strength"/> 0..1 squashes and sets the jelly wobbling.</summary>
        public void Kick(float strength)
        {
            Capture();
            if (ReducedMotion || !awake) return;
            m_SquashVelocity += Mathf.Clamp01(strength) * 9f;
        }

        void LateUpdate()
        {
            if (visual == null || !awake || Time.timeSinceLevelLoad < StartDelaySeconds) return;
            Capture();
            float dt = Mathf.Clamp(Time.deltaTime, 1e-4f, 0.05f);
            float now = Time.time;

            // How the root moved this frame (dragging, flight, orbit), in the root's own axes.
            var position = transform.position;
            if (m_ResetMotion)
            {
                m_ResetMotion = false;
                m_LastPosition = position;
                m_Velocity = Vector3.zero;
            }
            var velocity = (position - m_LastPosition) / dt;
            if ((position - m_LastPosition).sqrMagnitude > 1f) velocity = Vector3.zero; // teleport, not motion
            var accel = (velocity - m_Velocity) / dt;
            m_LastPosition = position;
            m_Velocity = velocity;
            var localVel = transform.InverseTransformDirection(velocity);
            var localAccel = transform.InverseTransformDirection(accel);

            if (ReducedMotion)
            {
                Write(m_HomePosition, m_HomeRotation, m_HomeScale);
                return;
            }

            // Jelly lags behind acceleration and leans into its travel; a damped spring makes it jiggle.
            var target = new Vector2(
                -localAccel.z * m_Motion.LeanPerAccel + localVel.z * m_Motion.LeanPerSpeed,
                localAccel.x * m_Motion.LeanPerAccel - localVel.x * m_Motion.LeanPerSpeed);
            target = Vector2.ClampMagnitude(target, CharacterMotion.MaxLeanDegrees);
            float k = m_Motion.Stiffness, c = 2f * m_Motion.Damping * Mathf.Sqrt(k);
            m_LeanVelocity += ((target - m_Lean) * k - m_LeanVelocity * c) * dt;
            m_Lean += m_LeanVelocity * dt;
            m_Lean = Vector2.ClampMagnitude(m_Lean, CharacterMotion.MaxLeanDegrees);

            // Vertical jolts (landing, stopping a drag) squash it; it springs back with a wobble.
            m_SquashVelocity += (-m_Squash * k - m_SquashVelocity * c - Mathf.Clamp(localAccel.y, -40f, 40f) * 0.004f) * dt;
            m_Squash = Mathf.Clamp(m_Squash + m_SquashVelocity * dt, -CharacterMotion.MaxSquash, CharacterMotion.MaxSquash);

            float t = now + m_Phase;
            float breathe = Mathf.Sin(t * 2f * Mathf.PI * m_Motion.BreatheRate) * m_Motion.BreatheAmount;
            float sway = Mathf.Sin(t * 2f * Mathf.PI * m_Motion.BreatheRate * 0.5f) * m_Motion.SwayDegrees;

            float hopT = (now - m_HopStart) / m_Motion.HopSeconds;
            float lift = CharacterMotion.HopArc(hopT) * m_Motion.HopHeight;
            float hopStretch = hopT > 0f && hopT < 1f ? Mathf.Sin(hopT * Mathf.PI) * 0.15f : 0f;

            float squash = m_Squash + breathe - hopStretch;
            var scale = Vector3.Scale(m_HomeScale, new Vector3(1f + squash * 0.5f, 1f - squash, 1f + squash * 0.5f));
            var rotation = m_HomeRotation * Quaternion.Euler(m_Lean.x, 0f, m_Lean.y + sway);
            // Grow from the base: shift down by the height lost, then lift for the hop (body units).
            var offset = Vector3.up * (-squash * 0.5f * m_HomeScale.y + lift * m_HomeScale.y);
            Write(m_HomePosition + offset, rotation, scale);
        }

        void Write(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (visual.localPosition != position) visual.localPosition = position;
            if (visual.localRotation != rotation) visual.localRotation = rotation;
            if (visual.localScale != scale) visual.localScale = scale;
        }
    }
}
