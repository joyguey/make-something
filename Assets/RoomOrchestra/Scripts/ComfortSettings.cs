using System;
using UnityEngine;

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
    /// beat stay readable without anything sweeping around. Posture: Standing lifts the controls.
    /// Full and Seated are the defaults, so nothing changes at startup.
    /// </summary>
    public static class ComfortSettings
    {
        public static MotionMode Motion { get; private set; } = MotionMode.Full;
        public static bool CalmMotion => Motion == MotionMode.Calm;
        public static PlayPosture Posture { get; private set; } = PlayPosture.Seated;

        public static event Action<MotionMode> Changed;
        public static event Action<PlayPosture> PostureChanged;

        public static void SetMotion(MotionMode mode)
        {
            if (Motion == mode) return;
            Motion = mode;
            Changed?.Invoke(mode);
        }

        public static void SetPosture(PlayPosture posture)
        {
            if (Posture == posture) return;
            Posture = posture;
            PostureChanged?.Invoke(posture);
        }

        // Fresh defaults each play session, even with domain reload off (matches the authored scene).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetToDefaults()
        {
            Motion = MotionMode.Full;
            Posture = PlayPosture.Seated;
        }
    }
}
