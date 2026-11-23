using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// A pitch column's tiles wear their instrument's colour (owner, 2026-11-19: rounded keycap tiles tinted by
    /// instrument): a darker body and a lighter glossy face, one pair of materials per instrument. The tint follows the
    /// instrument when Change swaps it. The words on the tiles stay, so colour is never the only cue, and white letters
    /// keep at least 4.5:1 contrast on every face (EditMode test). High contrast (<see cref="ViewSettings"/>) puts its own
    /// near-black material on the same renderers; this leaves any material that isn't one of its tints alone.
    /// </summary>
    public sealed class InstrumentTint : MonoBehaviour
    {
        [SerializeField] InstrumentTarget target;
        [SerializeField] Renderer[] bodies;
        [SerializeField] Renderer[] faces;
        [Tooltip("Indexed by InstrumentType.")]
        [SerializeField] Material[] bodyTints;
        [SerializeField] Material[] faceTints;

        InstrumentType m_Applied = (InstrumentType)(-1);

        public InstrumentTarget Target => target;
        public Renderer[] Bodies => bodies;
        public Renderer[] Faces => faces;

        public void Configure(InstrumentTarget owner, Renderer[] bodyParts, Renderer[] faceParts, Material[] bodyByInstrument, Material[] faceByInstrument)
        {
            target = owner;
            bodies = bodyParts;
            faces = faceParts;
            bodyTints = bodyByInstrument;
            faceTints = faceByInstrument;
        }

        public Material BodyFor(InstrumentType type) => Pick(bodyTints, type);
        public Material FaceFor(InstrumentType type) => Pick(faceTints, type);

        static Material Pick(Material[] set, InstrumentType type) =>
            set != null && (int)type >= 0 && (int)type < set.Length ? set[(int)type] : null;

        void LateUpdate()
        {
            if (target == null) return;
            var type = target.Instrument;
            bool changed = type != m_Applied;
            m_Applied = type;
            Wear(bodies, bodyTints, BodyFor(type), changed);
            Wear(faces, faceTints, FaceFor(type), changed);
        }

        /// <summary>Puts <paramref name="tint"/> on every part wearing one of this set's tints (high contrast's material
        /// is left alone); a part that came back from high contrast with an old tint is corrected too.</summary>
        static void Wear(Renderer[] parts, Material[] set, Material tint, bool changed)
        {
            if (parts == null || tint == null) return;
            foreach (var r in parts)
            {
                if (r == null) continue;
                var now = r.sharedMaterial;
                if (now == tint) continue;
                if (System.Array.IndexOf(set, now) >= 0) r.sharedMaterial = tint;
            }
        }
    }
}
