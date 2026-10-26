using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Ball dispenser with an endless supply from a fixed pool (oldest thrown ball is recycled).
    /// Grab the ready ball and release to throw; a quick tap tosses it toward where you're facing.
    /// Aim assist: the throw snaps to the instrument nearest your throw direction, and an
    /// <see cref="AimArc"/> shows the path while aiming and stays on the last throw until you aim again.
    /// Pool balls live in the scene so the Spatial renderer knows them.
    /// </summary>
    public class BallLauncher : MonoBehaviour
    {
        [SerializeField] Transform spawnPoint;
        [Tooltip("Where spare balls wait out of sight (inside the dispenser post).")]
        [SerializeField] Transform parkPoint;
        [SerializeField] Ball[] pool;
        [SerializeField] AimArc arc;

        [Header("Aim assist")]
        [Tooltip("Instruments within this angle (degrees) of the throw direction get snapped to.")]
        [SerializeField, Range(0f, 90f)] float assistAngle = 35f;
        [Tooltip("Horizontal speed (m/s) used to pick the flight time of an assisted throw.")]
        [SerializeField, Min(0.5f)] float assistHorizontalSpeed = 5f;
        [SerializeField, Min(0.1f)] float minFlightTime = 0.35f;
        [SerializeField, Min(0.1f)] float maxFlightTime = 1.0f;

        [Header("Unassisted throws")]
        [Tooltip("Releases slower than this (m/s) aim along the gaze/pointer direction instead.")]
        [SerializeField, Min(0f)] float minThrowSpeed = 1.2f;
        [SerializeField, Min(0.1f)] float tossSpeed = 5f;
        [SerializeField] float tossLift = 1.5f;
        [SerializeField, Min(0.1f)] float maxThrowSpeed = 15f;

        struct Sample
        {
            public float time;
            public Vector3 position;
        }

        readonly Queue<Ball> m_Free = new();
        readonly LinkedList<Ball> m_Thrown = new();
        readonly List<Sample> m_History = new();
        Ball m_Ready;
        int m_GrabId = -1;
        Vector3 m_GrabOffset;
        Vector3 m_Facing = Vector3.forward;
        Vector3 m_AimDirection = Vector3.forward;
        bool m_ArcHeld; // arc is showing the last throw and should stay put
        OrbType m_Orb = OrbType.Flutter;

        public Ball ReadyBall => m_Ready;
        public OrbType CurrentOrb => m_Orb;

        /// <summary>Chooses which orb the ready ball (and every later ball) is.</summary>
        public void SetOrb(OrbType orb)
        {
            m_Orb = orb;
            if (m_Ready != null) m_Ready.ApplyOrb(orb);
        }
        public int ThrowCount { get; private set; }
        public InstrumentTarget AimedTarget { get; private set; }

        /// <summary>Wires references when built from code (tests); scenes set them in the inspector.</summary>
        public void Configure(Transform spawn, Ball[] balls)
        {
            spawnPoint = spawn;
            pool = balls;
        }

        void Start()
        {
            // Balls pass through each other so ones resting on furniture never block new throws.
            for (int i = 0; i < pool.Length; i++)
                for (int j = i + 1; j < pool.Length; j++)
                    if (pool[i] != null && pool[j] != null)
                        Physics.IgnoreCollision(pool[i].GetComponent<Collider>(), pool[j].GetComponent<Collider>());

            // PICO's Spatial renderer does not reliably follow SetActive, so spare balls are parked
            // out of sight (inside the dispenser post) instead of being deactivated.
            foreach (var ball in pool)
            {
                if (ball == null) continue;
                ball.Expired += OnExpired;
                Park(ball);
                m_Free.Enqueue(ball);
            }
            PrepareNext();
        }

        Vector3 ParkPosition => parkPoint != null ? parkPoint.position : spawnPoint.position + Vector3.down * 0.5f;

        void Park(Ball ball)
        {
            ball.Hold();
            ball.SetAlive(false);
            ball.FitForParking();
            Teleport(ball, ParkPosition, Quaternion.identity);
        }

        // Interpolated rigidbodies write their own pose back to the transform, so move both.
        static void Teleport(Ball ball, Vector3 position, Quaternion rotation)
        {
            var body = ball.GetComponent<Rigidbody>();
            body.position = position;
            body.rotation = rotation;
            ball.transform.SetPositionAndRotation(position, rotation);
        }

        void Update()
        {
            if (arc == null || m_Ready == null || m_ArcHeld) return;

            if (m_GrabId >= 0)
            {
                Vector3 drag = DragVelocity();
                Vector3 dir = Flat(drag).sqrMagnitude > 0.64f ? drag : m_Facing; // > 0.8 m/s sideways = intentional swing
                if (Flat(dir).sqrMagnitude > 1e-4f) m_AimDirection = Flat(dir).normalized;
            }
            Preview(m_Ready.transform.position, m_AimDirection);
        }

        public bool IsReadyBall(GameObject go) => m_Ready != null && go != null && go.GetComponentInParent<Ball>() == m_Ready;

        public void BeginGrab(int interactionId, Vector3 pointerPosition)
        {
            if (m_Ready == null) return;
            m_GrabId = interactionId;
            m_ArcHeld = false;
            m_GrabOffset = m_Ready.transform.position - pointerPosition;
            m_History.Clear();
            Record(m_Ready.transform.position);
        }

        public void UpdateGrab(int interactionId, Vector3 pointerPosition, Vector3 facing)
        {
            if (interactionId != m_GrabId || m_Ready == null) return;
            Teleport(m_Ready, pointerPosition + m_GrabOffset, m_Ready.transform.rotation);
            if (facing.sqrMagnitude > 1e-4f) m_Facing = facing;
            Record(m_Ready.transform.position);
        }

        /// <summary>Releases a grabbed ball; <paramref name="facing"/> aims slow releases (taps).</summary>
        public void EndGrab(int interactionId, Vector3 facing)
        {
            if (interactionId != m_GrabId || m_Ready == null) return;
            m_GrabId = -1;
            if (facing.sqrMagnitude > 1e-4f) m_Facing = facing;

            Vector3 velocity = DragVelocity();
            Vector3 dir = Flat(velocity).sqrMagnitude > 0.64f ? velocity : m_Facing;
            if (velocity.magnitude < minThrowSpeed)
                velocity = m_Facing.normalized * tossSpeed + Vector3.up * tossLift;
            ThrowAssisted(m_Ready.transform.position, dir, Vector3.ClampMagnitude(velocity, maxThrowSpeed));
        }

        /// <summary>
        /// Throws the ready ball from <paramref name="from"/>. If an instrument lies near
        /// <paramref name="direction"/> the ball is sent on an arc that lands on it; otherwise
        /// <paramref name="fallbackVelocity"/> is used unchanged.
        /// </summary>
        public Ball ThrowAssisted(Vector3 from, Vector3 direction, Vector3 fallbackVelocity)
        {
            if (m_Ready == null) return null;
            Teleport(m_Ready, from, m_Ready.transform.rotation);

            float duration;
            Vector3 velocity;
            bool assisted = TrySolve(from, direction, out velocity, out duration, out var target);
            if (!assisted)
            {
                velocity = fallbackVelocity;
                duration = maxFlightTime;
            }
            AimedTarget = target;
            if (Flat(direction).sqrMagnitude > 1e-4f) m_AimDirection = Flat(direction).normalized;

            if (arc != null)
            {
                arc.Show(from, velocity, duration, assisted);
                m_ArcHeld = true; // keep the last throw's arc visible until the next aim
            }
            Debug.Log($"[RoomOrchestra] Aim {(assisted ? "assisted -> " + target.Instrument : "free")}");
            return Throw(velocity);
        }

        /// <summary>Throws the ready ball from an arbitrary point without aim assist (tests).</summary>
        public Ball ThrowFrom(Vector3 position, Vector3 velocity)
        {
            if (m_Ready == null) return null;
            Teleport(m_Ready, position, m_Ready.transform.rotation);
            return Throw(velocity);
        }

        /// <summary>
        /// Finds the instrument closest to <paramref name="direction"/> (within <see cref="assistAngle"/>)
        /// and the launch velocity that lands on its aim point.
        /// </summary>
        public bool TrySolve(Vector3 from, Vector3 direction, out Vector3 velocity, out float duration, out InstrumentTarget target)
        {
            velocity = Vector3.zero;
            duration = 0f;
            target = null;
            Vector3 dir = Flat(direction);
            if (dir.sqrMagnitude < 1e-4f) return false;

            float best = assistAngle;
            foreach (var t in InstrumentTarget.Active)
            {
                Vector3 to = Flat(t.AimPosition - from);
                if (to.sqrMagnitude < 0.04f) continue;
                float angle = Vector3.Angle(dir, to);
                if (angle <= best)
                {
                    best = angle;
                    target = t;
                }
            }
            if (target == null) return false;

            Vector3 delta = target.AimPosition - from;
            duration = Mathf.Clamp(Flat(delta).magnitude / assistHorizontalSpeed, minFlightTime, maxFlightTime);
            velocity = delta / duration - 0.5f * duration * Physics.gravity;
            return true;
        }

        void Preview(Vector3 from, Vector3 direction)
        {
            if (TrySolve(from, direction, out var v, out var duration, out var target))
            {
                AimedTarget = target;
                arc.Show(from, v, duration, true);
            }
            else
            {
                AimedTarget = null;
                arc.Show(from, direction.normalized * tossSpeed + Vector3.up * tossLift, maxFlightTime, false);
            }
        }

        Ball Throw(Vector3 velocity)
        {
            var ball = m_Ready;
            if (ball == null) return null;
            m_Ready = null;
            ball.Launch(velocity);
            m_Thrown.AddLast(ball);
            ThrowCount++;
            Debug.Log($"[RoomOrchestra] Ball thrown #{ThrowCount} speed={velocity.magnitude:F1} m/s");
            PrepareNext();
            return ball;
        }

        void PrepareNext()
        {
            Ball next;
            if (m_Free.Count > 0)
            {
                next = m_Free.Dequeue();
            }
            else if (m_Thrown.Count > 0)
            {
                next = m_Thrown.First.Value; // recycle the oldest ball in flight
                m_Thrown.RemoveFirst();
            }
            else
            {
                return;
            }

            next.Hold();
            next.SetAlive(true);
            next.ApplyOrb(m_Orb);
            Teleport(next, spawnPoint.position, spawnPoint.rotation);
            m_Ready = next;
        }

        void OnExpired(Ball ball)
        {
            if (!m_Thrown.Remove(ball)) return;
            Park(ball);
            m_Free.Enqueue(ball);
        }

        Vector3 DragVelocity()
        {
            if (m_History.Count < 2) return Vector3.zero;
            var first = m_History[0];
            var last = m_History[m_History.Count - 1];
            float dt = last.time - first.time;
            return dt > 0.01f ? (last.position - first.position) / dt : Vector3.zero;
        }

        void Record(Vector3 position)
        {
            float now = Time.time;
            m_History.Add(new Sample { time = now, position = position });
            while (m_History.Count > 2 && now - m_History[0].time > 0.12f) // velocity over the last ~0.1 s
                m_History.RemoveAt(0);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
