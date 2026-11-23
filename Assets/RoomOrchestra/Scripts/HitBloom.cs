using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// What a live hit looks like on the furniture (owner, 2026-11-19, the Astro Bot direction): the furniture squashes
    /// and springs back, a puff of petals (blossom pink, cream and the playing character's colour) rises and drifts down,
    /// and a soft ring ripples out from where the note landed. Nothing flashes or blinks (no strobing); echo repeats don't
    /// bloom (their orbiting mini is their cue, which keeps a full loop calm); with <see cref="ReducedMotion"/> nothing
    /// moves and the furniture's own gentle flash is the only cue, as before. A fixed pool of petals and one ripple per
    /// burst: nothing is created while playing.
    /// </summary>
    public sealed class HitBloom : MonoBehaviour
    {
        public static bool ReducedMotion { get; set; }

        public const float SquashSeconds = 0.4f, PetalSeconds = 1.0f, RippleSeconds = 0.6f;
        public const float Squash = 0.06f;
        const int PetalsPerHit = 10;

        [SerializeField] Transform[] petals;
        [SerializeField] Renderer[] petalLooks;
        [SerializeField] Material[] petalColours; // pink, cream, then one per character (OrbType order)
        [SerializeField] Transform[] ripples;
        [SerializeField] MeshFilter[] rippleShapes;
        [SerializeField] Renderer[] rippleLooks;
        [SerializeField] Transform park;

        struct Petal { public float start; public Vector3 from, velocity, spin; public float size; }
        Petal[] m_Petals;
        float[] m_RippleStart;
        Vector3[] m_RippleAt;
        Mesh[] m_RippleMeshes;
        int m_NextPetal, m_NextRipple;
        InstrumentTarget m_Squashing;
        Vector3 m_SquashBase, m_LastSet;
        float m_SquashStart = -1f;
        System.Random m_Random = new(7);
        bool m_Busy;

        public int ActivePetals { get; private set; }
        public Renderer[] PetalLooks => petalLooks;
        public bool Squashing => m_SquashStart >= 0f;
        public Transform[] Petals => petals;

        public void Configure(Transform[] petalParts, Material[] colours, Transform[] rippleParts, Transform parkPoint)
        {
            petals = petalParts;
            petalLooks = System.Array.ConvertAll(petalParts, p => p.GetComponent<Renderer>());
            petalColours = colours;
            ripples = rippleParts;
            rippleShapes = System.Array.ConvertAll(rippleParts, p => p.GetComponent<MeshFilter>());
            rippleLooks = System.Array.ConvertAll(rippleParts, p => p.GetComponent<Renderer>());
            park = parkPoint;
        }

        void OnEnable() => InstrumentTarget.AnyPlayed += OnPlayed;

        void OnDisable()
        {
            InstrumentTarget.AnyPlayed -= OnPlayed;
            EndSquash();
        }

        void OnPlayed(InstrumentTarget target, int note, float strength, InstrumentTarget.HitSource source)
        {
            if (source == InstrumentTarget.HitSource.Loop || target == null) return;
            Bloom(target, Mathf.Clamp01(strength));
        }

        /// <summary>One bloom on <paramref name="target"/> (false with reduced motion: nothing moves).</summary>
        public bool Bloom(InstrumentTarget target, float strength = 1f)
        {
            if (ReducedMotion || petals == null || petals.Length == 0) return false;
            Ensure();
            // the squash: about the furniture's own pivot (its floor point), restarting cleanly on a quick second hit
            EndSquash();
            m_Squashing = target;
            m_SquashBase = target.transform.localScale;
            m_LastSet = m_SquashBase;
            m_SquashStart = Time.time;

            var at = target.AimPosition;
            int character = OrbSelector.Instance != null ? (int)OrbSelector.Instance.SelectedOrb : 0;
            int count = Mathf.RoundToInt(Mathf.Lerp(6, PetalsPerHit, strength));
            for (int k = 0; k < count; k++)
            {
                int i = m_NextPetal;
                m_NextPetal = (m_NextPetal + 1) % petals.Length;
                float a = (float)m_Random.NextDouble() * Mathf.PI * 2f;
                float out_ = 0.35f + 0.3f * (float)m_Random.NextDouble();
                float up = 0.75f + 0.5f * (float)m_Random.NextDouble();
                m_Petals[i] = new Petal
                {
                    start = Time.time,
                    from = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.03f,
                    velocity = new Vector3(Mathf.Cos(a) * out_, up, Mathf.Sin(a) * out_),
                    spin = new Vector3((float)m_Random.NextDouble(), (float)m_Random.NextDouble(), (float)m_Random.NextDouble()) * 720f,
                    size = 0.8f + 0.4f * (float)m_Random.NextDouble(),
                };
                int colour = k % 3 == 2 ? 2 + character : k % 3;
                if (petalLooks[i] != null && petalColours != null && colour < petalColours.Length) petalLooks[i].sharedMaterial = petalColours[colour];
            }
            int r = m_NextRipple;
            m_NextRipple = (m_NextRipple + 1) % ripples.Length;
            m_RippleStart[r] = Time.time;
            m_RippleAt[r] = at + Vector3.up * 0.004f;
            if (rippleLooks[r] != null && petalColours != null && 2 + character < petalColours.Length) rippleLooks[r].sharedMaterial = petalColours[2 + character];
            m_Busy = true;
            return true;
        }

        void Ensure()
        {
            if (m_Petals != null) return;
            m_Petals = new Petal[petals.Length];
            for (int i = 0; i < m_Petals.Length; i++) m_Petals[i].start = -10f;
            m_RippleStart = new float[ripples.Length];
            m_RippleAt = new Vector3[ripples.Length];
            m_RippleMeshes = new Mesh[ripples.Length];
            for (int i = 0; i < ripples.Length; i++)
            {
                m_RippleStart[i] = -10f;
                m_RippleMeshes[i] = new Mesh { name = "Ripple" };
                m_RippleMeshes[i].MarkDynamic();
                if (rippleShapes[i] != null) rippleShapes[i].sharedMesh = m_RippleMeshes[i];
            }
        }

        /// <summary>The squash's height change at <paramref name="t"/> seconds: down 6%, a small spring back, home.</summary>
        public static float SquashAt(float t) =>
            t <= 0f || t >= SquashSeconds ? 0f : Squash * Mathf.Exp(-t * 9f) * Mathf.Cos(t * 26f) * (1f - t / SquashSeconds);

        void Update()
        {
            if (m_Petals == null || !m_Busy) return; // idle: listens, moves nothing
            float now = Time.time;
            bool busy = false;
            if (m_Squashing != null)
            {
                float t = now - m_SquashStart;
                if (t >= SquashSeconds) EndSquash();
                else if (m_Squashing.transform.localScale != m_LastSet)
                {
                    // something else is sizing it (a station appearing from the cloud): let go, change nothing
                    m_Squashing = null;
                    m_SquashStart = -1f;
                }
                else
                {
                    float s = SquashAt(t);
                    m_LastSet = Vector3.Scale(m_SquashBase, new Vector3(1f + s * 0.5f, 1f - s, 1f + s * 0.5f));
                    m_Squashing.transform.localScale = m_LastSet;
                    busy = true;
                }
            }
            int active = 0;
            for (int i = 0; i < petals.Length; i++)
            {
                var p = m_Petals[i];
                float t = now - p.start;
                var part = petals[i];
                if (part == null) continue;
                if (t < 0f || t >= PetalSeconds)
                {
                    if (park != null && part.position != park.position) { part.position = park.position; part.localScale = Vector3.one * 0.002f; }
                    if (petalLooks[i] != null && petalLooks[i].enabled) petalLooks[i].enabled = false; // parked: not drawn at all
                    continue;
                }
                active++;
                if (petalLooks[i] != null && !petalLooks[i].enabled) petalLooks[i].enabled = true;
                // up and out, slowing in the air, drifting down like a petal (a little sideways sway)
                var pos = p.from + p.velocity * t * (1f - 0.35f * t) + Vector3.down * (0.9f * t * t);
                pos += new Vector3(Mathf.Sin(t * 9f + i), 0f, Mathf.Cos(t * 7f + i)) * 0.012f * t;
                part.position = pos;
                part.rotation = Quaternion.Euler(p.spin * t);
                float grow = Mathf.Min(1f, t / 0.08f) * (1f - Mathf.SmoothStep(0.55f, 1f, t / PetalSeconds));
                part.localScale = Vector3.one * Mathf.Max(0.002f, grow * p.size);
            }
            ActivePetals = active;
            for (int i = 0; i < ripples.Length; i++)
            {
                float t = now - m_RippleStart[i];
                var ring = ripples[i];
                if (ring == null) continue;
                if (t < 0f || t >= RippleSeconds)
                {
                    if (park != null && ring.position != park.position) { ring.position = park.position; Ring(m_RippleMeshes[i], 0.001f, 0.0005f); }
                    if (rippleLooks[i] != null && rippleLooks[i].enabled) rippleLooks[i].enabled = false;
                    continue;
                }
                busy = true;
                if (rippleLooks[i] != null && !rippleLooks[i].enabled) rippleLooks[i].enabled = true;
                float u = t / RippleSeconds;
                ring.position = m_RippleAt[i];
                ring.rotation = Quaternion.identity;
                float radius = 0.05f + 0.33f * (1f - (1f - u) * (1f - u));
                Ring(m_RippleMeshes[i], radius, 0.012f * (1f - u) + 0.0008f); // widens and thins away (no fade, no blink)
            }
            if (!busy && active == 0) m_Busy = false;
        }

        void EndSquash()
        {
            if (m_Squashing != null && m_Squashing.transform.localScale == m_LastSet) m_Squashing.transform.localScale = m_SquashBase;
            m_Squashing = null;
            m_SquashStart = -1f;
        }

        static readonly Vector3[] s_Ring = new Vector3[96];
        static int[] s_RingTris;

        /// <summary>A flat ring, double-sided, of <paramref name="radius"/> and band <paramref name="width"/>.</summary>
        static void Ring(Mesh mesh, float radius, float width)
        {
            const int n = 48;
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.PI * 2f / n;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                s_Ring[k * 2] = d * Mathf.Max(0f, radius - width / 2f);
                s_Ring[k * 2 + 1] = d * (radius + width / 2f);
            }
            if (s_RingTris == null)
            {
                s_RingTris = new int[n * 12];
                for (int k = 0; k < n; k++)
                {
                    int a = k * 2, b = a + 1, c = (k + 1) % n * 2, d = c + 1, o = k * 12;
                    s_RingTris[o] = a; s_RingTris[o + 1] = c; s_RingTris[o + 2] = b;
                    s_RingTris[o + 3] = b; s_RingTris[o + 4] = c; s_RingTris[o + 5] = d;
                    s_RingTris[o + 6] = a; s_RingTris[o + 7] = b; s_RingTris[o + 8] = c;
                    s_RingTris[o + 9] = b; s_RingTris[o + 10] = d; s_RingTris[o + 11] = c;
                }
            }
            mesh.vertices = s_Ring;
            if (mesh.triangles.Length != s_RingTris.Length)
            {
                mesh.triangles = s_RingTris;
                var up = new Vector3[s_Ring.Length];
                for (int k = 0; k < up.Length; k++) up[k] = Vector3.up;
                mesh.normals = up;
            }
            mesh.RecalculateBounds();
        }
    }
}
