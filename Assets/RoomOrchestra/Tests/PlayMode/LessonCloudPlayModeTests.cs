using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// Owner, headset 2026-11-12: "during the tutorial, there is a yellow disk that still appears on top of the ball that
    /// is selected on the cloud". Through the lesson, only the ball (and the lesson's arrow pointing at it) is drawn on
    /// the cloud: nothing else that waits there shows.
    /// </summary>
    public class LessonCloudPlayModeTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            var empty = SceneManager.CreateScene($"LessonCloudEmpty{Time.frameCount}");
            SceneManager.SetActiveScene(empty);
            var real = SceneManager.GetSceneByPath(ScenePath);
            if (real.isLoaded) yield return SceneManager.UnloadSceneAsync(real);
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
        }

        static string[] DrawnAroundTheBall(BallLauncher launcher)
        {
            var ball = launcher.ReadyBall;
            var dispenser = GameObject.Find("RoomOrchestra").transform.Find("BallDispenser");
            var cloud = new[] { dispenser.Find("Pedestal"), dispenser.Find("CloudPuffs") };
            var arrow = TutorialDirector.Instance != null ? TutorialDirector.Instance.Arrow : null;
            return Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.bounds.size.sqrMagnitude > 0f)
                .Where(r => (r.bounds.center - ball.transform.position).magnitude < 0.3f)
                .Where(r => !r.transform.IsChildOf(ball.transform))
                .Where(r => !cloud.Any(c => c != null && r.transform.IsChildOf(c)))
                .Where(r => arrow == null || !r.transform.IsChildOf(arrow))
                .Where(r => !(r is LineRenderer))
                .Where(r => r.GetComponentInParent<AimArc>() == null) // the throw's trajectory starts at the ball
                .Select(r => $"{r.name} ({r.transform.parent?.name}) at {r.bounds.center:F2} size {r.bounds.size:F2}").ToArray();
        }

        [UnityTest]
        public IEnumerator NothingButTheBallShowsOnTheCloudDuringTheLesson()
        {
            yield return new WaitForSeconds(0.3f);
            TitleScreen.Instance.SendMessage("Begin", SendMessageOptions.DontRequireReceiver);
            Object.FindFirstObjectByType<IntroSequence>().Finish(skipTutorial: false);
            float until = Time.time + 5f;
            while (!TutorialDirector.Instance.Active && Time.time < until) yield return null;
            var launcher = Object.FindFirstObjectByType<BallLauncher>();
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForSeconds(0.5f);
                Assert.IsTrue(TutorialDirector.Instance.Active, "the lesson runs");
                CollectionAssert.IsEmpty(DrawnAroundTheBall(launcher), $"nothing drawn on the cloud with the ball ({i * 0.5f + 0.5f} s in)");
            }
            // whatever the lesson focuses on (an instrument still waiting in the cloud too), the yellow disc is never over the ball
            var selector = OrbSelector.Instance;
            var marker = GameObject.Find("FocusMarker").GetComponent<Renderer>();
            foreach (var target in Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None))
            {
                selector.Focus(target);
                yield return null;
                bool overBall = Vector3.Distance(marker.bounds.center, launcher.ReadyBall.transform.position) < OrbSelector.ClearOfBall;
                Assert.IsFalse(marker.enabled && overBall, $"focused on the {target.Instrument}: no yellow disc over the ball");
            }
        }
    }
}
