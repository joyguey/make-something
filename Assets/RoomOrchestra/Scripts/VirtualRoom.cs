using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The virtual starting room (Version 1 feedback; owner: start in a fully virtual room that tears down to reveal
    /// the real room): a simple cosy room of panels (walls, ceiling, floor) around the stage, standing through the
    /// title, the opening and the guided song. When the song is done (or skipped), it comes apart panel by panel,
    /// a ripple from the far wall outward with little poofs, and the player's own room is the stage from then on.
    /// PICO Spatial: no runtime objects and nothing switched off, so a panel that has gone is shrunk and parked
    /// inside the ball stand's cloud. Walls and ceiling are solid to thrown balls (each bounces off with its own sound,
    /// <see cref="BounceSurface"/>) until they go; the floor panels are not. Nothing taps them.
    /// </summary>
    public class VirtualRoom : MonoBehaviour
    {
        /// <summary>How fast the ripple spreads from the far wall (m/s), and how long one panel takes to go.</summary>
        public const float RippleSpeed = 2.4f, PanelSeconds = 0.6f;
        /// <summary>A gone panel's scale (share of its own) while parked in the cloud.</summary>
        public const float ParkedScale = 0.04f;
        const float DriftMetres = 0.35f, TiltDegrees = 30f;
        /// <summary>Every this-many panels gets a little poof as it goes.</summary>
        const int PoofEvery = 4;
        const float PanelPoofSize = 1.2f;

        [SerializeField] Transform[] panels;
        [SerializeField] Vector3[] homePositions, homeScales;
        [SerializeField] Quaternion[] homeRotations;
        [Tooltip("Where the ripple starts (the far wall's middle), in this object's space.")]
        [SerializeField] Vector3 rippleFrom;
        [Tooltip("The room's middle, in this object's space: panels drift away from it as they go.")]
        [SerializeField] Vector3 middle;
        [SerializeField] Transform park;
        [SerializeField] bool gone;

        float m_Start = -1f;
        float[] m_Delays;
        bool[] m_Done;
        Vector3 m_ParkedAt;

        public static VirtualRoom Instance { get; private set; }
        public bool Standing => !gone && m_Start < 0f;
        public bool TearingDown => !gone && m_Start >= 0f;
        public bool Gone => gone;
        public System.Collections.Generic.IReadOnlyList<Transform> Panels => panels;
        /// <summary>The whole tear-down, first panel to last.</summary>
        public float TearDownSeconds { get; private set; }

        /// <summary>Records the room as built (scene setup).</summary>
        public void Configure(Transform[] roomPanels, Vector3 rippleStartLocal, Vector3 middleLocal, Transform parkPoint)
        {
            panels = roomPanels;
            rippleFrom = rippleStartLocal;
            middle = middleLocal;
            park = parkPoint;
            gone = false;
            homePositions = new Vector3[panels.Length];
            homeScales = new Vector3[panels.Length];
            homeRotations = new Quaternion[panels.Length];
            for (int i = 0; i < panels.Length; i++)
            {
                homePositions[i] = panels[i].localPosition;
                homeScales[i] = panels[i].localScale;
                homeRotations[i] = panels[i].localRotation;
            }
        }

        /// <summary>When a panel starts to go: its distance from the far wall's middle, at the ripple's speed.</summary>
        public static float DelayFor(float distance) => Mathf.Min(MaxDelaySeconds, Mathf.Max(0f, distance) / RippleSpeed);
        /// <summary>The meadow reaches the horizon: its farthest pieces still start going within this (s).</summary>
        public const float MaxDelaySeconds = 3.2f;

        void OnEnable() => Instance = this;

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        void Delays()
        {
            if (m_Delays != null) return;
            m_Delays = new float[panels.Length];
            m_Done = new bool[panels.Length];
            TearDownSeconds = 0f;
            for (int i = 0; i < panels.Length; i++)
            {
                m_Delays[i] = DelayFor(Vector3.Distance(homePositions[i], rippleFrom));
                TearDownSeconds = Mathf.Max(TearDownSeconds, m_Delays[i] + PanelSeconds);
            }
        }

        /// <summary>The room comes apart, far wall first (the guided song's end, or Skip).</summary>
        public void TearDown()
        {
            if (gone || m_Start >= 0f || panels == null) return;
            Delays();
            m_Start = Time.time;
            PoofCloud.PoofAt(transform.TransformPoint(rippleFrom), JungleDirector.ClearSmokeSize); // one soft whoosh as it starts
            Debug.Log($"[RoomOrchestra] Virtual room: coming apart ({panels.Length} panels, {TearDownSeconds:0.0} s); your room is the stage now");
        }

        /// <summary>Gone at once (skipping the opening, and the state the layout tests check).</summary>
        public void HideNow()
        {
            if (panels == null) return;
            gone = true;
            m_Start = -1f;
            for (int i = 0; i < panels.Length; i++) Park(i);
        }

        /// <summary>
        /// One panel's pose <paramref name="progress"/> (0..1) of the way through going: it tips, drifts away from the
        /// room's middle and shrinks. Used by the tear-down and its previews.
        /// </summary>
        public void PosePanel(int i, float progress)
        {
            float u = Mathf.Clamp01(progress);
            float e = u * u * (3f - 2f * u);
            var away = homePositions[i] - middle;
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.up;
            panels[i].localPosition = homePositions[i] + away * (DriftMetres * e);
            panels[i].localRotation = homeRotations[i] * Quaternion.Euler(TiltDegrees * e, 0f, TiltDegrees * 0.5f * e);
            panels[i].localScale = homeScales[i] * Mathf.Lerp(1f, ParkedScale, e);
        }

        void Park(int i)
        {
            if (park == null) return;
            m_ParkedAt = park.position;
            panels[i].position = park.position;
            panels[i].localRotation = homeRotations[i];
            panels[i].localScale = ParkedScaleOf(homeScales[i]);
            if (panels[i].TryGetComponent<Collider>(out var solid)) solid.enabled = false; // nothing to hit in the cloud
            // PICO XR mode draws only what is switched on: a gone piece of the meadow is not drawn at all
            if (panels[i].TryGetComponent<Renderer>(out var look)) look.enabled = false;
        }

        /// <summary>A gone panel's scale: small enough to sit in the cloud whatever its own size.</summary>
        public static Vector3 ParkedScaleOf(Vector3 home)
        {
            float biggest = Mathf.Max(1f, Mathf.Max(Mathf.Abs(home.x), Mathf.Max(Mathf.Abs(home.y), Mathf.Abs(home.z))));
            return home * (ParkedScale / biggest);
        }

        void Update()
        {
            if (gone)
            {
                // The ball stand travels with the leaf; the parked panels go with it (written only when it moves).
                if (park != null && park.position != m_ParkedAt && Time.timeSinceLevelLoad >= JellyBody.StartDelaySeconds)
                    for (int i = 0; i < panels.Length; i++) Park(i);
                return;
            }
            if (m_Start < 0f) return;
            float t = Time.time - m_Start;
            bool all = true;
            for (int i = 0; i < panels.Length; i++)
            {
                if (m_Done[i]) continue;
                float local = t - m_Delays[i];
                if (local < 0f)
                {
                    all = false;
                    continue;
                }
                if (local >= PanelSeconds)
                {
                    m_Done[i] = true;
                    Park(i);
                    continue;
                }
                if (local < Time.deltaTime && i % PoofEvery == 0) PoofCloud.PoofAt(panels[i].position, PanelPoofSize);
                PosePanel(i, local / PanelSeconds);
                all = false;
            }
            if (all)
            {
                gone = true;
                Debug.Log("[RoomOrchestra] Virtual room: gone");
            }
        }
    }
}
