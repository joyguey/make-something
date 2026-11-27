using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// On the real scene: story mode is separate (owner, 2026-11-25: "an entirely new feature and not affect the rest of
    /// the game"). Starting it hides the sandbox and pauses its input; leaving puts every picture and the input back.
    /// </summary>
    public class StoryPlayModeTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
            PlayerPrefs.DeleteKey(StoryDirector.EasyPref);
            foreach (var lore in StoryLore.Levels) PlayerPrefs.DeleteKey(StoryLore.RestoredKey(lore.Instrument)); // start at Level 1
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            Object.FindFirstObjectByType<IntroSequence>().Finish(); // free play
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (StoryDirector.Instance != null) StoryDirector.Instance.Leave("test over");
            var empty = SceneManager.CreateScene($"StoryEmpty{Time.frameCount}");
            SceneManager.SetActiveScene(empty);
            var real = SceneManager.GetSceneByPath(ScenePath);
            if (real.isLoaded) yield return SceneManager.UnloadSceneAsync(real);
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
            PlayerPrefs.DeleteKey(StoryDirector.EasyPref);
        }

        [UnityTest]
        public IEnumerator StoryHidesTheSandboxAndLeavingPutsItAllBack()
        {
            var story = StoryDirector.Instance;
            Assert.IsNotNull(story, "a story director in the sandbox scene");
            Assert.IsNull(GameObject.Find("StoryTestSign"), "no floating Play Story sign (owner, 2026-11-26)");
            var menuStory = Object.FindFirstObjectByType<StoryMenuButton>(FindObjectsInactive.Include);
            Assert.IsNotNull(menuStory, "Story is on the menu");
            Assert.IsTrue(menuStory.transform.IsChildOf(Object.FindFirstObjectByType<MenuTray>(FindObjectsInactive.Include).transform), "on the menu tray");
            var router = Object.FindFirstObjectByType<InstrumentInputRouter>();
            var camera = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(c => c.CompareTag("MainCamera"));
            int maskBefore = camera.cullingMask;
            var hands = GameObject.Find("PlayerHands");
            var handLayers = hands != null ? hands.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject.layer).ToArray() : new int[0];
            var token = Object.FindObjectsByType<OrbToken>(FindObjectsSortMode.None).First().GetComponentInChildren<Renderer>();
            Assert.IsTrue(token.enabled);

            menuStory.Press();
            yield return null;
            Assert.IsTrue(story.Running);
            Assert.AreEqual(StoryDirector.Stage.Intro, story.Current);
            Assert.IsTrue(story.CameraOnStory, "the camera draws only story mode: the sandbox is hidden");
            Assert.IsTrue(token.enabled, "without touching the sandbox's own pictures");
            Assert.IsFalse(router.enabled, "the sandbox's taps pause");
            Assert.IsNotNull(GameObject.Find("StoryStage"), "the portal and its stage");
            Assert.IsTrue(HandOutline.Wanted, "the hands' outlines show in story mode");
            for (int i = 0; i < 30; i++) yield return null;

            var buttons = Object.FindObjectsByType<StoryButton>(FindObjectsSortMode.None).Where(b => b.name.StartsWith("StoryButton_")).ToArray();
            Assert.AreEqual(3, buttons.Length, "Pause, Easy aim, Leave");
            var begin = Object.FindObjectsByType<StoryButton>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == "CardButton");
            Assert.IsNotNull(begin, "the intro card's Begin");
            Assert.AreEqual(StoryDirector.Stage.Intro, story.Current, "waits on the card");
            begin.Press();
            Assert.AreEqual(StoryDirector.Stage.WaveCue, story.Current, "Begin starts the level");
            var leave = buttons.OrderBy(b => b.transform.localPosition.x).Last();
            var pause = buttons.OrderBy(b => b.transform.localPosition.x).First();
            pause.Press();
            Assert.IsTrue(story.Paused);
            pause.Press();
            Assert.IsFalse(story.Paused);
            leave.Press();
            yield return null;
            Assert.IsTrue(story.Running, "Leave asks first");
            leave.Press();
            yield return null;

            Assert.IsFalse(story.Running);
            Assert.IsTrue(router.enabled, "the sandbox's taps are back");
            Assert.IsTrue(token.enabled);
            Assert.IsFalse(story.CameraOnStory);
            Assert.IsNull(GameObject.Find("StoryStage"));
            Assert.AreEqual(maskBefore, camera.cullingMask, "the camera draws everything it drew before");
            if (hands != null) CollectionAssert.AreEqual(handLayers, hands.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject.layer).ToArray(), "the hands back on their own layer");
        }

        [UnityTest]
        public IEnumerator EasyAimIsRememberedAndStoryCanStartAgain()
        {
            var story = StoryDirector.Instance;
            story.Begin();
            yield return null;
            var easy = Object.FindObjectsByType<StoryButton>(FindObjectsSortMode.None).Where(b => b.name.StartsWith("StoryButton_")).OrderBy(b => b.transform.localPosition.x).ElementAt(1);
            easy.Press();
            Assert.IsTrue(story.Easy);
            Assert.AreEqual(1, PlayerPrefs.GetInt(StoryDirector.EasyPref, 0));
            story.Leave("test");
            yield return null;
            story.Begin();
            yield return null;
            Assert.IsTrue(story.Running, "starts again");
            Assert.IsTrue(story.Easy);
            story.Leave("test");
        }
        [UnityTest]
        public IEnumerator OnceTheUduIsRestoredStoryOpensOnLevelTwoTheErhu()
        {
            PlayerPrefs.SetInt(StoryLore.RestoredKey(InstrumentType.Udu), 1);
            var story = StoryDirector.Instance;
            story.Begin();
            yield return null;
            Assert.AreEqual(1, story.LevelIndex, "the first instrument still to mend");
            Assert.AreEqual(2, story.Level.Number);
            Assert.AreEqual(StoryDirector.Stage.Intro, story.Current);
            var back = Object.FindObjectsByType<StoryButton>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == "LevelButton");
            Assert.IsNotNull(back, "Level 1 can still be chosen");
            back.Press();
            yield return null;
            Assert.AreEqual(0, story.LevelIndex);
            Assert.AreEqual(StoryDirector.Stage.Intro, story.Current, "on its card");
            story.Leave("test");
            PlayerPrefs.DeleteKey(StoryLore.RestoredKey(InstrumentType.Udu));
        }
    }
}
