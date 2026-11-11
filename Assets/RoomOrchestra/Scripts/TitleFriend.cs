using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// One of the three friends hopping on the title sign (V3 review: "a little meadow with your bouncy happy
    /// characters"): a happy little hop on every beat of the title theme (88 BPM, like the game), each friend a
    /// little after the one before, squashing as it lands. Children of the title card, so they fly into the cloud
    /// with it on Start. Reduced motion keeps them still.
    /// </summary>
    public class TitleFriend : MonoBehaviour
    {
        public const float Bpm = 88f, HopHeight = 0.06f, Squash = 0.18f;
        /// <summary>The share of each beat spent in the air (owner, headset 2026-11-12: they looked like they floated):
        /// a quick hop, then sitting on the rail.</summary>
        public const float AirShare = 0.38f, LandShare = 0.22f;

        [SerializeField] float delayBeats;
        [SerializeField] Vector3 restPosition, restScale = Vector3.one;
        [Tooltip("From the friend's pivot down to its feet (m, at rest): squashing keeps the feet on the rail.")]
        [SerializeField] float footDrop;

        public void Configure(float startDelayBeats, float pivotAboveFeet = 0f)
        {
            delayBeats = startDelayBeats;
            footDrop = pivotAboveFeet;
            restPosition = transform.localPosition;
            restScale = transform.localScale;
        }

        void Update()
        {
            if (VineGrowth.ReducedMotion)
            {
                transform.localPosition = restPosition;
                transform.localScale = restScale;
                return;
            }
            float beats = Time.time * Bpm / 60f - delayBeats;
            float u = beats - Mathf.Floor(beats); // 0..1 through this beat
            // a quick hop, a squash as it lands, then sitting on the rail for the rest of the beat
            float hop = u < AirShare ? Mathf.Sin(u / AirShare * Mathf.PI) : 0f;
            float land = u >= AirShare && u < AirShare + LandShare ? Mathf.Sin((u - AirShare) / LandShare * Mathf.PI) : 0f;
            float squash = Squash * land;
            // squashing about the feet, not the middle: the feet stay on the rail
            float sink = footDrop * squash;
            transform.localPosition = restPosition + Vector3.up * (HopHeight * hop - sink);
            transform.localScale = new Vector3(restScale.x * (1f + squash * 0.6f), restScale.y * (1f - squash), restScale.z * (1f + squash * 0.6f));
        }
    }
}
