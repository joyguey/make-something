using System.Collections;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Headset and emulator testing without anyone wearing the headset: launched by adb with
    /// <c>--ez le_freeplay true</c>, the app presses Start on the title card and skips the lesson once the opening is
    /// done, as a player would, without remembering the lesson as done. Without the extra it does nothing.
    /// </summary>
    public static class LaunchTestingOptions
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Read()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            bool freePlay = false;
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity.Call<AndroidJavaObject>("getIntent");
                freePlay = intent.Call<bool>("getBooleanExtra", "le_freeplay", false);
            }
            catch (System.Exception) { return; }
            if (!freePlay) return;
            Debug.Log("[RoomOrchestra] Testing: straight to free play (Start, then Skip)");
            var host = new GameObject("LaunchTestingOptions").AddComponent<Runner>();
            Object.DontDestroyOnLoad(host.gameObject);
            host.StartCoroutine(host.FreePlay());
#endif
        }

        sealed class Runner : MonoBehaviour
        {
            public IEnumerator FreePlay()
            {
                yield return new WaitForSeconds(JellyBody.StartDelaySeconds + 1f);
                TitleScreen.Instance?.Begin();
                while (IntroSequence.Instance != null && !IntroSequence.Instance.Done) yield return null;
                yield return new WaitForSeconds(1f);
                var tutorial = TutorialDirector.Instance;
                while (tutorial != null && !tutorial.Active && !tutorial.Plan.Done) yield return null;
                tutorial?.Skip();
                // A test run must not mark the lesson done for the player (headset 2026-11-08: the owner's next
                // launch went straight to free play because of it).
                PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
                PlayerPrefs.Save();
                Debug.Log("[RoomOrchestra] Testing: lesson skipped (not remembered: the next normal launch has the lesson)");
            }
        }
    }
}
