using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>The title card's three ways in (owner, 2026-11-26: Story, Sandbox, Play together).</summary>
    public class TitleChoicePlayModeTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
            foreach (var lore in StoryLore.Levels) PlayerPrefs.DeleteKey(StoryLore.RestoredKey(lore.Instrument));
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (StoryDirector.Instance != null) StoryDirector.Instance.Leave("test over");
            var empty = SceneManager.CreateScene($"TitleChoiceEmpty{Time.frameCount}");
            SceneManager.SetActiveScene(empty);
            var real = SceneManager.GetSceneByPath(ScenePath);
            if (real.isLoaded) yield return SceneManager.UnloadSceneAsync(real);
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
            LogAssert.ignoreFailingMessages = false;
        }

        static TitleStartButton Choice(TitleChoice which) =>
            TitleScreen.Instance.Card.GetComponentsInChildren<TitleStartButton>(true).First(b => b.Choice == which);

        static IEnumerator UntilParked(float seconds = 8f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!TitleScreen.Instance.Parked && Time.realtimeSinceStartup < end) yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator StoryOnTheTitleOpensStoryAndLeavesTheLessonForLater()
        {
            Assert.IsTrue(TitleScreen.Instance.Showing);
            Choice(TitleChoice.Story).Press();
            Assert.AreEqual(TitleChoice.Story, TitleScreen.Instance.Choice);
            yield return UntilParked();
            Assert.IsTrue(TitleScreen.Instance.Parked, "the card tucks away first");
            Assert.IsTrue(StoryDirector.Instance.Running, "then Story opens");
            Assert.AreEqual(StoryDirector.Stage.Intro, StoryDirector.Instance.Current, "on Level 1's card");
            Assert.IsFalse(TutorialDirector.RememberedDone, "the sandbox's lesson is not skipped: it waits for the sandbox");
            var intro = Object.FindFirstObjectByType<IntroSequence>();
            if (intro != null) Assert.IsFalse(intro.Done || intro.Playing, "the sandbox's opening waits behind Story");
        }

        [UnityTest]
        public IEnumerator LeavingStoryChosenOnTheTitleGoesBackToTheTitle()
        {
            // owner, headset 2026-11-26: "shouldn't it take me back to the sign post at the beginning so I can select sandbox?"
            Choice(TitleChoice.Story).Press();
            yield return UntilParked();
            StoryDirector.Instance.Leave("test leave");
            yield return null;
            Assert.IsTrue(TitleScreen.Instance.Showing, "the card is back");
            Assert.IsFalse(TitleScreen.Instance.Parked);
            Choice(TitleChoice.Sandbox).Press();
            yield return UntilParked();
            Assert.AreEqual(TitleChoice.Sandbox, TitleScreen.Instance.Choice);
            Assert.IsFalse(StoryDirector.Instance.Running, "then the sandbox, as from the start");
            var intro = Object.FindFirstObjectByType<IntroSequence>();
            if (intro != null) Assert.IsTrue(intro.Done || intro.Playing, "and its opening plays now");
        }

        [UnityTest]
        public IEnumerator PlayTogetherOnTheTitleBringsOutTheBattleCard()
        {
            Choice(TitleChoice.Together).Press();
            yield return UntilParked();
            Assert.IsTrue(EchoBattleDirector.Instance.Shown, "the Play together card comes out");
            Assert.IsFalse(StoryDirector.Instance.Running);
        }

        [UnityTest]
        public IEnumerator SandboxOnTheTitleIsTheOpeningAsBefore()
        {
            Choice(TitleChoice.Sandbox).Press();
            yield return UntilParked();
            Assert.IsFalse(StoryDirector.Instance.Running);
            Assert.IsFalse(EchoBattleDirector.Instance.Shown);
        }
    }
}
