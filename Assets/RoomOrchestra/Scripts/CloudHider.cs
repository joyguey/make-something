using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Things that wait shrunk inside the ball stand's cloud (stations before the lesson brings them, the confirm card,
    /// spare balls, poof clouds, the glowing spots...) were hidden only by the cloud around them, a trick from PICO
    /// Spatial, which drew switched-off objects. In PICO XR mode bits poked out (owner, headset 2026-11-08: "when the game
    /// started up, I still saw some artifacts"). Now anything whose look sits wholly inside the cloud's core is not drawn
    /// at all, and is drawn again once it leaves. Only looks this switched off are switched back on.
    /// </summary>
    public class CloudHider : MonoBehaviour
    {
        /// <summary>Anything wholly within this distance of the park point is waiting in the cloud (m).</summary>
        public const float CoreRadius = 0.14f;
        const float CheckSeconds = 0.1f;

        [SerializeField] Transform park;
        [SerializeField] Transform[] cloud;

        Renderer[] m_All;
        readonly HashSet<Renderer> m_Hidden = new();
        float m_Next, m_Rescan;
        BallLauncher m_Launcher;

        public void Configure(Transform parkPoint, Transform[] cloudLooks)
        {
            park = parkPoint;
            cloud = cloudLooks;
        }

        public int HiddenCount => m_Hidden.Count;

        bool IsCloud(Transform t)
        {
            if (cloud == null) return false;
            foreach (var c in cloud) if (c != null && t.IsChildOf(c)) return true;
            return false;
        }

        void LateUpdate()
        {
            if (park == null || Time.unscaledTime < m_Next) return;
            m_Next = Time.unscaledTime + CheckSeconds;
            if (m_All == null || Time.unscaledTime > m_Rescan)
            {
                m_Rescan = Time.unscaledTime + 5f; // runtime-made looks (glows, bands) join in
                m_All = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            }
            if (m_Launcher == null) m_Launcher = FindFirstObjectByType<BallLauncher>();
            var ready = m_Launcher != null ? m_Launcher.ReadyBall : null;
            var at = park.position;
            foreach (var r in m_All)
            {
                if (r == null || IsCloud(r.transform)) continue;
                if (ready != null && r.transform.IsChildOf(ready.transform)) continue;
                var b = r.bounds;
                bool inside = (b.center - at).sqrMagnitude < CoreRadius * CoreRadius && b.extents.magnitude < CoreRadius;
                if (inside)
                {
                    if (r.enabled && m_Hidden.Add(r)) r.enabled = false;
                }
                else if (m_Hidden.Remove(r)) r.enabled = true;
            }
        }
    }
}
