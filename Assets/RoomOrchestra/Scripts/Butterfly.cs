using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// A rainforest butterfly: it waits hidden inside the lamp shade until the song is layered and
    /// harmonious enough (JungleDirector), then flutters out, wanders a lazy looping path around the stage
    /// leaving a short trail of glowing sparkles, and flies home again when the song is cleared.
    /// Everything is authored in the scene and only moved (PICO Spatial: no runtime objects); reduced
    /// motion holds it still.
    /// </summary>
    public class Butterfly : MonoBehaviour
    {
        public static bool ReducedMotion { get; set; }

        [SerializeField] Transform wingL, wingR;
        [Tooltip("Sparkle dots that follow the butterfly, nearest first.")]
        [SerializeField] Transform[] trail;
        [Tooltip("Centre of the loop (stage space) and its half-size in x, y, z (m).")]
        [SerializeField] Vector3 centre = new Vector3(0f, 1.5f, 2.3f), size = new Vector3(1.4f, 0.25f, 0.45f);
        [SerializeField] float lapSeconds = 26f, phase;
        [SerializeField] float flapsPerSecond = 5f;
        [Tooltip("Where it waits, hidden (stage space): inside the lamp shade.")]
        [SerializeField] Vector3 home;

        const float TravelSeconds = 3f;
        bool m_Out, m_Parked = true;
        float m_TravelStart = -100f;

        const int HistoryLength = 64;
        const float SampleSeconds = 0.05f, TrailSpacing = 2.5f; // history samples between trail dots
        readonly Vector3[] m_History = new Vector3[HistoryLength];
        int m_Head, m_Count;
        float m_NextSample;
        Vector3[] m_TrailScale;

        public Transform WingL => wingL;
        public System.Collections.Generic.IReadOnlyList<Transform> Trail => trail;

        public bool IsOut => m_Out;
        public bool AtHome => !m_Out && Time.time - m_TravelStart >= TravelSeconds;
        public Vector3 Home => home;

        /// <summary>Sends it out to its loop, or back home into the lamp.</summary>
        public void SetOut(bool goOut)
        {
            if (m_Out == goOut) return;
            m_Out = goOut;
            m_TravelStart = Time.time;
            Debug.Log($"[RoomOrchestra] Jungle: {name} {(goOut ? "flutters out of the lamp" : "flies home")}");
        }

        public void Configure(Transform left, Transform right, Transform[] sparkleTrail, Vector3 loopCentre, Vector3 loopSize, float lap, float startPhase, Vector3 hideAt)
        {
            home = hideAt;
            wingL = left;
            wingR = right;
            trail = sparkleTrail;
            centre = loopCentre;
            size = loopSize;
            lapSeconds = lap;
            phase = startPhase;
        }

        /// <summary>Where the loop puts the butterfly at time <paramref name="t"/> (stage space): a lazy figure-eight.</summary>
        public Vector3 PathAt(float t)
        {
            float a = (t / lapSeconds + phase) * 2f * Mathf.PI;
            return centre + new Vector3(Mathf.Sin(a) * size.x, Mathf.Sin(a * 2f + 1f) * size.y, Mathf.Sin(a * 2f) * size.z);
        }

        void Update()
        {
            if (ReducedMotion || Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds) return;
            var stage = transform.parent;
            if (AtHome)
            {
                // Resting out of sight inside the lamp: settle once, then write nothing.
                if (!m_Parked)
                {
                    m_Parked = true;
                    var at = stage != null ? stage.TransformPoint(home) : home;
                    transform.position = at;
                    SetWings(RestAngle);
                    ParkTrail(at);
                }
                return;
            }
            m_Parked = false;
            float t = Time.time;
            // Between home and the loop, glide along an eased blend so it emerges from (or returns to) the lamp.
            float travel = Mathf.Clamp01((t - m_TravelStart) / TravelSeconds);
            float outness = m_Out ? Smooth(travel) : 1f - Smooth(travel);
            Vector3 Local(float at) => Vector3.Lerp(home, PathAt(at), outness);
            var position = stage != null ? stage.TransformPoint(Local(t)) : Local(t);
            var ahead = stage != null ? stage.TransformPoint(Local(t + 0.2f)) : Local(t + 0.2f);
            var heading = ahead - position;
            heading.y *= 0.3f; // bank gently, never nose-dive
            transform.SetPositionAndRotation(position, heading.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(heading, Vector3.up) : transform.rotation);

            // Flap: wings held up in a V, beating between nearly closed and open, with a soft glide now and then.
            float glide = 0.5f + 0.5f * Mathf.Sin(t * 0.7f + phase * 10f);
            float beat = Mathf.Sin(t * flapsPerSecond * 2f * Mathf.PI) * 0.5f + 0.5f;
            SetWings(Mathf.Lerp(RestAngle - 10f, Mathf.Lerp(RestAngle + 15f, 80f, glide), beat));

            UpdateTrail(position, t);
        }

        static float Smooth(float u) => u * u * (3f - 2f * u);

        void ParkTrail(Vector3 at)
        {
            if (trail == null) return;
            m_Count = 0;
            foreach (var dot in trail) dot.position = at;
        }

        /// <summary>Wing lift above flat (degrees) while resting: a V that shows the wings' faces to the player.</summary>
        public const float RestAngle = 40f;

        /// <summary>Lifts both wings by <paramref name="degrees"/> (rotation about the body's long axis).</summary>
        public void SetWings(float degrees)
        {
            // About +Z, positive turns +X toward +Y: the right wing (+X) rises with +angle, the left with -angle.
            if (wingL != null) wingL.localRotation = Quaternion.Euler(0f, 0f, -degrees);
            if (wingR != null) wingR.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        void UpdateTrail(Vector3 position, float t)
        {
            if (trail == null || trail.Length == 0) return;
            if (m_TrailScale == null)
            {
                m_TrailScale = new Vector3[trail.Length];
                for (int i = 0; i < trail.Length; i++) m_TrailScale[i] = trail[i].localScale;
            }
            if (t >= m_NextSample)
            {
                m_NextSample = t + SampleSeconds;
                m_Head = (m_Head + 1) % HistoryLength;
                m_History[m_Head] = position;
                m_Count = Mathf.Min(m_Count + 1, HistoryLength);
            }
            for (int i = 0; i < trail.Length; i++)
            {
                int back = Mathf.Min(Mathf.RoundToInt((i + 1) * TrailSpacing), m_Count - 1);
                var at = m_History[((m_Head - back) % HistoryLength + HistoryLength) % HistoryLength];
                if (m_Count == 0) at = position;
                // Dots drift down a little and shrink with age; each twinkles on its own beat.
                float age = (i + 1f) / trail.Length;
                at += Vector3.down * 0.04f * age;
                float twinkle = 0.75f + 0.25f * Mathf.Sin(t * 6f + i * 1.7f);
                trail[i].position = at;
                trail[i].localScale = m_TrailScale[i] * Mathf.Max(0.25f, (1f - age * 0.8f) * twinkle);
                trail[i].rotation = Quaternion.Euler(0f, 0f, t * 90f + i * 40f);
            }
        }
    }
}
