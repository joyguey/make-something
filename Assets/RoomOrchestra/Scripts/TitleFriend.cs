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

        [SerializeField] float delayBeats;
        [SerializeField] Vector3 restPosition, restScale = Vector3.one;

        public void Configure(float startDelayBeats)
        {
            delayBeats = startDelayBeats;
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
            // up and down in the first 70% of the beat, then a squash on landing and a bounce back
            float hop = u < 0.7f ? Mathf.Sin(u / 0.7f * Mathf.PI) : 0f;
            float land = u >= 0.7f ? Mathf.Sin((u - 0.7f) / 0.3f * Mathf.PI) : 0f;
            transform.localPosition = restPosition + Vector3.up * (HopHeight * hop);
            float squash = Squash * land;
            transform.localScale = new Vector3(restScale.x * (1f + squash * 0.6f), restScale.y * (1f - squash), restScale.z * (1f + squash * 0.6f));
        }
    }
}
