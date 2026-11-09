using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// A little flower somewhere in the room; one more pops up with each note the song adds. It grows out of
    /// the ground as a closed green bud on its stem, then the petals bloom open around the heart while the bud
    /// tucks away inside it (owner, 2026-10-29).
    /// </summary>
    public class FloorFlower : FloorSprout
    {
        public const float BloomSeconds = 0.9f;
        /// <summary>Petals while still in the bud: tucked inside it, too small to see.</summary>
        public const float ClosedScale = 0.15f;

        [Tooltip("Petals, petal rims and heart, scaled about the heart's centre.")]
        [SerializeField] Transform bloom;
        [Tooltip("The closed green bud, scaled about the same centre.")]
        [SerializeField] Transform bud;

        public Transform Bloom => bloom;
        public Transform Bud => bud;
        /// <summary>0 = a closed bud, 1 = in full bloom.</summary>
        public float Openness { get; private set; }

        public void ConfigureBloom(Transform petals, Transform closedBud)
        {
            bloom = petals;
            bud = closedBud;
        }

        protected override void OnGrowing(float secondsSinceGrown, bool reducedMotion)
        {
            float t = reducedMotion ? 1f : Mathf.Clamp01(secondsSinceGrown / BloomSeconds);
            // The petals spring open (a little past full, then settle); the bud shrinks into the heart.
            float open = t <= 0f ? 0f : t >= 1f ? 1f : t * t * (3f - 2f * t) + Mathf.Sin(t * Mathf.PI) * 0.2f;
            SetOpenness(open);
        }

        protected override void OnUnsprout() => SetOpenness(0f);

        void SetOpenness(float open)
        {
            Openness = Mathf.Clamp01(open);
            if (bloom != null)
            {
                var petals = Vector3.one * Mathf.LerpUnclamped(ClosedScale, 1f, open);
                if (bloom.localScale != petals) bloom.localScale = petals;
            }
            if (bud != null)
            {
                var closed = Vector3.one * Mathf.Lerp(1f, ClosedScale, Mathf.Clamp01(open));
                if (bud.localScale != closed) bud.localScale = closed;
            }
        }
    }
}
