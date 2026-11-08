using System.Collections;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Quit (George, 2026-11-03: "a main menu with the app's name and a Quit button"). Asks to confirm first
    /// (<see cref="ConfirmGate"/>, via the input router), then closes the app.
    ///
    /// Headset, 2026-11-07: <c>Application.Quit()</c> crashed on PICO OS 6.2 every time. Unity stops XR after the
    /// system has already taken the Stage away, and PICO's own head-tracking shutdown
    /// (<c>libStagePlatform HMDTracking::stop</c>) dereferences a null inside the spatial runtime service. So on the
    /// headset the app saves its settings, asks Android to close its task (the system removes the Stage), and ends
    /// its own process, which is a normal exit, not a crash, and skips that broken shutdown path.
    /// </summary>
    public class QuitButton : MonoBehaviour
    {
        /// <summary>Set by tests: counts quits instead of closing the app.</summary>
        public static int QuitRequests { get; set; }

        public void Press()
        {
            QuitRequests++;
            Debug.Log("[RoomOrchestra] Quit");
#if !UNITY_EDITOR
            QuitApp();
#endif
        }

        /// <summary>Closes the app without going through the XR shutdown that crashes on PICO OS 6.2.</summary>
        public static void QuitApp()
        {
            PlayerPrefs.Save();
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("finishAndRemoveTask");
                using var process = new AndroidJavaClass("android.os.Process");
                int pid = process.CallStatic<int>("myPid");
                Debug.Log($"[RoomOrchestra] Quit: task closed, ending process {pid}");
                process.CallStatic("killProcess", pid);
                return;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RoomOrchestra] Quit: couldn't close the task directly ({e.Message}); quitting the usual way");
            }
#endif
            Application.Quit();
        }

        /// <summary>
        /// Headset testing hook: launched with <c>--ef le_autoquit SECONDS</c> (adb), the app quits by itself after
        /// that long, so the quit path can be checked on a headset nobody is wearing. Without the extra it does nothing.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoQuitForTesting()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            float seconds = 0f;
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity.Call<AndroidJavaObject>("getIntent");
                seconds = intent.Call<float>("getFloatExtra", "le_autoquit", 0f);
            }
            catch (System.Exception) { return; }
            if (seconds <= 0f) return;
            Debug.Log($"[RoomOrchestra] Quit: testing hook, quitting by itself in {seconds:F0} s");
            var host = new GameObject("AutoQuitForTesting").AddComponent<QuitTimer>();
            DontDestroyOnLoad(host.gameObject);
            host.StartCoroutine(host.After(seconds));
#endif
        }

        sealed class QuitTimer : MonoBehaviour
        {
            public IEnumerator After(float seconds)
            {
                yield return new WaitForSecondsRealtime(seconds);
                Debug.Log("[RoomOrchestra] Quit");
                QuitApp();
            }
        }
    }
}
