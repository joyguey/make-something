using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// A leaf control that waits out of sight during the guided song, so the first session offers one thing to try
    /// (George, 2026-11-03: "give me one thing to try and let me hear or see the result before introducing the
    /// rest"). It is tucked small under the leaf, which hides it from above and carries it wherever the leaf goes;
    /// when the lesson ends or is skipped it comes back in a small poof. Pause, Undo, Recenter, Height, View,
    /// language and Quit never tuck away (CLAUDE.md: pause and reset available any time; comfort settings first).
    /// PICO Spatial: nothing is created or switched off at runtime, only moved, scaled and its colliders disabled.
    /// </summary>
    public class TuckAway : MonoBehaviour
    {
        /// <summary>Tucked size as a share of its own: small parts stay above the near-zero scale limit.</summary>
        public const float TuckedScale = 0.6f;
        /// <summary>How far below the leaf's top a tucked control sits.</summary>
        public const float TuckDepth = 0.08f;
        /// <summary>How far toward the leaf's middle it moves, so no edge of it peeks out under the rim.</summary>
        public const float TowardMiddle = 0.75f;
        public const float AppearAfterSeconds = 0.15f;
        public const float PoofSize = 0.14f;

        [SerializeField] Vector3 homePosition, homeScale, tuckedPosition;
        [SerializeField] bool tucked;
        [SerializeField] Collider[] switchedOff;
        // Owner, headset 2026-11-08: "there's an exit button that is hidden beneath the leaf when it comes in": seen from
        // below as the leaf floats down, the tucked Clear hung under it. Tucked controls are not drawn at all now.
        [SerializeField] Renderer[] hidden;

        float m_AppearAt = -1f;

        public bool Tucked => tucked;
        public bool Appearing => m_AppearAt >= 0f;
        /// <summary>Records home (scene setup, with the control in place) and its hiding place under the leaf.</summary>
        public void Configure(Vector3 leafMiddle)
        {
            homePosition = transform.localPosition;
            homeScale = transform.localScale;
            var parent = transform.parent;
            var under = Vector3.Lerp(transform.position, leafMiddle, TowardMiddle);
            under.y = leafMiddle.y - TuckDepth;
            tuckedPosition = parent != null ? parent.InverseTransformPoint(under) : under;
            tucked = false;
        }

        // Found each time (rare): a registry kept in OnEnable is empty in the Editor's tools outside Play mode.
        static TuckAway[] Every() => FindObjectsByType<TuckAway>(FindObjectsSortMode.None);

        public void TuckNow()
        {
            m_AppearAt = -1f;
            if (!tucked)
            {
                var off = new List<Collider>();
                foreach (var c in GetComponentsInChildren<Collider>())
                    if (c.enabled)
                    {
                        c.enabled = false;
                        off.Add(c);
                    }
                switchedOff = off.ToArray();
                var unseen = new List<Renderer>();
                foreach (var r in GetComponentsInChildren<Renderer>())
                    if (r.enabled)
                    {
                        r.enabled = false;
                        unseen.Add(r);
                    }
                hidden = unseen.ToArray();
            }
            tucked = true;
            Set(tuckedPosition, homeScale * TuckedScale);
        }

        public void ShowNow()
        {
            m_AppearAt = -1f;
            if (!tucked) return;
            tucked = false;
            Set(homePosition, homeScale);
            if (switchedOff != null)
                foreach (var c in switchedOff)
                    if (c != null) c.enabled = true;
            switchedOff = null;
            if (hidden != null)
                foreach (var r in hidden)
                    if (r != null) r.enabled = true;
            hidden = null;
        }

        /// <summary>A small poof where it belongs, and it pops up inside it.</summary>
        public void Reveal()
        {
            if (!tucked || m_AppearAt >= 0f) return;
            var parent = transform.parent;
            PoofCloud.PoofAt(parent != null ? parent.TransformPoint(homePosition) : homePosition, PoofSize);
            m_AppearAt = Time.time + AppearAfterSeconds;
        }

        void Update()
        {
            if (m_AppearAt >= 0f && Time.time >= m_AppearAt) ShowNow();
        }

        void Set(Vector3 position, Vector3 scale)
        {
            if (transform.localPosition != position) transform.localPosition = position;
            if (transform.localScale != scale) transform.localScale = scale;
        }

        public static void TuckAll()
        {
            foreach (var t in Every()) t.TuckNow();
        }

        public static void RevealAll()
        {
            foreach (var t in Every()) t.Reveal();
        }

        public static void ShowAllNow()
        {
            foreach (var t in Every()) t.ShowNow();
        }
    }
}
