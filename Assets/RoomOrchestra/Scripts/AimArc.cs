using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Dotted throw trajectory plus a landing ring. Built from pre-placed primitives because
    /// PICO Spatial does not render LineRenderer; dots and ring must not have colliders.
    /// PICO's Spatial renderer does not reliably follow SetActive, so unused parts are parked
    /// at <see cref="parkPoint"/> (inside an opaque object) instead of being hidden.
    /// </summary>
    public class AimArc : MonoBehaviour
    {
        [SerializeField] Transform[] dots;
        [Tooltip("Ring shown where the ball will land on the aimed instrument.")]
        [SerializeField] Transform landingMarker;
        [Tooltip("Where hidden parts are parked (inside the ball stand's cloud).")]
        [SerializeField] Transform parkPoint;

        [Tooltip("For this long after start, dots are nudged every frame so the Spatial renderer picks up " +
                 "their positions even if it connects after the first update.")]
        [SerializeField, Min(0f)] float startupSyncSeconds = 10f;

        const float k_Nudge = 0.0005f; // half a millimetre: invisible, but a real transform change

        public bool IsVisible { get; private set; }
        public Vector3 LandingPoint { get; private set; }

        Vector3 ParkPosition => parkPoint != null ? parkPoint.position : new Vector3(0f, -5f, 0f);

        /// <summary>Lays the dots along a ballistic path from <paramref name="start"/> for <paramref name="duration"/> seconds.</summary>
        public void Show(Vector3 start, Vector3 velocity, float duration, bool hasTarget)
        {
            Vector3 g = Physics.gravity;
            Vector3 nudge = Time.timeSinceLevelLoad < startupSyncSeconds && (Time.frameCount & 1) == 0
                ? new Vector3(k_Nudge, 0f, 0f)
                : Vector3.zero;

            for (int i = 0; i < dots.Length; i++)
            {
                float t = duration * (i + 1) / (dots.Length + 1);
                dots[i].position = start + velocity * t + 0.5f * t * t * g + nudge;
            }

            LandingPoint = start + velocity * duration + 0.5f * duration * duration * g;
            SetMarker(hasTarget, LandingPoint + nudge);
            IsVisible = true;
        }

        public void Hide()
        {
            foreach (var d in dots)
                d.position = ParkPosition;
            SetMarker(false, Vector3.zero);
            IsVisible = false;
        }

        Vector3 m_MarkerScale;

        void Awake()
        {
            if (landingMarker != null) m_MarkerScale = landingMarker.localScale;
        }

        // The ring is wider than the park spot, so a parked ring is also shrunk to nothing.
        void SetMarker(bool visible, Vector3 position)
        {
            if (landingMarker == null) return;
            landingMarker.position = visible ? position : ParkPosition;
            landingMarker.localScale = visible ? m_MarkerScale : m_MarkerScale * 0.001f;
        }
    }
}
