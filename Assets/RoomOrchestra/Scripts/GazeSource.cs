using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Where the player is looking, in rig space (V3 review: "use eye tracking to determine where the UI should
    /// interact and pinch to confirm"). PICO's eye tracking where the headset has it and the player allows it;
    /// otherwise the head's own direction, so look-and-pinch works on every headset. Eye tracking is asked for at most
    /// every few seconds while it isn't delivering (PICO's call logs an error each time it fails).
    /// </summary>
    public static class GazeSource
    {
        public const float RetrySeconds = 5f;
        /// <summary>Eye gaze is smoothed a little (s): the eyes dart; a pointer that darts with them is hard to read.</summary>
        public const float SmoothSeconds = 0.05f;

        static float s_NextTry;
        static bool s_Eyes, s_Logged;
        static Vector3 s_Smoothed = Vector3.forward;

        /// <summary>Is the pointer following the eyes (true) or the head (false)?</summary>
        public static bool UsingEyes => s_Eyes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_NextTry = 0f;
            s_Eyes = false;
            s_Logged = false;
            s_Smoothed = Vector3.forward;
        }

        /// <summary>The eyes' ray in rig space: from the head, toward where the player looks.</summary>
        public static bool TryGetGaze(out Vector3 origin, out Quaternion rotation)
        {
            origin = default;
            rotation = Quaternion.identity;
            var rig = XrRig.Instance;
            var head = rig != null ? rig.Head : null;
            if (head == null || !rig.Tracked) return false;
            var local = head.transform;
            var direction = Vector3.forward; // head space
            if (TryEyes(out var eyes)) direction = eyes;
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / SmoothSeconds);
            s_Smoothed = Vector3.Slerp(s_Smoothed, direction, s_Eyes ? k : 1f).normalized;
            origin = local.localPosition;
            rotation = local.localRotation * Quaternion.LookRotation(s_Smoothed, Vector3.up);
            return true;
        }

        static bool TryEyes(out Vector3 direction)
        {
            direction = Vector3.forward;
#if UNITY_ANDROID && !UNITY_EDITOR && ENABLE_PICO_XR_SDK
            if (!s_Eyes && Time.unscaledTime < s_NextTry) return false;
            var manager = ByteDance.PICO.XR.PXR_Manager.Instance;
            if (manager == null || !manager.eyeTracking) { s_NextTry = Time.unscaledTime + RetrySeconds; return false; }
            if (ByteDance.PICO.XR.PXR_EyeTracking.GetCombineEyeGazeVector(out var v) && v.sqrMagnitude > 1e-6f)
            {
                if (!s_Eyes) Debug.Log("[RoomOrchestra] Look and pinch: following the eyes (eye tracking)");
                s_Eyes = true;
                direction = v.normalized;
                return true;
            }
            if (s_Eyes || !s_Logged) Debug.Log("[RoomOrchestra] Look and pinch: following the head (no eye tracking data)");
            s_Logged = true;
            s_Eyes = false;
            s_NextTry = Time.unscaledTime + RetrySeconds;
#endif
            return false;
        }
    }
}
