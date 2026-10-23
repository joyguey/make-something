using System;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>A throwable ball that plays any <see cref="InstrumentTarget"/> it strikes.</summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class Ball : MonoBehaviour
    {
        [SerializeField, Min(1f)] float lifetime = 15f;
        [Tooltip("Impacts slower than this (m/s) are ignored, so rolling balls don't spam notes.")]
        [SerializeField, Min(0f)] float minHitSpeed = 0.5f;
        [Tooltip("Impact speed (m/s) that plays at full volume.")]
        [SerializeField, Min(0.1f)] float fullVolumeSpeed = 6f;
        Rigidbody m_Body;
        float m_LaunchTime;
        // Each throw plays each instrument at most once, so bounces (e.g. shade then lamp base)
        // don't add an echo note; ricochets onto a different instrument still play it.
        readonly System.Collections.Generic.HashSet<InstrumentTarget> m_Played = new();

        public bool InFlight { get; private set; }
        public float LaunchTime => m_LaunchTime;
        public int Hits { get; private set; }

        /// <summary>Raised when a thrown ball times out or falls away, so its pool can reclaim it.</summary>
        public event Action<Ball> Expired;

        void Awake() => m_Body = GetComponent<Rigidbody>();

        /// <summary>Holds the ball still (in the dispenser or while being dragged).</summary>
        public void Hold()
        {
            if (m_Body == null) m_Body = GetComponent<Rigidbody>();
            InFlight = false;
            m_Body.isKinematic = true;
        }

        public void Launch(Vector3 velocity)
        {
            if (m_Body == null) m_Body = GetComponent<Rigidbody>();
            m_Body.isKinematic = false;
            m_Body.linearVelocity = velocity;
            m_Body.angularVelocity = Vector3.zero;
            InFlight = true;
            Hits = 0;
            m_Played.Clear();
            m_LaunchTime = Time.time;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!InFlight) return;
            var target = collision.collider.GetComponentInParent<InstrumentTarget>();
            if (target == null) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < minHitSpeed) return;
            if (!m_Played.Add(target)) return; // already played this instrument on this throw

            Hits++;
            target.Play(InstrumentTarget.HitSource.Ball, Mathf.Clamp(speed / fullVolumeSpeed, 0.3f, 1f));
        }

        void Update()
        {
            if (InFlight && (Time.time - m_LaunchTime > lifetime || transform.position.y < -5f))
            {
                InFlight = false;
                Expired?.Invoke(this);
            }
        }
    }
}
