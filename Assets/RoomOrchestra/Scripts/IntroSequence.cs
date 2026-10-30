using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The opening (owner, 2026-10-29): the leaf palette glides in on a swoop, then Flutter, Bop and Boom
    /// bounce up from the floor and land on it in front of the player (motion in <see cref="IntroPath"/>).
    /// PICO Spatial keeps the poses of anything moved before its renderer connects, so the scene is saved
    /// in the opening's first frame (leaf out of view, characters on the floor) and the animation starts
    /// after <see cref="JellyBody.StartDelaySeconds"/>. <see cref="Finish"/> jumps to the end.
    /// </summary>
    public class IntroSequence : MonoBehaviour
    {
        /// <summary>A beat after the renderer connects, so the swoop is seen from its start.</summary>
        public const float StartAfterSeconds = 0.4f;

        [Tooltip("Holds the leaf and everything on it; its local pose is the swoop's offset from home.")]
        [SerializeField] Transform leaf;
        [SerializeField] Transform[] characters;
        [Tooltip("Each character's place on the leaf, in its parent's space.")]
        [SerializeField] Vector3[] homes;
        [Tooltip("Each character's spot on the floor, in the leaf pad's space (it waits there during the swoop).")]
        [SerializeField] Vector3[] floorSpots;
        [Tooltip("The hole that opens and the stalk that grows up to meet the leaf.")]
        [SerializeField] LeafStalk stalk;
        [Tooltip("The title card; with one, the opening waits for Start instead of beginning by itself.")]
        [SerializeField] TitleScreen title;

        /// <summary>The hole opens over this long; then the stalk grows, reaching the leaf just before it lands,
        /// and the hole and its cracks close up around it, leaving the stalk on the floor.</summary>
        public const float HoleOpenSeconds = 0.4f, StalkDoneBefore = 0.2f, HoleCloseSeconds = 0.5f;

        float m_Start = -1f;
        bool m_Done;
        int[] m_Bounce; // the bounce each character is on, for a squash at each take-off

        public static IntroSequence Instance { get; private set; }
        public bool Playing => m_Start >= 0f && !m_Done;
        public bool Done => m_Done;
        public Transform Leaf => leaf;

        /// <summary>Wires the opening and puts the scene in its first frame (called by the scene setup).</summary>
        public void Configure(Transform leafHolder, Transform[] cast, Vector3[] floorWorld, LeafStalk leafStalk = null, TitleScreen titleCard = null)
        {
            stalk = leafStalk;
            title = titleCard;
            leaf = leafHolder;
            characters = cast;
            homes = new Vector3[cast.Length];
            floorSpots = new Vector3[cast.Length];
            var pad = leaf.parent;
            for (int i = 0; i < cast.Length; i++)
            {
                homes[i] = cast[i].localPosition;
                floorSpots[i] = pad.InverseTransformPoint(floorWorld[i]);
            }
            Pose(0f);
            for (int i = 0; i < cast.Length; i++) PlaceOnFloor(i);
        }

        void OnEnable() => Instance = this;

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (m_Done || leaf == null) return;
            if (m_Start < 0f)
            {
                if (title != null) return; // the title's Start begins it (Play)
                if (Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds + StartAfterSeconds) return;
                Play();
            }
            float t = Time.time - m_Start;
            Pose(t);
            bool allLanded = t >= IntroPath.SwoopSeconds;
            for (int i = 0; i < characters.Length; i++)
            {
                float hop = t - IntroPath.SwoopSeconds - i * IntroPath.HopStagger;
                if (!Hop(i, hop)) allLanded = false;
            }
            if (allLanded) Finish();
        }

        /// <summary>Starts the opening (the title's Start calls this; without a title it starts by itself).</summary>
        public void Play()
        {
            if (m_Done || m_Start >= 0f) return;
            m_Start = Time.time;
            m_Bounce = new int[characters.Length];
            Debug.Log("[RoomOrchestra] Intro: the leaf swoops in");
        }

        /// <summary>Everything in its place at once (a skip, and the state tests check the layout in).</summary>
        public void Finish()
        {
            if (title != null && !title.Parked) title.ParkNow();
            if (leaf != null)
            {
                leaf.localPosition = Vector3.zero;
                leaf.localRotation = Quaternion.identity;
            }
            if (stalk != null)
            {
                stalk.HoleOpen = 0f; // closed again around the grown stalk
                stalk.Grown = 1f;
            }
            if (characters != null)
                for (int i = 0; i < characters.Length; i++)
                    if (characters[i] != null) characters[i].localPosition = homes[i];
            if (!m_Done && m_Start >= 0f) Debug.Log("[RoomOrchestra] Intro: everyone has landed on the leaf");
            m_Done = true;
        }

        void Pose(float seconds)
        {
            var p = IntroPath.LeafAt(seconds);
            leaf.localPosition = new Vector3(p.x, p.y, p.z);
            leaf.localRotation = Quaternion.Euler(p.pitch, p.yaw, p.roll);
            if (stalk != null)
            {
                float grownAt = IntroPath.SwoopSeconds - StalkDoneBefore;
                float opening = Mathf.Clamp01(seconds / HoleOpenSeconds);
                float closing = Mathf.Clamp01((seconds - grownAt) / HoleCloseSeconds);
                stalk.HoleOpen = opening * (1f - closing * closing * (3f - 2f * closing));
                float grow = Mathf.Clamp01((seconds - HoleOpenSeconds) / (grownAt - HoleOpenSeconds));
                stalk.Grown = grow * grow * (3f - 2f * grow);
            }
        }

        void PlaceOnFloor(int i) => characters[i].position = leaf.parent.TransformPoint(floorSpots[i]);

        // One character's bounces; true once it has landed on the leaf.
        bool Hop(int i, float seconds)
        {
            var c = characters[i];
            if (c == null) return true;
            if (seconds <= 0f)
            {
                PlaceOnFloor(i); // waits on the floor while the leaf moves over it
                return false;
            }
            var (blend, lift, landed) = IntroPath.HopAt(seconds);
            if (landed)
            {
                if (c.localPosition != homes[i])
                {
                    c.localPosition = homes[i];
                    if (c.TryGetComponent<JellyBody>(out var jelly)) jelly.Kick(0.6f); // a squashy landing
                }
                return true;
            }
            int bounce = IntroPath.BounceIndex(seconds) + 1; // 0 = not yet off the floor
            if (m_Bounce != null && bounce != m_Bounce[i])
            {
                m_Bounce[i] = bounce;
                if (c.TryGetComponent<JellyBody>(out var jelly)) jelly.Kick(0.35f); // a little squash at each take-off
            }
            var floor = leaf.parent.TransformPoint(floorSpots[i]);
            var home = c.parent.TransformPoint(homes[i]);
            c.position = Vector3.Lerp(floor, home, blend) + Vector3.up * lift;
            return false;
        }
    }
}
