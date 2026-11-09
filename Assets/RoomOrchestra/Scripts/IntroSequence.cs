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
        /// <summary>Until Start the leaf waits this far above where the swoop begins: out of sight, not floating in
        /// the room (owner, headset 2026-11-08: the leaf hung in mid-air up to the right during the title).</summary>
        public const float HiddenLift = 80f;

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
        [Tooltip("The guided song that follows; skipping the opening skips it too.")]
        [SerializeField] TutorialDirector tutorial;
        [Tooltip("The ball stand (and what travels with it): directly in front at launch (Version 1 feedback: the podium " +
                 "with the ball is the first thing the user sees), gliding to its place beside the leaf during the swoop.")]
        [SerializeField] Transform[] stand;
        [SerializeField] Vector3[] standHomes;
        [SerializeField] Vector3 standFrontOffset;
        [SerializeField] BallLauncher launcher;

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
            HideLeaf(); // saved out of sight: PICO Spatial keeps the saved pose until the renderer connects
            for (int i = 0; i < cast.Length; i++) PlaceOnFloor(i);
        }

        void HideLeaf() => leaf.localPosition += Vector3.up * HiddenLift;

        /// <summary>The leaf is out of sight, waiting for Start.</summary>
        public bool LeafHidden => leaf != null && leaf.localPosition.y > HiddenLift * 0.5f;

        public void ConfigureTutorial(TutorialDirector guide) => tutorial = guide;

        /// <summary>Puts the ball stand <paramref name="frontOffset"/> from its home, in front of the player, for the
        /// first frame (scene setup); the opening glides it home.</summary>
        // Homes and offset are kept in the stage's own space (owner, headset 2026-11-08: the cloud and ball were "very
        // off"): the headset recentres the stage on the player a second in, and homes kept in the room's space put the
        // cloud back where the stage used to be.
        [SerializeField] Transform standFrame;

        public void ConfigureStand(Transform[] carried, Vector3 frontOffset, BallLauncher balls, Transform frame = null)
        {
            stand = carried;
            launcher = balls;
            standFrame = frame;
            standFrontOffset = frame != null ? frame.InverseTransformVector(frontOffset) : frontOffset;
            standHomes = new Vector3[carried.Length];
            for (int i = 0; i < carried.Length; i++)
            {
                standHomes[i] = frame != null ? frame.InverseTransformPoint(carried[i].position) : carried[i].position;
                carried[i].position += frontOffset;
            }
        }

        Vector3 StandHome(int i, float away) =>
            standFrame != null ? standFrame.TransformPoint(standHomes[i] + standFrontOffset * away) : standHomes[i] + standFrontOffset * away;

        /// <summary>How far the stand still is from home (0 once it has glided into place).</summary>
        public float StandAway => stand == null || stand.Length == 0 ? 0f : Vector3.Distance(stand[0].position, StandHome(0, 0f));

        void PoseStand(float u)
        {
            if (stand == null) return;
            float e = u * u * (3f - 2f * u);
            for (int i = 0; i < stand.Length; i++)
                if (stand[i] != null) stand[i].position = StandHome(i, 1f - e);
            if (launcher != null && Application.isPlaying) launcher.FollowStand(); // the ready ball rides along on the cloud
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
            if (allLanded) Land();
        }

        /// <summary>Starts the opening (the title's Start calls this; without a title it starts by itself).</summary>
        public void Play()
        {
            if (m_Done || m_Start >= 0f) return;
            m_Start = Time.time;
            m_Bounce = new int[characters.Length];
            if (leaf != null)
            {
                Pose(0f); // appears behind the player's head, in a poof
                PoofCloud.PoofAt(leaf.position, 0.6f);
            }
            Debug.Log("[RoomOrchestra] Intro: the leaf appears behind you and floats down");
        }

        /// <summary>
        /// Everything in its place at once (a skip, and the state tests check the layout in); the guided song is
        /// skipped too unless <paramref name="skipTutorial"/> is false.
        /// </summary>
        public void Finish(bool skipTutorial = true)
        {
            Land();
            if (skipTutorial && tutorial != null) tutorial.SkipNow();
        }

        void Land()
        {
            if (stand != null && StandAway > 0f)
            {
                PoseStand(1f);
                PoofCloud.ParkIdle();
            }
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
            if (seconds > 0f) PoseStand(Mathf.Clamp01(seconds / IntroPath.SwoopSeconds));
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
