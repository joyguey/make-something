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
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>Character bodies glow softly in their own colour (share of the base colour).</summary>
        public const float GlowStrength = 0.5f;

        [SerializeField, Min(1f)] float lifetime = 15f;
        [Tooltip("Impacts slower than this (m/s) are ignored, so rolling balls don't spam notes.")]
        [SerializeField, Min(0f)] float minHitSpeed = 0.5f;
        [SerializeField] OrbType orb = OrbType.Flutter;
        [Tooltip("Size of a round (Bop-proportioned) orb; other orbs stretch from this.")]
        [SerializeField, Min(0.01f)] float diameter = 0.1f;
        [Tooltip("Body renderer (on the wobbling visual child); defaults to this object's renderer.")]
        [SerializeField] Renderer body;
        [Tooltip("Character rim and face parts, recoloured for whichever orb this ball is.")]
        [SerializeField] Renderer rim;
        [SerializeField] Renderer[] faceParts;
        [Tooltip("Ready-made body, rim and face materials per character, in OrbType order (Flutter, Bop, Boom). " +
                 "PICO Spatial draws a material made at runtime white (headset 2026-11-08: Boom on the cloud), " +
                 "so the ball swaps to the project's own materials; recolouring is only the fallback.")]
        [SerializeField] Material[] bodySkins, rimSkins, faceSkins;
        [Tooltip("Plays this character's bounce when it bounces off a wall (BounceSurface).")]
        [SerializeField] AudioSource bounceVoice;
        [Tooltip("Each character's bounce, in OrbType order: Flutter, Bop, Boom.")]
        [SerializeField] AudioClip[] bounceClips;
        float m_LastBounce = -10f;

        public void ConfigureBounce(AudioSource voice, AudioClip[] clips)
        {
            bounceVoice = voice;
            bounceClips = clips;
        }

        /// <summary>The bounce sound last played, or null.</summary>
        public AudioClip LastBounce { get; private set; }
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
            m_Renderer = body != null ? body : GetComponent<Renderer>();
        }

        /// <summary>
        /// Upright, narrow waiting shape (Flutter's, the authored one) so a parked ball fits wholly inside
        /// the 12 cm core of the ball stand's cloud; a wide Boom or a tilted ball poked out of it. ApplyOrb
        /// restores the orb's own shape when the ball is handed out.
        /// </summary>
        public void FitForParking()
        {
            var narrow = OrbCatalog.Get(OrbType.Flutter);
            var scale = new Vector3(narrow.ScaleX, narrow.ScaleY, narrow.ScaleZ) * diameter;
            if (transform.localScale != scale) transform.localScale = scale;
        }

        /// <summary>
        /// Turns this ball into <paramref name="type"/>: proportions, mass, bounce and color.
        /// Writes only what differs, so pre-skinned balls cause no changes at startup.
        /// </summary>
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

            if (Skin(bodySkins, type, out var bodySkin))
            {
                if (m_Renderer != null && m_Renderer.sharedMaterial != bodySkin) m_Renderer.sharedMaterial = bodySkin;
                if (rim != null && Skin(rimSkins, type, out var rimSkin) && rim.sharedMaterial != rimSkin) rim.sharedMaterial = rimSkin;
                if (faceParts != null && Skin(faceSkins, type, out var faceSkin))
                    foreach (var part in faceParts)
                        if (part != null && part.sharedMaterial != faceSkin) part.sharedMaterial = faceSkin;
                return;
            }

            var color = new Color(profile.R, profile.G, profile.B);
            var current = m_Material != null ? m_Material : m_Renderer != null ? m_Renderer.sharedMaterial : null;
            if (current != null && current.HasProperty(BaseColorId) && current.GetColor(BaseColorId) != color)
            {
                if (m_Material == null) m_Material = m_Renderer.material;
                m_Material.SetColor(BaseColorId, color);
                if (m_Material.HasProperty(EmissionColorId)) m_Material.SetColor(EmissionColorId, color * GlowStrength);
            }
            if (rim != null) Recolor(new[] { rim }, ref m_RimMaterial, profile.Rim);
            if (faceParts != null && faceParts.Length > 0) Recolor(faceParts, ref m_FaceMaterial, profile.Face);
        }

        static bool Skin(Material[] skins, OrbType type, out Material skin)
        {
            skin = skins != null && (int)type < skins.Length ? skins[(int)type] : null;
            return skin != null;
        }

        /// <summary>The project materials each character wears (scene setup), in OrbType order.</summary>
        public void ConfigureSkins(Material[] bodies, Material[] rims, Material[] faces)
        {
            bodySkins = bodies;
            rimSkins = rims;
            faceSkins = faces;
        }

        public Material BodyMaterial => m_Renderer != null ? m_Renderer.sharedMaterial : null;

        /// <summary>Wires the character parts (scene setup tool and tests).</summary>
        public void ConfigureCharacter(Renderer rimRenderer, Renderer[] face, Renderer bodyRenderer = null)
        {
            if (bodyRenderer != null) body = bodyRenderer;
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
        // A throw made by tapping a note (owner, 2026-11-07: tapping a note shouldn't skip the throw): the ball
        // carries that note to its instrument and plays it there; if anything stops it on the way, the note still
        // plays when it should have landed (one action, one musical event).
        InstrumentTarget m_TapTarget;
        PitchInput m_TapPitch;
        IntensityTier m_TapTier;
        double m_TapDsp;
        float m_TapDeadline = -1f;

        /// <summary>A tapped note this ball is carrying, if any.</summary>
        public InstrumentTarget CarryingTo => m_TapTarget;

        public void CarryTapNote(InstrumentTarget target, PitchInput pitch, IntensityTier tier, double tapDsp, float flightSeconds)
        {
            m_TapTarget = target;
            m_TapPitch = pitch;
            m_TapTier = tier;
            m_TapDsp = tapDsp;
            m_TapDeadline = Time.time + flightSeconds + TapGraceSeconds;
        }

        /// <summary>A carried note plays this long after the planned landing at the latest.</summary>
        public const float TapGraceSeconds = 0.25f;

        bool DeliverTapNote(InstrumentTarget target)
        {
            if (m_TapTarget == null) return false;
            var to = target != null ? target : m_TapTarget;
            m_TapTarget = null;
            m_TapDeadline = -1f;
            if (MusicalHitPipeline.HasFired(m_ActionId)) return true;
            if (MusicalHitPipeline.Submit(m_ActionId, to, orb, m_TapTier, m_TapPitch, InputPath.Place, m_TapDsp) != null) Hits++;
            return true;
        }

        public void Hold()
        {
            if (m_Body == null) m_Body = GetComponent<Rigidbody>();
            if (m_TapTarget != null) DeliverTapNote(null); // recycled or parked before it landed: its note still plays
            InFlight = false;
            m_PoofAt = -1f;
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

        /// <summary>Alive (wobbling, blinking) on the stand, in the hand and in flight; at rest when parked.</summary>
        bool IsFallingToTheFloor()
        {
            if (m_Body == null || m_Body.isKinematic) return false;
            var velocity = m_Body.linearVelocity;
            if (velocity.y <= -ScaredFallSpeed)
                return Physics.Raycast(transform.position, velocity.normalized, out var hit, ScaredLookAhead, ~0, QueryTriggerInteraction.Ignore)
                    && IsGround(hit.collider);
            return IsRollingTowardAnEdge(velocity);
        }

        // Rolling on furniture: look ahead along the roll; if a spot it will reach soon has only the floor
        // beneath it, the edge is coming. Only while touching a surface, so a ball flying toward a target
        // never panics mid-throw.
        bool IsRollingTowardAnEdge(Vector3 velocity)
        {
            if (Time.time - m_LastSurfaceContact > 0.1f) return false;
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            if (flat.magnitude < EdgeMinSpeed) return false;
            float radius = transform.lossyScale.y * 0.5f;
            foreach (float seconds in EdgeLookAheadSeconds)
            {
                var ahead = transform.position + flat * seconds;
                if (!Physics.Raycast(ahead + Vector3.up * radius, Vector3.down, out var hit, ScaredLookAhead, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.attachedRigidbody == m_Body) continue;
                if (IsGround(hit.collider) && transform.position.y - hit.point.y > radius + EdgeMinDrop) return true;
            }
            return false;
        }

        static bool IsGround(Collider c) => c.GetComponentInParent<GroundSurface>() != null;

        /// <summary>Rolling slower than this (m/s) never worries; drops lower than this (m) are not scary.</summary>
        public const float EdgeMinSpeed = 0.08f, EdgeMinDrop = 0.15f;
        static readonly float[] EdgeLookAheadSeconds = { 0.15f, 0.3f, 0.5f };
        float m_LastSurfaceContact = -10f;

        void OnCollisionStay(Collision collision)
        {
            if (!IsGround(collision.collider)) m_LastSurfaceContact = Time.time;
        }

        /// <summary>Falling faster than this (m/s) toward the floor looks scared; it looks this far ahead (m).</summary>
        public const float ScaredFallSpeed = 0.3f, ScaredLookAhead = 3f;

        public void SetAlive(bool alive)
        {
            if (TryGetComponent<JellyBody>(out var jelly)) jelly.SetAwake(alive);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (TryGetComponent<JellyBody>(out var jelly)) jelly.Kick(collision.relativeVelocity.magnitude / 5f); // splat
            if (!IsGround(collision.collider)) m_LastSurfaceContact = Time.time;
            if (m_TapTarget != null)
            {
                var hitTarget = collision.collider.GetComponentInParent<InstrumentTarget>();
                if (hitTarget == m_TapTarget) DeliverTapNote(hitTarget);
            }
            if (InFlight && IsGround(collision.collider))
            {
                // Down on the floor: a squashed, startled moment where it landed, then a poof and it is
                // gone (back to the stand).
                m_PoofPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
                m_PoofAt = Time.time + FloorPauseSeconds;
                InFlight = false;
                m_Body.isKinematic = true;
                if (TryGetComponent<CharacterFace>(out var face)) face.Show(Expression.Scared, hold: FloorPauseSeconds + 0.1f);
                if (jelly != null) jelly.Kick(1f);
                return;
            }
            if (InFlight && collision.collider.GetComponentInParent<BounceSurface>() != null)
            {
                Bounce(collision.relativeVelocity.magnitude);
                return;
            }
            if (m_TapTarget != null) return; // carrying a tapped note for its own instrument
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

        // Its own sound off a wall: this character's voice, louder and a touch brighter the harder it hits.
        void Bounce(float speed)
        {
            float volume = BounceSound.Volume(speed);
            if (volume <= 0f || Time.time - m_LastBounce < BounceSound.GapSeconds) return;
            if (bounceVoice == null || bounceClips == null || (int)orb >= bounceClips.Length || bounceClips[(int)orb] == null) return;
            m_LastBounce = Time.time;
            LastBounce = bounceClips[(int)orb];
            bounceVoice.pitch = BounceSound.Pitch(speed);
            bounceVoice.PlayOneShot(LastBounce, volume);
            Debug.Log($"[RoomOrchestra] {orb} bounces off a wall ({speed:0.0} m/s, volume {volume:0.00})");
        }

        /// <summary>
        /// True while falling and the first thing in its path is the floor (rolled off a table, or a throw
        /// that missed): the face looks down at the drop, scared.
        /// </summary>
        public bool FallingToTheFloor { get; private set; }

        /// <summary>How long a character sits startled on the floor before it poofs (s).</summary>
        public const float FloorPauseSeconds = 0.35f;

        float m_PoofAt = -1f;
        Vector3 m_PoofPoint;

        public bool OnTheFloor => m_PoofAt > 0f;

        void Update()
        {
            if (m_TapTarget != null && Time.time >= m_TapDeadline) DeliverTapNote(null);
            if (m_PoofAt > 0f && Time.time >= m_PoofAt)
            {
                m_PoofAt = -1f;
                PoofCloud.PoofAt(m_PoofPoint);
                Debug.Log($"[RoomOrchestra] {orb} landed on the floor: poof");
                Expired?.Invoke(this);
                return;
            }
            bool wasScared = FallingToTheFloor;
            FallingToTheFloor = InFlight && IsFallingToTheFloor();
            if (FallingToTheFloor && !wasScared) Debug.Log($"[RoomOrchestra] {orb}: eek! the floor is coming");
            if (FallingToTheFloor)
            {
                if (TryGetComponent<CharacterFace>(out var face)) face.Show(Expression.Scared, hold: 0.3f); // relaxes soon if it lands safely
                if (TryGetComponent<JellyBody>(out var jelly)) jelly.Tremble();
            }
            if (InFlight && (Time.time - m_LaunchTime > lifetime || transform.position.y < -5f))
            {
                InFlight = false;
                Expired?.Invoke(this);
            }
        }
    }
}
