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
            bool freePlay = false, labels = false;
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity.Call<AndroidJavaObject>("getIntent");
                freePlay = intent.Call<bool>("getBooleanExtra", "le_freeplay", false);
                labels = intent.Call<bool>("getBooleanExtra", "le_labels", false);
            }
            catch (System.Exception) { return; }
            if (!freePlay) return;
            Debug.Log("[RoomOrchestra] Testing: straight to free play (Start, then Skip)");
            var host = new GameObject("LaunchTestingOptions").AddComponent<Runner>();
            Object.DontDestroyOnLoad(host.gameObject);
            host.StartCoroutine(host.FreePlay(labels));
#endif
        }

        sealed class Runner : MonoBehaviour
        {
            public IEnumerator FreePlay(bool labels)
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
                if (labels) yield return NoteLabels();
            }

            // --ez le_labels true: two real-object homes in front of the eyes, pointed at all the time, so the note
            // names can be seen on the device's own renderer (emulator screenshots; the headset blocks capture).
            IEnumerator NoteLabels()
            {
                yield return new WaitForSeconds(2f);
                if (StageAnchor.Instance == null || !StageAnchor.Instance.TryGetHeadPose(out var head)) yield break;
                var flat = head.rotation * Vector3.forward;
                flat.y = 0f;
                flat = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
                var right = Vector3.Cross(Vector3.up, flat);
                var look = RealWalls.Instance != null ? RealWalls.Instance.ScanLook : null;
                var style = RealFurniture.NoteLabelStyle();
                var homes = new[]
                {
                    RealHome.Create("TestUdu", InstrumentTarget.Find(InstrumentType.Udu), new Pose(head.position + flat * 1.3f - right * 0.45f + Vector3.down * 0.9f, Quaternion.LookRotation(right)), new Vector3(0.9f, 0.5f, 0.4f), look, style),
                    RealHome.Create("TestChair", InstrumentTarget.Find(Instruments.Chair), new Pose(head.position + flat * 1.3f + right * 0.55f + Vector3.down * 0.9f, Quaternion.LookRotation(right)), new Vector3(0.9f, 0.5f, 0.4f), look, style),
                };
                Debug.Log($"[RoomOrchestra] Testing: targets {InstrumentTarget.Find(InstrumentType.Udu)?.name ?? "none"}, {InstrumentTarget.Find(Instruments.Chair)?.name ?? "none"}");
                Debug.Log($"[RoomOrchestra] Testing: note labels shown on two test objects (style {(style != null ? style.name : "none")}, look {(look != null ? look.name : "none")})");
                while (true)
                {
                    foreach (var h in homes) if (h != null) h.Point(h.transform.TransformPoint(new Vector3(0.3f, 0.25f, 0f)));
                    yield return null;
                }
            }
        }
    }
}
