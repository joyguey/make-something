using System;

namespace RoomOrchestra
{
    public enum MotionMode
    {
        Full,
        Calm
    }

    /// <summary>
    /// Player comfort choices, local to this session. Calm motion: no flashing furniture, echo minis
    /// hold still at their place on the ring and the play marker steps once per beat, so rhythm and
    /// beat stay readable without anything sweeping around. Full is the default, so nothing changes
    /// at startup.
    /// </summary>
    public static class ComfortSettings
    {
        public static MotionMode Motion { get; private set; } = MotionMode.Full;
        public static bool CalmMotion => Motion == MotionMode.Calm;

        public static event Action<MotionMode> Changed;

        public static void SetMotion(MotionMode mode)
        {
            if (Motion == mode) return;
            Motion = mode;
            Changed?.Invoke(mode);
        }
    }
}
