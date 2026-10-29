using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The leaf's stalk and the hole in the ground it grows from (owner, 2026-10-29). The stalk stands on the
    /// floor under the leaf's home wherever the leaf pad has carried it, and reaches up to the leaf even when
    /// Height lifts the controls (it used to float). In the opening the hole opens and the stalk grows up to
    /// meet the swooping leaf (<see cref="IntroSequence"/> sets <see cref="HoleOpen"/> and <see cref="Grown"/>).
    /// Writes only what changed, so it causes no movement at startup.
    /// </summary>
    public class LeafStalk : MonoBehaviour
    {
        /// <summary>The hole's scale while closed: a speck (not zero: PICO Spatial drops zero-size objects).</summary>
        public const float Hidden = 0.01f;
        /// <summary>The stalk's smallest size (metres), unseen inside the closed hole.</summary>
        public const float Stub = 0.004f;
        const float HoleLift = 0.002f; // just above the floor, so it never flickers into it

        [SerializeField] Transform stalk;
        [SerializeField] Transform hole;
        [Tooltip("The underside of the leaf at home, in the leaf pad's space (the stalk's top).")]
        [SerializeField] Vector3 leafUnderside;
        [SerializeField] float width = 0.03f;
        [SerializeField, Range(0f, 1f)] float grown = 1f;
        [SerializeField, Range(0f, 1f)] float holeOpen = 1f;

        public Transform Stalk => stalk;
        public Transform Hole => hole;
        public float Grown { get => grown; set { grown = Mathf.Clamp01(value); Apply(); } }
        public float HoleOpen { get => holeOpen; set { holeOpen = Mathf.Clamp01(value); Apply(); } }

        public void Configure(Transform stalkPart, Transform holePart, Vector3 underside, float stalkWidth, float startGrown, float startOpen)
        {
            stalk = stalkPart;
            hole = holePart;
            leafUnderside = underside;
            width = stalkWidth;
            grown = startGrown;
            holeOpen = startOpen;
            Apply();
        }

        void LateUpdate() => Apply();

        /// <summary>Places the stalk on the floor (world y = 0) under the leaf's home and sizes it.</summary>
        public void Apply()
        {
            if (stalk == null || hole == null || transform.parent == null) return;
            var pad = transform.parent;
            var top = pad.TransformPoint(leafUnderside);
            float height = Mathf.Max(top.y * grown, Stub);
            float thick = Mathf.Max(width * Mathf.Min(1f, grown * 4f), Stub); // thickens as it starts to grow
            var foot = new Vector3(top.x, 0f, top.z);
            Set(stalk, foot + Vector3.up * (height / 2f), new Vector3(thick, height, thick));
            float open = Mathf.Max(holeOpen, Hidden);
            Set(hole, foot + Vector3.up * HoleLift, new Vector3(open, 1f, open));
        }

        static void Set(Transform t, Vector3 position, Vector3 scale)
        {
            if ((t.position - position).sqrMagnitude > 1e-12f) t.position = position;
            if (t.localScale != scale) t.localScale = scale;
        }
    }
}
