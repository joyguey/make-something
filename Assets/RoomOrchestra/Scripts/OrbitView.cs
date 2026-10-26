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

        readonly Dictionary<long, Transform> m_ByEcho = new();
        readonly Dictionary<Transform, long> m_ByMini = new();
        readonly Dictionary<Transform, Vector3> m_BaseScale = new();
        readonly HashSet<long> m_Seen = new();
        readonly List<long> m_Stale = new();

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
                if (parkPoint != null) Move(mini, parkPoint.position);
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
