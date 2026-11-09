using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// Owner, headset 2026-11-08: "It also bounced back without hitting the virtual chair. Can you not do the spatial
    /// scan until AFTER the tutorial?" and "The ball disappeared on the cloud after I threw the first one."
    /// </summary>
    public class LessonWallsPlayModeTests
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
            var empty = SceneManager.CreateScene($"WallsEmpty{Time.frameCount}");
            SceneManager.SetActiveScene(empty);
            var real = SceneManager.GetSceneByPath(ScenePath);
            if (real.isLoaded) yield return SceneManager.UnloadSceneAsync(real);
        }

        // A real desk's front face (upright), between the seat and the virtual chair.
        static Mesh DeskFront()
        {
            var m = new Mesh
            {
                vertices = new[] { new Vector3(-0.6f, 0f, 0f), new Vector3(0.6f, 0f, 0f), new Vector3(0.6f, 0.8f, 0f), new Vector3(-0.6f, 0.8f, 0f) },
                triangles = new[] { 0, 2, 1, 0, 3, 2 }
            };
            m.RecalculateNormals();
            return m;
        }

        [UnityTest]
        public IEnumerator TheRealRoomLeavesTheBallAloneDuringTheLesson()
        {
            var walls = RealWalls.Instance;
            walls.ApplyForTest(default, DeskFront(), new Vector3(0f, 0f, 0.7f));
            yield return null;
            var wall = walls.WallFor(default);
            Assert.IsNotNull(wall);
            Assert.IsTrue(VirtualRoom.Instance.Standing, "the lesson's virtual room stands");
            Assert.IsFalse(wall.enabled, "the real desk does not bounce the ball during the lesson");
            VirtualRoom.Instance.HideNow();
            yield return null;
            Assert.IsTrue(wall.enabled, "after the lesson it does");
        }

        [UnityTest]
        public IEnumerator TheNextBallOnTheCloudIsDrawn()
        {
            Object.FindFirstObjectByType<IntroSequence>().Finish();
            var launcher = Object.FindFirstObjectByType<BallLauncher>();
            yield return new WaitForSeconds(0.3f);
            var first = launcher.ReadyBall;
            launcher.ThrowAssisted(first.transform.position, Vector3.forward, new Vector3(0f, 1f, 3f));
            float until = Time.time + 3f;
            while ((launcher.ReadyBall == null || launcher.ReadyBall == first) && Time.time < until) yield return null;
            yield return new WaitForSeconds(0.5f);
            var next = launcher.ReadyBall;
            Assert.IsNotNull(next);
            Assert.AreNotEqual(first, next, "a new ball came up");
            Assert.IsTrue(next.GetComponentsInChildren<Renderer>().Where(r => r.name.Contains("Body") || r.GetComponent<MeshFilter>() != null).Any(r => r.enabled), "and it is drawn on the cloud");
            Assert.IsTrue(next.GetComponentsInChildren<Renderer>().All(r => r.enabled || r.name.Contains("Mouth")), "every part but the parked mouth");
        }
    }
}
