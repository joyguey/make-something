using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Diagnostic: periodically logs any enabled renderer whose world bounds exceed a size limit,
    /// to tell Unity-side transform problems apart from Spatial-renderer display problems.
    /// </summary>
    public class RenderBoundsProbe : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float maxSize = 1.3f;
        [SerializeField, Min(1f)] float interval = 10f;

        float m_Next = 2f;

        void Update()
        {
            if (Time.time < m_Next) return;
            m_Next = Time.time + interval;

            int checkedCount = 0, offenders = 0;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                checkedCount++;
                Vector3 s = r.bounds.size;
                if (Mathf.Max(s.x, Mathf.Max(s.y, s.z)) <= maxSize) continue;
                offenders++;
                var t = r.transform;
                Debug.Log($"[RoomOrchestra] Probe LARGE {Path(t)} size={s:F2} pos={t.position:F2} rot={t.rotation.eulerAngles:F0} lossyScale={t.lossyScale:F3}");
            }
            Debug.Log($"[RoomOrchestra] Probe checked={checkedCount} large={offenders}");
        }

        static string Path(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}
