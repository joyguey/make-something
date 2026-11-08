using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The leaf pad: carries the ball tray, the buttons and the ball stand after a player who walks
    /// around the room (rules in <see cref="TrayFollow"/>), and comes at once when called.
    /// Glides smoothly, never at startup (PICO Spatial keeps poses moved before its renderer
    /// connects). Thrown balls stay where they are; the ready ball and the spares go with the stand.
    /// </summary>
    public class RoamingTray : MonoBehaviour
    {
        public const float FollowSeconds = 0.6f, CallSeconds = 0.25f, MaxSpeed = 1.5f;

        [Tooltip("The stage frame the seat (origin, facing +Z) is authored in.")]
        [SerializeField] Transform frame;
        [Tooltip("Things outside the pad that travel with it (ball stand, poof park).")]
        [SerializeField] Transform[] carried;
        [SerializeField] Vector3[] carriedHomePositions;
        [SerializeField] Quaternion[] carriedHomeRotations;
        [Tooltip("Stays where it is in the room while its parent moves (the pool of thrown balls).")]
        [SerializeField] Transform keepStill;
        [SerializeField] BallLauncher launcher;

        readonly TrayFollow m_Follow = new();
        Vector3 m_Position, m_Velocity; // pad origin on the frame's floor plan
        float m_Yaw, m_YawVelocity;

        public static RoamingTray Instance { get; private set; }
        public TrayMode Mode => m_Follow.Mode;
        // Within 1 cm and half a degree it snaps the rest of the way (a smooth glide never quite arrives).
        public bool AtTarget => (m_Position - TargetPosition).sqrMagnitude < 1e-4f && Mathf.Abs(Mathf.DeltaAngle(m_Yaw, m_Follow.Target.YawDegrees)) < 0.5f;
        Vector3 TargetPosition => new Vector3(m_Follow.Target.X, 0f, m_Follow.Target.Z);

        public void Configure(Transform stageFrame, Transform[] carry, Transform still, BallLauncher balls)
        {
            frame = stageFrame;
            carried = carry;
            carriedHomePositions = new Vector3[carry.Length];
            carriedHomeRotations = new Quaternion[carry.Length];
            for (int i = 0; i < carry.Length; i++)
            {
                carriedHomePositions[i] = frame.InverseTransformPoint(carry[i].position);
                carriedHomeRotations[i] = Quaternion.Inverse(frame.rotation) * carry[i].rotation;
            }
            keepStill = still;
            launcher = balls;
        }

        void OnEnable()
        {
            Instance = this;
            StageAnchor.Recentered += GoHomeNow;
            StageAnchor.Recentered += OnRecentered;
            ComfortSettings.PostureChanged += OnPosture;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            StageAnchor.Recentered -= GoHomeNow;
            StageAnchor.Recentered -= OnRecentered;
            ComfortSettings.PostureChanged -= OnPosture;
        }

        void Update()
        {
            if (Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds) return;
            if (TryGetHead(out var head) && m_Follow.Update(head.X, head.Z, head.YawDegrees, Time.deltaTime))
                Log();
            Glide(Time.deltaTime);
            UpdateDesk(Time.deltaTime);
        }

        // ---- Above the player's desk ----
        // Owner, headset 2026-11-08: "Because usually people have a desk in front of them, I also can not access the
        // leaf with the buttons. The floating ball on cloud is also further away from me." The leaf was built 49 cm
        // above the floor, under a desk. Once the room scan shows a table-top in front of the seat, the leaf, the ball
        // stand and what travels with them rise to sit just above it and come in toward the player, onto the desk's
        // front. Never during the opening (it poses the stand itself). Only rises (a scan that briefly misses the desk
        // never drops the leaf back under it).

        /// <summary>Where the leaf looks for a desk (pad frame, m): across, and how far out.</summary>
        public const float DeskMinX = -0.75f, DeskMaxX = 0.9f, DeskNearZ = 0.3f, DeskFarZ = 1.45f, DeskStep = 0.1f;
        /// <summary>A desk-top is this high above the floor (m), and covers at least this share of the samples.</summary>
        public const float DeskLow = 0.3f, DeskHigh = 1.2f, DeskCover = 0.2f;
        /// <summary>The leaf's underside at home (m above the floor), and how far above a desk it then floats.</summary>
        public const float LeafUnderside = 0.475f, AboveDesk = 0.05f;
        /// <summary>The leaf's middle comes no closer than this (m), and this far in from the desk's front edge.</summary>
        public const float NearestLeaf = 0.68f, OnDeskFront = 0.25f, LeafHomeZ = 1.17f;

        Vector3 m_Desk, m_DeskTarget, m_DeskVelocity; // (0, lift, pull) in the pad's frame
        float m_NextDeskLook;

        /// <summary>How far the leaf has risen (m) and come toward the player (m) to clear a desk.</summary>
        public Vector2 DeskOffset => new(m_Desk.y, m_Desk.z);
        public Vector2 DeskTarget => new(m_DeskTarget.y, m_DeskTarget.z);

        // ---- Within the player's reach (owner, headset 2026-11-08: "I CAN'T REACH ANYTHING, PLEASE REDO THE HEIGHT") ----
        // The leaf was laid out from the floor for a seated eye 1.15 m up, 1.17 m out. On the headset everything is now
        // placed from the player's own head once the opening is over (and again after Recenter or Height): the leaf's
        // middle a forearm's length in front of them and a little left, its top 40 cm below their eyes; the ball on its
        // cloud just right of it, 30 cm below the eyes. Over a desk both stay just above the desk's own top (the most
        // common flat height there: the monitor and things on the desk no longer count as the desk).

        /// <summary>The leaf's middle in front of the eyes (m), and its top below them (m).</summary>
        public const float ReachOut = 0.45f, LeafBelowEyes = 0.40f, LeafAside = -0.1f;
        /// <summary>The ball on the cloud below the eyes (m), at least this far above a desk (m), and the cloud's place from the leaf (m).</summary>
        public const float BallBelowEyes = 0.30f, BallAboveDesk = 0.25f, StandInward = 0.2f;
        /// <summary>Authored (home) heights: the leaf's top and underside, and the ball on the cloud (m above the floor).</summary>
        public const float LeafHomeTop = 0.505f, LeafHomeX = -0.2f, BallHome = 1.06f;

        Vector3 m_Stand, m_StandTarget, m_StandVelocity; // the cloud's own offset (x, up, toward the player)
        bool m_Fitted;

        void OnRecentered() => m_Fitted = false; // measure the player again
        void OnPosture(PlayPosture posture) => m_Fitted = false;

        // Owner, 2026-11-08: "adapt to the height with each playthrough ... and follow them at a comfortable height as
        // they start to move around". Fitted after every opening, after Recenter and Height, and again whenever the
        // player stands up, sits down or walks off (a change held for a moment, so leaning never moves it).
        /// <summary>A head this much higher or lower (m), or this far away across the floor (m), held this long (s), refits.</summary>
        public const float RefitRise = 0.15f, RefitWalk = 0.6f, RefitHold = 1.5f;
        /// <summary>The ball on its cloud: this far right of the eyes and out in front (m).</summary>
        public const float BallAside = 0.3f, BallOut = 0.4f;
        Vector3 m_FittedHead;
        float m_ChangedSince = -1f;

        void UpdateDesk(float deltaSeconds)
        {
            var intro = IntroSequence.Instance;
            if (intro != null && !intro.Done) return;
            bool device = XrRig.Instance != null && XrRig.Instance.Tracked;
            if (device && Time.time >= m_NextDeskLook)
            {
                m_NextDeskLook = Time.time + 0.25f;
                if (TryGetHeadInFrame(out var head))
                {
                    var flat = head - m_FittedHead;
                    flat.y = 0f;
                    bool changed = Mathf.Abs(head.y - m_FittedHead.y) > RefitRise || flat.magnitude > RefitWalk;
                    if (!changed) m_ChangedSince = -1f;
                    else if (m_ChangedSince < 0f) m_ChangedSince = Time.time;
                    if (!m_Fitted || (changed && Time.time - m_ChangedSince >= RefitHold))
                    {
                        Fit(head, true);
                        m_ChangedSince = -1f;
                    }
                }
            }
            bool moving = (m_Desk - m_DeskTarget).sqrMagnitude > 1e-6f || (m_Stand - m_StandTarget).sqrMagnitude > 1e-6f;
            if (!moving) return;
            m_Desk = Vector3.SmoothDamp(m_Desk, m_DeskTarget, ref m_DeskVelocity, 0.4f, 1.5f, deltaSeconds);
            m_Stand = Vector3.SmoothDamp(m_Stand, m_StandTarget, ref m_StandVelocity, 0.4f, 1.5f, deltaSeconds);
            if ((m_Desk - m_DeskTarget).sqrMagnitude < 1e-6f) { m_Desk = m_DeskTarget; m_DeskVelocity = Vector3.zero; }
            if ((m_Stand - m_StandTarget).sqrMagnitude < 1e-6f) { m_Stand = m_StandTarget; m_StandVelocity = Vector3.zero; }
            Apply();
        }

        // Places the leaf and the ball on its cloud from the head (frame space), facing the way the pad faces.
        void Fit(Vector3 head, bool log)
        {
            m_FittedHead = head;
            float rig = PostureLayout.ControlsLift(ComfortSettings.Posture); // the Controls holder lifts the leaf already
            var turn = Quaternion.Euler(0f, m_Follow.Target.YawDegrees, 0f);
            var origin = TargetPosition;
            var leafMiddle = head + turn * new Vector3(LeafAside, 0f, ReachOut);
            var ballAt = head + turn * new Vector3(BallAside, 0f, BallOut);
            float leafTop = head.y - LeafBelowEyes;
            float ball = head.y - BallBelowEyes;
            bool desk = TryFindDeskUnder(leafMiddle, head.y, out float deskTop);
            if (desk)
            {
                leafTop = Mathf.Max(leafTop, deskTop + AboveDesk + (LeafHomeTop - LeafUnderside));
                ball = Mathf.Max(ball, deskTop + BallAboveDesk);
            }
            // the leaf: offsets from its authored place, in the pad's frame (across, up, toward the player)
            var leafLocal = Quaternion.Inverse(turn) * (leafMiddle - origin);
            var leaf = new Vector3(leafLocal.x - LeafHomeX, leafTop - LeafHomeTop - rig, LeafHomeZ - leafLocal.z);
            // the cloud: from where the ball sits now, back to its authored place, then on to where it should be
            var stand = m_StandTarget;
            if (launcher != null && launcher.SpawnPosition.HasValue)
            {
                var nowTurn = Quaternion.Euler(0f, m_Yaw, 0f);
                var spawn = frame.InverseTransformPoint(launcher.SpawnPosition.Value);
                var home = Quaternion.Inverse(nowTurn) * (spawn - m_Position) - new Vector3(m_Stand.x, m_Stand.y, -m_Stand.z);
                var want = Quaternion.Inverse(turn) * (new Vector3(ballAt.x, ball, ballAt.z) - origin);
                var offset = want - home;
                stand = new Vector3(offset.x, offset.y, -offset.z);
            }
            m_DeskTarget = leaf;
            m_StandTarget = stand;
            if (log)
                Debug.Log($"[RoomOrchestra] Leaf pad: fitted to the player (eyes {head.y:F2} m up{(desk ? $", desk top {deskTop:F2} m" : ", no desk under the leaf")}): leaf top {leafTop:F2} m, {ReachOut:F2} m out; ball {ball:F2} m");
            m_Fitted = true;
        }

        float m_NextDeskLog;

        /// <summary>Test hook: fits to a head at <paramref name="head"/> (stage space) at once.</summary>
        public void FitForTest(Vector3 head)
        {
            m_Fitted = false;
            Fit(head, true);
            m_Desk = m_DeskTarget;
            m_Stand = m_StandTarget;
            Apply();
        }

        bool TryGetHeadInFrame(out Vector3 head)
        {
            head = default;
            if (frame == null || StageAnchor.Instance == null || !StageAnchor.Instance.TryGetHeadPose(out var pose)) return false;
            head = frame.InverseTransformPoint(pose.position);
            return head.y > 0.5f;
        }

        // A desk under the leaf: flat scanned surfaces below the eyes over the leaf's footprint; the desk's top is the
        // most common height among them (things standing on the desk are fewer and higher).
        bool TryFindDeskUnder(Vector3 middle, float eye, out float top)
        {
            top = 0f;
            if (frame == null || RealWalls.Instance == null || RealWalls.Instance.SurfaceCount == 0) return false;
            var bins = new System.Collections.Generic.Dictionary<int, int>();
            int samples = 0, hits = 0;
            float floor = frame.position.y;
            const float bin = 0.04f;
            for (float x = -0.5f; x <= 0.5f + 1e-4f; x += DeskStep)
            for (float z = -0.25f; z <= 0.35f + 1e-4f; z += DeskStep)
            {
                samples++;
                var from = frame.TransformPoint(new Vector3(middle.x + x, eye - 0.1f, middle.z + z));
                if (!Physics.Raycast(from, Vector3.down, out var hit, eye, 1 << RealWalls.SurfaceLayer, QueryTriggerInteraction.Ignore)) continue;
                float y = hit.point.y - floor;
                if (y < DeskLow || y > eye - 0.15f || Mathf.Abs(hit.normal.y) < 0.7f) continue;
                int k = Mathf.RoundToInt(y / bin);
                bins[k] = bins.TryGetValue(k, out int n) ? n + 1 : 1;
                hits++;
            }
            if (hits < samples * DeskCover) return false;
            int best = 0, bestCount = -1;
            foreach (var pair in bins) if (pair.Value > bestCount) { best = pair.Key; bestCount = pair.Value; }
            top = best * bin;
            return true;
        }


        /// <summary>Option C: come to the player now (or home, if they are at the seat).</summary>
        public bool Call()
        {
            if (!TryGetHead(out var head) || !m_Follow.Call(head.X, head.Z, head.YawDegrees)) return false;
            Log();
            return true;
        }

        /// <summary>Test hook: one frame with the head at <paramref name="head"/> (frame floor plan).</summary>
        public void Step(StagePose head, float deltaSeconds)
        {
            if (m_Follow.Update(head.X, head.Z, head.YawDegrees, deltaSeconds)) Log();
            Glide(deltaSeconds);
        }

        /// <summary>Test hook: the call with the head at <paramref name="head"/>.</summary>
        public bool CallFrom(StagePose head)
        {
            if (!m_Follow.Call(head.X, head.Z, head.YawDegrees)) return false;
            Log();
            return true;
        }

        void Log() => Debug.Log($"[RoomOrchestra] Leaf pad: {(m_Follow.Called ? "called, " : "")}{(m_Follow.Mode == TrayMode.Home ? "home" : "following")} to ({m_Follow.Target.X:F2}, {m_Follow.Target.Z:F2}) facing {m_Follow.Target.YawDegrees:F0}°");

        bool TryGetHead(out StagePose head)
        {
            head = default;
            if (frame == null || StageAnchor.Instance == null || !StageAnchor.Instance.TryGetHeadPose(out var pose)) return false;
            var p = frame.InverseTransformPoint(pose.position);
            var forward = frame.InverseTransformDirection(pose.rotation * Vector3.forward);
            var up = frame.InverseTransformDirection(pose.rotation * Vector3.up);
            if (!StagePlacement.TryRecenter(p.x, p.z, forward.x, forward.y, forward.z, up.x, up.y, up.z, out head)) return false;
            return true;
        }

        void Glide(float deltaSeconds)
        {
            if (AtTarget && m_Velocity == Vector3.zero) return;
            float smooth = m_Follow.Called ? CallSeconds : FollowSeconds;
            float speed = m_Follow.Called ? MaxSpeed * 3f : MaxSpeed;
            m_Position = Vector3.SmoothDamp(m_Position, TargetPosition, ref m_Velocity, smooth, speed, deltaSeconds);
            m_Yaw = Mathf.SmoothDampAngle(m_Yaw, m_Follow.Target.YawDegrees, ref m_YawVelocity, smooth, 360f, deltaSeconds);
            if (AtTarget)
            {
                m_Position = TargetPosition;
                m_Yaw = m_Follow.Target.YawDegrees;
                m_Velocity = Vector3.zero;
                m_YawVelocity = 0f;
            }
            Apply();
        }

        void GoHomeNow()
        {
            m_Follow.Reset();
            m_Position = Vector3.zero;
            m_Velocity = Vector3.zero;
            m_Yaw = 0f;
            m_YawVelocity = 0f;
            Apply();
        }

        void Apply()
        {
            var turn = Quaternion.Euler(0f, m_Yaw, 0f);
            var desk = new Vector3(m_Desk.x, m_Desk.y, -m_Desk.z); // fitted to the player: across, up, in toward them (pad frame)
            var standOffset = new Vector3(m_Stand.x, m_Stand.y, -m_Stand.z);
            // The pad sits in the Controls holder, which only lifts it (Height); x/z/yaw are ours.
            transform.localPosition = new Vector3(m_Position.x, 0f, m_Position.z) + turn * desk;
            transform.localRotation = turn;

            Pose still = keepStill != null ? new Pose(keepStill.position, keepStill.rotation) : default;
            if (carried != null && frame != null)
                for (int i = 0; i < carried.Length; i++)
                {
                    if (carried[i] == null) continue;
                    carried[i].SetPositionAndRotation(
                        frame.TransformPoint(turn * (carriedHomePositions[i] + standOffset) + m_Position),
                        frame.rotation * turn * carriedHomeRotations[i]);
                }
            if (keepStill != null) keepStill.SetPositionAndRotation(still.position, still.rotation);
            Physics.SyncTransforms(); // pointer rays and balls meet the pad where it now is
            if (launcher != null) launcher.FollowStand();
        }
    }
}
