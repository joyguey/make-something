using System;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Player comfort choices, local to this session. Posture: Standing lifts the controls.
    /// Seated is the default, so nothing changes at startup.
    /// </summary>
    public static class ComfortSettings
    {
        public static PlayPosture Posture { get; private set; } = PlayPosture.Seated;

        public static event Action<PlayPosture> PostureChanged;

        public static void SetPosture(PlayPosture posture)
        {
            if (Posture == posture) return;
            Posture = posture;
            PostureChanged?.Invoke(posture);
        }

        // Fresh defaults each play session, even with domain reload off (matches the authored scene).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetToDefaults() => Posture = PlayPosture.Seated;
    }
}
