using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// Owner, headset 2026-11-08: "right at the start of the game, I still see the bundle of furniture, menu items and
    /// random things in front of me. the ball and throw trajectory is still not seated on top of the cloud and there's a
    /// yellow disk behind the menu." At the title only the title card, the cloud and the ball on it are drawn near the
    /// player, also after the headset recentres the stage on them (it does, a second in).
    /// </summary>
    public class CleanStartPlayModeTests
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
            var empty = SceneManager.CreateScene($"CleanEmpty{Time.frameCount}");
            SceneManager.SetActiveScene(empty);
            var real = SceneManager.GetSceneByPath(ScenePath);
            if (real.isLoaded) yield return SceneManager.UnloadSceneAsync(real);
        }

        static string[] DrawnStrays()
        {
            var dispenser = GameObject.Find("RoomOrchestra").transform.Find("BallDispenser");
            var park = dispenser.Find("ParkPoint").position;
            var cloud = new[] { dispenser.Find("Pedestal"), dispenser.Find("CloudPuffs") };
            var launcher = Object.FindFirstObjectByType<BallLauncher>();
            var title = GameObject.Find("TitleCard");
            return Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy)
                .Where(r => (r.bounds.center - park).magnitude < 0.6f)
                .Where(r => !cloud.Any(c => c != null && r.transform.IsChildOf(c)))
                .Where(r => launcher.ReadyBall == null || !r.transform.IsChildOf(launcher.ReadyBall.transform))
                .Where(r => title == null || !r.transform.IsChildOf(title.transform))
                .Select(r => $"{r.name} ({r.transform.parent?.name})").ToArray();
        }

        [UnityTest]
        public IEnumerator AtTheTitleOnlyTheCardTheCloudAndItsBallShow()
        {
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(TitleScreen.Instance.Showing);
            CollectionAssert.IsEmpty(DrawnStrays(), "nothing else is drawn around the cloud");
            var arc = Object.FindFirstObjectByType<AimArc>();
            Assert.IsFalse(arc.GetComponentsInChildren<Renderer>().Any(r => r.enabled), "no throw trajectory at the title");
            var marker = GameObject.Find("FocusMarker");
            Assert.IsFalse(marker.GetComponent<Renderer>().enabled, "no yellow focus disc at the title");

            // the headset recentres the stage on the player (here: 0.4 m aside, turned 20 degrees)
            StageAnchor.Instance.Recenter(new Vector3(0.4f, 1.5f, -0.3f), Quaternion.Euler(0f, 20f, 0f));
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.25f);
            CollectionAssert.IsEmpty(DrawnStrays(), "still nothing else once the stage has moved");
            var launcher = Object.FindFirstObjectByType<BallLauncher>();
            var spawn = GameObject.Find("RoomOrchestra").transform.Find("BallDispenser/SpawnPoint");
            Assert.Less(Vector3.Distance(launcher.ReadyBall.transform.position, spawn.position), 0.01f, "the ball sits on its cloud");

            // through the opening the cloud glides home with the stage, the ball riding on it
            TitleScreen.Instance.SendMessage("Begin", SendMessageOptions.DontRequireReceiver);
            Object.FindFirstObjectByType<IntroSequence>().Finish();
            yield return null;
            Assert.Less(Vector3.Distance(launcher.ReadyBall.transform.position, spawn.position), 0.01f, "still on its cloud");
            var stage = GameObject.Find("RoomOrchestra").transform;
            var dispenser = stage.Find("BallDispenser");
            var home = stage.InverseTransformPoint(dispenser.position);
            Assert.AreEqual(0.62f, home.x, 0.05f, "the cloud is at its home beside the leaf, in the stage's own space");
            Assert.AreEqual(1.15f, home.z, 0.05f);
        }
    }
}
