using System;

namespace RoomOrchestra
{
    public enum TrayMode
    {
        /// <summary>At the seat, where it was authored.</summary>
        Home,
        /// <summary>Drifting after a player who walked away from the seat.</summary>
        Following
    }

    /// <summary>
    /// Where the leaf pad (ball tray, buttons and ball stand) should be, on the stage's floor plan.
    /// The pad's layout was authored for a player at the stage origin facing +Z, so following means
    /// putting that origin under the player's head, turned the way they face: the controls then sit
    /// exactly where they do for a seated player.
    /// A (lazy follow): only once the player walks away from the seat, and then only after they have
    /// moved or turned well past a dead zone, so it never jitters after every head movement.
    /// C (call): one tap brings it to the player at once, or home if they are at the seat.
    /// Back at the seat for a moment, it returns home.
    /// </summary>
    public sealed class TrayFollow
    {
        public const float LeaveRadius = 0.9f;
        public const float ReturnRadius = 0.5f;
        public const float ReturnDwellSeconds = 1.5f;
        public const float MoveDeadzone = 0.5f;
        public const float TurnDeadzoneDegrees = 45f;

        float m_AtSeatSeconds;

        public TrayMode Mode { get; private set; } = TrayMode.Home;
        /// <summary>Where the pad's origin should be (stage floor plan) and which way it faces.</summary>
        public StagePose Target { get; private set; } = new StagePose(0f, 0f, 0f);
        /// <summary>True when the last move was a call: the pad comes quickly instead of drifting.</summary>
        public bool Called { get; private set; }

        /// <summary>One frame of head movement; true if the target changed.</summary>
        public bool Update(float headX, float headZ, float headYawDegrees, float deltaSeconds)
        {
            float fromSeat = Length(headX, headZ);
            if (Mode == TrayMode.Home)
            {
                if (fromSeat <= LeaveRadius) return false;
                Mode = TrayMode.Following;
                return MoveTo(headX, headZ, headYawDegrees, called: false);
            }

            if (fromSeat < ReturnRadius)
            {
                m_AtSeatSeconds += deltaSeconds;
                if (m_AtSeatSeconds >= ReturnDwellSeconds)
                {
                    GoHome(called: false);
                    return true;
                }
            }
            else
            {
                m_AtSeatSeconds = 0f;
            }

            bool moved = Length(headX - Target.X, headZ - Target.Z) > MoveDeadzone;
            bool turned = Math.Abs(DeltaAngle(headYawDegrees, Target.YawDegrees)) > TurnDeadzoneDegrees;
            return (moved || turned) && MoveTo(headX, headZ, headYawDegrees, called: false);
        }

        /// <summary>The call: to the player now, or home if they are at the seat. True if it moves.</summary>
        public bool Call(float headX, float headZ, float headYawDegrees)
        {
            if (Length(headX, headZ) < ReturnRadius)
            {
                if (Mode == TrayMode.Home) return false;
                GoHome(called: true);
                return true;
            }
            Mode = TrayMode.Following;
            return MoveTo(headX, headZ, headYawDegrees, called: true);
        }

        /// <summary>Recenter moved the seat under the player: the pad is home again.</summary>
        public void Reset()
        {
            Mode = TrayMode.Home;
            Target = new StagePose(0f, 0f, 0f);
            Called = false;
            m_AtSeatSeconds = 0f;
        }

        void GoHome(bool called)
        {
            Reset();
            Called = called;
        }

        bool MoveTo(float x, float z, float yaw, bool called)
        {
            Target = new StagePose(x, z, yaw);
            Called = called;
            m_AtSeatSeconds = 0f;
            return true;
        }

        static float Length(float x, float z) => (float)Math.Sqrt(x * x + z * z);

        /// <summary>Shortest signed difference a - b in degrees (-180..180].</summary>
        public static float DeltaAngle(float a, float b)
        {
            float d = (a - b) % 360f;
            if (d > 180f) d -= 360f;
            if (d <= -180f) d += 360f;
            return d;
        }
    }
}
