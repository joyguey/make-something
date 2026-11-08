using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// One instrument's station (its furniture, note tiles, ring, vines and sparkles) that the guided song keeps
    /// out of sight until it is needed, then brings in with a big poof (owner, 2026-10-29: "they should appear
    /// in a large cloud of poof similar to how the objects disappear"). PICO Spatial: nothing is created or
    /// switched off at runtime, so a hidden station is shrunk and parked inside the ball stand's cloud, its
    /// colliders off so nothing hits or taps it there. The scene is saved hidden.
    /// </summary>
    public class StationReveal : MonoBehaviour
    {
        /// <summary>A hidden part's scale, as a share of its own: small enough for the cloud's core.</summary>
        public const float HiddenScale = 0.09f;
        /// <summary>The poof covers the station before it appears inside it.</summary>
        public const float AppearAfterSeconds = 0.2f;

        static readonly List<StationReveal> s_All = new();

        [SerializeField] InstrumentType instrument;
        [SerializeField] Transform[] parts;
        [SerializeField] Vector3[] homePositions, homeScales;
        [Tooltip("Each part's visible centre at home, in its parent's space: it lands on the park point.")]
        [SerializeField] Vector3[] homeCentres;
        [Tooltip("Where the cloud bursts: the station's middle, in this object's space.")]
        [SerializeField] Vector3 poofCentre;
        [SerializeField] float poofSize = JungleDirector.ClearSmokeSize;
        [SerializeField] Transform park;
        [SerializeField] bool hidden;
        [Tooltip("The colliders switched off while hidden.")]
        [SerializeField] Collider[] switchedOff;

        Vector3 m_ParkedAt;
        float m_AppearAt = -1f;

        public InstrumentType Instrument => instrument;
        public bool Hidden => hidden;
        /// <summary>The poof has burst and the station is about to appear in it.</summary>
        public bool Appearing => m_AppearAt >= 0f;
        public IReadOnlyList<Transform> Parts => parts;
        /// <summary>The station's middle at home, where its poof bursts.</summary>
        public Vector3 PoofCentre => transform.TransformPoint(poofCentre);

        /// <summary>Is this instrument's station out of sight (so the controller skips it)?</summary>
        public static bool IsHidden(InstrumentType type)
        {
            foreach (var station in s_All)
                if (station.instrument == type && station.hidden) return true;
            return false;
        }

        public static IReadOnlyList<StationReveal> All => s_All;

        /// <summary>Records the station at home (called by the scene setup, before hiding it).</summary>
        public void Configure(InstrumentType type, Transform[] stationParts, Transform parkPoint)
        {
            instrument = type;
            parts = stationParts;
            park = parkPoint;
            hidden = false;
            homePositions = new Vector3[parts.Length];
            homeScales = new Vector3[parts.Length];
            homeCentres = new Vector3[parts.Length];
            Bounds all = default;
            bool any = false;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                homePositions[i] = part.localPosition;
                homeScales[i] = part.localScale;
                var bounds = VisibleBounds(part);
                homeCentres[i] = part.parent != null ? part.parent.InverseTransformPoint(bounds.center) : bounds.center;
                if (!any) { all = bounds; any = true; }
                else all.Encapsulate(bounds);
            }
            poofCentre = transform.InverseTransformPoint(any ? all.center : transform.position);
        }

        static Bounds VisibleBounds(Transform part)
        {
            var renderers = part.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(part.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        void OnEnable() => s_All.Add(this);
        void OnDisable() => s_All.Remove(this);

        /// <summary>Into the cloud at once (the scene setup saves the station this way).</summary>
        public void HideNow()
        {
            m_AppearAt = -1f;
            if (!hidden)
            {
                var off = new List<Collider>();
                foreach (var part in parts)
                    foreach (var c in part.GetComponentsInChildren<Collider>())
                        if (c.enabled)
                        {
                            c.enabled = false;
                            off.Add(c);
                        }
                switchedOff = off.ToArray();
            }
            hidden = true;
            Park();
        }

        // Each part shrinks about its pivot so that its visible centre sits on the park point.
        void Park()
        {
            if (park == null) return;
            m_ParkedAt = park.position;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var parent = part.parent;
                var pivot = parent != null ? parent.TransformPoint(homePositions[i]) : homePositions[i];
                var centre = parent != null ? parent.TransformPoint(homeCentres[i]) : homeCentres[i];
                var at = m_ParkedAt - HiddenScale * (centre - pivot);
                part.localPosition = parent != null ? parent.InverseTransformPoint(at) : at;
                part.localScale = homeScales[i] * HiddenScale;
            }
        }

        /// <summary>A big poof where the station stands, and it appears inside it.</summary>
        public void Reveal()
        {
            if (!hidden || m_AppearAt >= 0f) return;
            PoofCloud.PoofAt(transform.TransformPoint(poofCentre), poofSize);
            m_AppearAt = Time.time + AppearAfterSeconds;
            Debug.Log($"[RoomOrchestra] Tutorial: the {instrument} appears in a poof");
        }

        /// <summary>Home at once, no poof.</summary>
        public void ShowNow()
        {
            m_AppearAt = -1f;
            if (!hidden) return;
            hidden = false;
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].localPosition = homePositions[i];
                parts[i].localScale = homeScales[i];
            }
            if (switchedOff != null)
                foreach (var c in switchedOff)
                    if (c != null) c.enabled = true;
            switchedOff = null;
            Physics.SyncTransforms();
        }

        void Update()
        {
            if (!hidden) return;
            if (m_AppearAt >= 0f)
            {
                if (Time.time >= m_AppearAt) ShowNow();
                return;
            }
            // The ball stand travels with the leaf; a hidden station goes with it (written only when it moves).
            if (park != null && park.position != m_ParkedAt) Park(); // at once: the stage recentres a second in (PICO XR)
        }
    }
}
