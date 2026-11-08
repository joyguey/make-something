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
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            StageAnchor.Recentered -= GoHomeNow;
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

        void UpdateDesk(float deltaSeconds)
        {
            var intro = IntroSequence.Instance;
            if (intro != null && !intro.Done) return;
            if (Time.time >= m_NextDeskLook)
            {
                m_NextDeskLook = Time.time + 1f;
                if (TryFindDesk(out float top, out float frontZ))
                {
                    float lift = Mathf.Max(0f, top + AboveDesk - LeafUnderside);
                    float leafZ = Mathf.Max(NearestLeaf, frontZ + OnDeskFront);
                    float pull = Mathf.Max(0f, LeafHomeZ - leafZ);
                    var target = new Vector3(0f, Mathf.Max(lift, m_DeskTarget.y), lift > 0f ? pull : m_DeskTarget.z);
                    if ((target - m_DeskTarget).sqrMagnitude > 0.03f * 0.03f)
                    {
                        m_DeskTarget = target;
                        Debug.Log($"[RoomOrchestra] Leaf pad: a desk {top:F2} m high in front (front edge {frontZ:F2} m out): the leaf rises {target.y:F2} m and comes {target.z:F2} m closer");
                    }
                }
            }
            if ((m_Desk - m_DeskTarget).sqrMagnitude < 1e-6f) return;
            m_Desk = Vector3.SmoothDamp(m_Desk, m_DeskTarget, ref m_DeskVelocity, 0.4f, 1.5f, deltaSeconds);
            if ((m_Desk - m_DeskTarget).sqrMagnitude < 1e-6f)
            {
                m_Desk = m_DeskTarget;
                m_DeskVelocity = Vector3.zero;
            }
            Apply();
        }

        /// <summary>Test hook: as if the scan had found a desk this high with its front edge this far out.</summary>
        public void SetDeskForTest(float top, float frontZ)
        {
            float lift = Mathf.Max(0f, top + AboveDesk - LeafUnderside);
            float leafZ = Mathf.Max(NearestLeaf, frontZ + OnDeskFront);
            m_DeskTarget = m_Desk = new Vector3(0f, lift, Mathf.Max(0f, LeafHomeZ - leafZ));
            Apply();
        }

        // Looks straight down onto the scanned room over the leaf's area in front of the seat.
        bool TryFindDesk(out float top, out float frontZ)
        {
            top = 0f;
            frontZ = 0f;
            if (frame == null || RealWalls.Instance == null || RealWalls.Instance.SurfaceCount == 0) return false;
            var turn = Quaternion.Euler(0f, m_Yaw, 0f);
            var heights = new System.Collections.Generic.List<(float y, float z)>();
            int samples = 0;
            float floor = frame.position.y;
            for (float x = DeskMinX; x <= DeskMaxX + 1e-4f; x += DeskStep)
            for (float z = DeskNearZ; z <= DeskFarZ + 1e-4f; z += DeskStep)
            {
                samples++;
                var local = turn * new Vector3(x, 0f, z) + m_Position;
                var from = frame.TransformPoint(new Vector3(local.x, DeskHigh + 0.3f, local.z));
                if (!Physics.Raycast(from, Vector3.down, out var hit, DeskHigh + 0.3f, 1 << RealWalls.SurfaceLayer, QueryTriggerInteraction.Ignore)) continue;
                float y = hit.point.y - floor;
                if (y < DeskLow || y > DeskHigh || Mathf.Abs(hit.normal.y) < 0.7f) continue;
                heights.Add((y, z));
            }
            if (samples == 0 || heights.Count < samples * DeskCover) return false;
            heights.Sort((a, b) => a.y.CompareTo(b.y));
            top = heights[Mathf.Clamp(Mathf.FloorToInt(heights.Count * 0.8f), 0, heights.Count - 1)].y;
            frontZ = float.MaxValue;
            foreach (var (y, z) in heights) if (Mathf.Abs(y - top) < 0.08f) frontZ = Mathf.Min(frontZ, z);
            return frontZ < float.MaxValue;
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
            var desk = new Vector3(0f, m_Desk.y, -m_Desk.z); // up off the desk and in toward the seat, in the pad's frame
            // The pad sits in the Controls holder, which only lifts it (Height); x/z/yaw are ours.
            transform.localPosition = new Vector3(m_Position.x, 0f, m_Position.z) + turn * desk;
            transform.localRotation = turn;

            Pose still = keepStill != null ? new Pose(keepStill.position, keepStill.rotation) : default;
            if (carried != null && frame != null)
                for (int i = 0; i < carried.Length; i++)
                {
                    if (carried[i] == null) continue;
                    carried[i].SetPositionAndRotation(
                        frame.TransformPoint(turn * (carriedHomePositions[i] + desk) + m_Position),
                        frame.rotation * turn * carriedHomeRotations[i]);
                }
            if (keepStill != null) keepStill.SetPositionAndRotation(still.position, still.rotation);
            Physics.SyncTransforms(); // pointer rays and balls meet the pad where it now is
            if (launcher != null) launcher.FollowStand();
        }
    }
}
