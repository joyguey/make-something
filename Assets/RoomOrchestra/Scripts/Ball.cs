using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// A throwable orb (Flutter, Bop or Boom). Each throw is one intended action: its first
    /// contact with an <see cref="InstrumentTarget"/> becomes one <see cref="MusicalHitEvent"/>;
    /// later bounces and ricochets in the same throw are ignored.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class Ball : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Min(1f)] float lifetime = 15f;
        [Tooltip("Impacts slower than this (m/s) are ignored, so rolling balls don't spam notes.")]
        [SerializeField, Min(0f)] float minHitSpeed = 0.5f;
        [SerializeField] OrbType orb = OrbType.Flutter;
        [Tooltip("Size of a round (Bop-proportioned) orb; other orbs stretch from this.")]
        [SerializeField, Min(0.01f)] float diameter = 0.1f;
        [Tooltip("Character rim and face parts, recoloured for whichever orb this ball is.")]
        [SerializeField] Renderer rim;
        [SerializeField] Renderer[] faceParts;
        Material m_RimMaterial, m_FaceMaterial;
        Rigidbody m_Body;
        Renderer m_Renderer;
        Material m_Material; // instanced only when the orb's color first has to change
        float m_LaunchTime;
        long m_ActionId;

        public bool InFlight { get; private set; }
        public float LaunchTime => m_LaunchTime;
        public int Hits { get; private set; }
        public OrbType Orb => orb;
        public long ActionId => m_ActionId;

        /// <summary>Raised when a thrown ball times out or falls away, so its pool can reclaim it.</summary>
        public event Action<Ball> Expired;

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
            m_Renderer = GetComponent<Renderer>();
        }

        /// <summary>
        /// Turns this ball into <paramref name="type"/>: proportions, mass, bounce and color.
        /// Writes only what differs, so pre-skinned balls cause no changes at startup.
        /// </summary>
        /// <summary>
        /// Upright, narrow waiting shape (Flutter's, the authored one) so a parked ball fits wholly inside
        /// the 12 cm dispenser post; a wide Boom or a tilted ball poked out of it. ApplyOrb restores the
        /// orb's own shape when the ball is handed out.
        /// </summary>
        public void FitForParking()
        {
            var narrow = OrbCatalog.Get(OrbType.Flutter);
            var scale = new Vector3(narrow.ScaleX, narrow.ScaleY, narrow.ScaleZ) * diameter;
            if (transform.localScale != scale) transform.localScale = scale;
        }

        public void ApplyOrb(OrbType type)
        {
            if (m_Body == null) Awake();
            orb = type;
            var profile = OrbCatalog.Get(type);

            var scale = new Vector3(profile.ScaleX, profile.ScaleY, profile.ScaleZ) * diameter;
            if (transform.localScale != scale) transform.localScale = scale;
            m_Body.mass = profile.Mass;

            var physics = GetComponent<Collider>().sharedMaterial;
            if (physics != null && !Mathf.Approximately(physics.bounciness, profile.Bounciness))
                GetComponent<Collider>().material.bounciness = profile.Bounciness; // per-ball instance

            var color = new Color(profile.R, profile.G, profile.B);
            var current = m_Material != null ? m_Material : m_Renderer != null ? m_Renderer.sharedMaterial : null;
            if (current != null && current.HasProperty(BaseColorId) && current.GetColor(BaseColorId) != color)
            {
                if (m_Material == null) m_Material = m_Renderer.material;
                m_Material.SetColor(BaseColorId, color);
            }
            if (rim != null) Recolor(new[] { rim }, ref m_RimMaterial, profile.Rim);
            if (faceParts != null && faceParts.Length > 0) Recolor(faceParts, ref m_FaceMaterial, profile.Face);
        }

        /// <summary>Wires the character parts (scene setup tool and tests).</summary>
        public void ConfigureCharacter(Renderer rimRenderer, Renderer[] face)
        {
            rim = rimRenderer;
            faceParts = face;
        }

        public Renderer Rim => rim;
        public IReadOnlyList<Renderer> FaceParts => faceParts;

        // One material instance per ball and role, made only when the colour first has to change.
        static void Recolor(Renderer[] parts, ref Material instance, Rgb rgb)
        {
            var color = new Color(rgb.R, rgb.G, rgb.B);
            var current = instance != null ? instance : parts[0].sharedMaterial;
            if (current == null || !current.HasProperty(BaseColorId) || current.GetColor(BaseColorId) == color) return;
            if (instance == null)
            {
                instance = new Material(parts[0].sharedMaterial);
                foreach (var part in parts) part.sharedMaterial = instance;
            }
            instance.SetColor(BaseColorId, color);
        }

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
            m_Body.linearVelocity = Vector3.ClampMagnitude(velocity, OrbCatalog.Get(orb).MaxLaunchSpeed);
            m_Body.angularVelocity = Vector3.zero;
            InFlight = true;
            Hits = 0;
            m_ActionId = MusicalHitPipeline.BeginAction();
            m_LaunchTime = Time.time;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!InFlight || MusicalHitPipeline.HasFired(m_ActionId)) return;
            var target = collision.collider.GetComponentInParent<InstrumentTarget>();
            if (target == null) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < minHitSpeed) return;

            float contactY = collision.contactCount > 0 ? collision.GetContact(0).point.y : transform.position.y;
            var tier = MusicalHitPipeline.Intensity.FromImpactSpeed(speed);
            if (MusicalHitPipeline.Submit(m_ActionId, target, orb, tier, target.PitchAtHeight(contactY), InputPath.Toss) != null)
                Hits++;
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
