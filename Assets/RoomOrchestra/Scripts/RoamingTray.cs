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
            // The pad sits in the Controls holder, which only lifts it (Height); x/z/yaw are ours.
            transform.localPosition = new Vector3(m_Position.x, 0f, m_Position.z);
            transform.localRotation = turn;

            Pose still = keepStill != null ? new Pose(keepStill.position, keepStill.rotation) : default;
            if (carried != null && frame != null)
                for (int i = 0; i < carried.Length; i++)
                {
                    if (carried[i] == null) continue;
                    carried[i].SetPositionAndRotation(
                        frame.TransformPoint(turn * carriedHomePositions[i] + m_Position),
                        frame.rotation * turn * carriedHomeRotations[i]);
                }
            if (keepStill != null) keepStill.SetPositionAndRotation(still.position, still.rotation);
            Physics.SyncTransforms(); // pointer rays and balls meet the pad where it now is
            if (launcher != null) launcher.FollowStand();
        }
    }
}
