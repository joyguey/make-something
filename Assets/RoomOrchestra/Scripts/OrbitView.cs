using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// One target's echo orbit: a clock-face ring (16 ticks, bigger on beats) facing the player, with
    /// a fixed marker at the top. Each echo is a mini of its orb that travels clockwise and passes the
    /// marker exactly when its repeat sounds; one revolution is one bar. Minis are pre-placed in the
    /// scene and parked at the ring's centre when unused (PICO Spatial: no runtime objects, no
    /// scale-hiding, nothing changes at startup).
    /// </summary>
    public class OrbitView : MonoBehaviour
    {
        [SerializeField] InstrumentTarget target;
        [SerializeField, Min(0.05f)] float radius = 0.3f;
        [Tooltip("Where unused minis wait, hidden (inside the hub or the target).")]
        [SerializeField] Transform parkPoint;
        [Tooltip("Six minis per orb, in OrbType order: Flutter, Bop, Boom.")]
        [SerializeField] Transform[] flutterMinis;
        [SerializeField] Transform[] bopMinis;
        [SerializeField] Transform[] boomMinis;
        [SerializeField, Min(1f)] float selectedScale = 1.5f;
        [Tooltip("The play marker at the top of the ring.")]
        [SerializeField] Transform marker;
        [Tooltip("Tilt of the ring from flat, degrees: 0 = a flat halo, 90 = an upright clock face.")]
        [SerializeField, Range(0f, 90f)] float tilt = HaloTilt;
        [Tooltip("Turn to face the player as they walk around, so echoes always read clockwise from where they stand.")]
        [SerializeField] bool faceViewer = true;

        /// <summary>
        /// Rings are halos around their object, tipped toward the player so the echoes read clearly; the
        /// marker sits on the far side (12 o'clock as the player sees it).
        /// </summary>
        public const float HaloTilt = 35f;
        const float StartTurningDegrees = 4f, StopTurningDegrees = 0.3f, TurnSeconds = 0.6f;
        bool m_Turning;

        readonly Dictionary<long, Transform> m_ByEcho = new();
        readonly Dictionary<Transform, long> m_ByMini = new();
        readonly Dictionary<Transform, Vector3> m_BaseScale = new();
        readonly HashSet<long> m_Seen = new();
        readonly List<long> m_Stale = new();
        double m_LastPhase = -1;

        /// <summary>An echo just passed the top marker (its note is sounding): rainforest sparkles flare.</summary>
        public static event System.Action<InstrumentTarget> NotePassed;

        public InstrumentTarget Target => target;
        public float Radius => radius;

        public void Configure(InstrumentTarget owner, Transform park, Transform[] flutter, Transform[] bop, Transform[] boom, float ringRadius, Transform playMarker = null)
        {
            marker = playMarker;
            target = owner;
            parkPoint = park;
            flutterMinis = flutter;
            bopMinis = bop;
            boomMinis = boom;
            radius = ringRadius;
        }

        /// <summary>
        /// World position of an echo at <paramref name="step"/> when the bar is at <paramref name="phase"/>.
        /// At phase == step/16 the echo is exactly at the top marker. Clockwise as the player sees it.
        /// </summary>
        public Vector3 PositionAt(double phase, int step) => PointAt(phase - step / (double)MusicClock.StepsPerBar, radius);

        /// <summary>An echo's place on the ring, <paramref name="step"/>/16 of a turn clockwise from the top.</summary>
        public Vector3 SlotPosition(int step) => PointAt(step / (double)MusicClock.StepsPerBar, radius);

        public Transform Marker => marker;

        Vector3 PointAt(double turns, float r)
        {
            float angle = (float)(turns * 2.0 * System.Math.PI);
            return transform.TransformPoint(new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) * r);
        }

        public Vector3 MarkerPosition => transform.TransformPoint(new Vector3(0f, radius, 0f));

        public float Tilt => tilt;

        /// <summary>
        /// The ring's rotation at <paramref name="ringPosition"/> for a player at <paramref name="viewer"/>:
        /// turned to face them (yaw only) and tipped back from upright to <paramref name="tiltDegrees"/>.
        /// Its local Y (12 o'clock) points away and up; its local -Z face looks up toward the player.
        /// </summary>
        public static Quaternion Facing(Vector3 ringPosition, Vector3 viewer, float tiltDegrees)
        {
            var away = ringPosition - viewer;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-6f) away = Vector3.forward;
            return Quaternion.LookRotation(away.normalized, Vector3.up) * Quaternion.Euler(90f - tiltDegrees, 0f, 0f);
        }

        /// <summary>Rotation that stands a ring's child upright (minis, marker) whatever the tilt.</summary>
        public static Quaternion Upright(float tiltDegrees) => Quaternion.Euler(-(90f - tiltDegrees), 0f, 0f);

        // Turn smoothly to face the player when they have walked round (a few degrees of slack, so small
        // head movements never set it turning and nothing is rewritten while the player sits still).
        void LateUpdate()
        {
            if (!faceViewer || Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds) return;
            if (!Viewer.TryGetPosition(out var head)) return;
            var want = Facing(transform.position, head, tilt);
            float off = Quaternion.Angle(transform.rotation, want);
            if (!m_Turning && off < StartTurningDegrees) return;
            m_Turning = off > StopTurningDegrees;
            transform.rotation = m_Turning
                ? Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-Time.deltaTime / TurnSeconds))
                : want;
        }

        /// <summary>The mini showing <paramref name="echoId"/>, or null (not on the ring yet).</summary>
        public Transform MiniFor(long echoId) => m_ByEcho.TryGetValue(echoId, out var mini) ? mini : null;

        /// <summary>The echo id shown by a mini, or 0.</summary>
        public long EchoFor(Transform mini) => mini != null && m_ByMini.TryGetValue(mini, out long id) ? id : 0;

        /// <summary>Places this target's echoes on the ring for the given bar phase; parks the rest.</summary>
        public void Sync(IReadOnlyList<EchoEvent> echoes, double phase, long selectedId)
        {
            m_Seen.Clear();
            foreach (var echo in echoes)
            {
                if (target == null || echo.Target != target.Instrument) continue;
                m_Seen.Add(echo.StableId);
                if (!m_ByEcho.TryGetValue(echo.StableId, out var mini))
                {
                    mini = FreeMini(echo.Orb);
                    if (mini == null) continue;
                    m_ByEcho[echo.StableId] = mini;
                    m_ByMini[mini] = echo.StableId;
                    if (mini.TryGetComponent<JellyBody>(out var waking)) waking.SetAwake(true);
                }
                else if (Crossed(m_LastPhase, phase, echo.Step))
                {
                    // Its note is sounding now: a little hop and smiling eyes.
                    if (mini.TryGetComponent<JellyBody>(out var body)) body.Hop();
                    if (mini.TryGetComponent<CharacterFace>(out var face)) face.Beam();
                    NotePassed?.Invoke(target);
                }
                Move(mini, PositionAt(phase, echo.Step));
                Scale(mini, echo.StableId == selectedId ? selectedScale : 1f);
            }

            m_Stale.Clear();
            foreach (var pair in m_ByEcho) if (!m_Seen.Contains(pair.Key)) m_Stale.Add(pair.Key);
            foreach (long id in m_Stale)
            {
                var mini = m_ByEcho[id];
                m_ByEcho.Remove(id);
                m_ByMini.Remove(mini);
                Scale(mini, 1f);
                if (mini.TryGetComponent<JellyBody>(out var sleeping)) sleeping.SetAwake(false); // rest wholly inside the hub
                if (parkPoint != null) Move(mini, parkPoint.position);
            }
            m_LastPhase = phase;
            ShowOnlyTheRing();
        }

        // Owner, headset 2026-11-12: a mini sat on top of the lamp, inside its ring, before anything was played. Waiting
        // minis rested in the hub, trusting it to hide them; PICO XR mode draws only what is switched on, so a waiting
        // mini is not drawn at all now, and a mini on the ring always is.
        readonly Dictionary<Transform, Renderer[]> m_Looks = new();

        void ShowOnlyTheRing()
        {
            foreach (var pool in new[] { flutterMinis, bopMinis, boomMinis })
            {
                if (pool == null) continue;
                foreach (var mini in pool)
                {
                    if (mini == null) continue;
                    if (!m_Looks.TryGetValue(mini, out var looks)) m_Looks[mini] = looks = mini.GetComponentsInChildren<Renderer>(true);
                    bool on = m_ByMini.ContainsKey(mini);
                    foreach (var r in looks) if (r != null && r.enabled != on) r.enabled = on;
                }
            }
        }

        Transform FreeMini(OrbType orb)
        {
            var pool = orb switch { OrbType.Flutter => flutterMinis, OrbType.Bop => bopMinis, _ => boomMinis };
            if (pool == null) return null;
            foreach (var mini in pool)
                if (mini != null && !m_ByMini.ContainsKey(mini)) return mini;
            return null;
        }

        /// <summary>True when the bar phase passed <paramref name="step"/>'s place this frame (wrapping at the bar line).</summary>
        public static bool Crossed(double lastPhase, double phase, int step)
        {
            if (lastPhase < 0 || lastPhase == phase) return false;
            double at = step / (double)MusicClock.StepsPerBar;
            return lastPhase < phase ? at > lastPhase && at <= phase : at > lastPhase || at <= phase;
        }

        static void Move(Transform t, Vector3 position)
        {
            if ((t.position - position).sqrMagnitude > 1e-10f) t.position = position;
        }

        void Scale(Transform t, float factor)
        {
            if (!m_BaseScale.TryGetValue(t, out var baseScale)) m_BaseScale[t] = baseScale = t.localScale;
            var target = baseScale * factor;
            if (t.localScale != target) t.localScale = target;
        }
    }
}
