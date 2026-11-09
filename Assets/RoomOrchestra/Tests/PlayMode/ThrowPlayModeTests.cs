using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// Owner, headset 2026-11-08: "When I try to pinch and pull back on the floating ball, it would then toss it
    /// prematurely behind me." A pull toward the player is a slingshot: the ball flies ahead, never behind.
    /// </summary>
    public class ThrowPlayModeTests
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
            Object.FindFirstObjectByType<IntroSequence>().Finish(); // free play
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            var empty = SceneManager.CreateScene($"ThrowEmpty{Time.frameCount}");
            SceneManager.SetActiveScene(empty);
            var real = SceneManager.GetSceneByPath(ScenePath);
            if (real.isLoaded) yield return SceneManager.UnloadSceneAsync(real);
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
        }

        static Ball Newest() => Object.FindObjectsByType<Ball>(FindObjectsSortMode.None)
            .Where(b => b.InFlight).OrderByDescending(b => b.LaunchTime).FirstOrDefault();

        IEnumerator Drag(BallLauncher launcher, Vector3 step, Vector3 facing)
        {
            var at = launcher.ReadyBall.transform.position;
            launcher.BeginGrab(7, at);
            for (int i = 0; i < 8; i++)
            {
                yield return null;
                at += step * Mathf.Max(Time.deltaTime, 0.005f);
                launcher.UpdateGrab(7, at, facing);
            }
            int before = launcher.ThrowCount;
            launcher.EndGrab(7, facing);
            Assert.AreEqual(before + 1, launcher.ThrowCount, "it threw");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PullingTheBallBackTowardYouThrowsItAheadNotBehind()
        {
            var launcher = Object.FindFirstObjectByType<BallLauncher>();
            var facing = GameObject.Find("RoomOrchestra").transform.forward;
            facing.y = 0f;
            yield return Drag(launcher, -facing.normalized * 2.5f, facing); // 2.5 m/s straight back toward the player
            var ball = Newest();
            Assert.IsNotNull(ball);
            var v = ball.GetComponent<Rigidbody>().linearVelocity;
            v.y = 0f;
            Assert.Greater(Vector3.Dot(v, facing.normalized), 0.5f, $"flies ahead ({v}), not behind");
        }

        [UnityTest]
        public IEnumerator AForwardSwingStillThrowsTheWayYouSwing()
        {
            var launcher = Object.FindFirstObjectByType<BallLauncher>();
            var facing = GameObject.Find("RoomOrchestra").transform.forward;
            facing.y = 0f;
            yield return Drag(launcher, facing.normalized * 3f, facing);
            var v = Newest().GetComponent<Rigidbody>().linearVelocity;
            v.y = 0f;
            Assert.Greater(Vector3.Dot(v, facing.normalized), 0.5f);
        }
    }
}
