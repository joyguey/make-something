using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>On the real scene: Clear, Lesson and Quit ask "Sure?" first; anything else pressed is a "no".</summary>
    public class ConfirmPlayModeTests
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
            QuitButton.QuitRequests = 0;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            var empty = SceneManager.CreateScene($"ConfirmEmpty{Time.frameCount}");
            SceneManager.SetActiveScene(empty);
            var real = SceneManager.GetSceneByPath(ScenePath);
            if (real.isLoaded) yield return SceneManager.UnloadSceneAsync(real);
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
            QuitButton.QuitRequests = 0;
        }

        static GameObject Tap<T>(System.Func<T, bool> which = null) where T : Component
        {
            var owner = Object.FindObjectsByType<T>(FindObjectsSortMode.None).First(c => which == null || which(c));
            return owner.GetComponentsInChildren<Collider>().First(c => !c.isTrigger).gameObject;
        }

        static string LabelOf(GameObject tapped) =>
            InstrumentInputRouter.PressableOwner(tapped).GetComponentInChildren<LocalizedLabel>().Text.text;

        [UnityTest]
        public IEnumerator ClearAsksFirstAndAnotherPressCancels()
        {
            var conductor = EchoConductor.Instance;
            Assert.IsTrue(InstrumentInputRouter.Activate(Tap<PitchTile>()), "a note");
            yield return null;
            int notes = conductor.Echoes.Count;
            Assert.Greater(notes, 0);

            var clear = Tap<MusicControlButton>(b => b.ButtonKind == MusicControlButton.Kind.Clear);
            Assert.IsTrue(InstrumentInputRouter.Activate(clear));
            Assert.AreEqual(notes, conductor.Echoes.Count, "the first tap only asks");
            Assert.AreEqual("Sure?", LabelOf(clear));

            Assert.IsTrue(InstrumentInputRouter.Activate(Tap<HeightButton>()), "pressing something else...");
            Assert.AreEqual("Clear", LabelOf(clear), "...puts the label back");
            Assert.IsTrue(InstrumentInputRouter.Activate(clear));
            Assert.AreEqual(notes, conductor.Echoes.Count, "...and Clear asks again");

            Assert.IsTrue(InstrumentInputRouter.Activate(clear));
            Assert.AreEqual(0, conductor.Echoes.Count, "the second tap clears");
            Assert.AreEqual("Clear", LabelOf(clear));

            Assert.IsTrue(InstrumentInputRouter.Activate(clear));
            Assert.AreEqual(0, InstrumentInputRouter.ConfirmAsking, "nothing to lose: Clear doesn't ask");
            ComfortSettings.SetPosture(PlayPosture.Seated);
        }

        [UnityTest]
        public IEnumerator LessonAsksFirstAndFollowsTheLanguage()
        {
            var tutorial = TutorialDirector.Instance;
            tutorial.Skip();
            yield return new WaitForSeconds(StationReveal.AppearAfterSeconds + TutorialDirector.PopSeconds + 0.1f);
            Assert.IsFalse(tutorial.Active, "free play");
            var lesson = Tap<TutorialReplayButton>();
            Assert.IsTrue(InstrumentInputRouter.Activate(lesson));
            Assert.IsFalse(tutorial.Active, "the first tap only asks");
            Assert.AreEqual("Sure?", LabelOf(lesson));
            Localization.Set(UiLanguage.Chinese, remember: false);
            Assert.AreEqual("确定？", LabelOf(lesson));
            Localization.Set(UiLanguage.English, remember: false);
            Assert.IsTrue(InstrumentInputRouter.Activate(lesson));
            float until = Time.time + 3f;
            while (!tutorial.Active && Time.time < until) yield return null;
            Assert.IsTrue(tutorial.Active, "the second tap starts the lesson again");
            Assert.AreEqual("Lesson", LabelOf(lesson));
        }

        [UnityTest]
        public IEnumerator QuitAsksFirst()
        {
            var quit = Object.FindObjectsByType<QuitButton>(FindObjectsSortMode.None);
            Assert.IsNotEmpty(quit, "the scene has a Quit button");
            foreach (var button in quit)
            {
                var tap = button.GetComponentsInChildren<Collider>().First(c => !c.isTrigger).gameObject;
                QuitButton.QuitRequests = 0;
                Assert.IsTrue(InstrumentInputRouter.Activate(tap));
                Assert.AreEqual(0, QuitButton.QuitRequests, $"{button.name}: the first tap only asks");
                Assert.AreEqual("Sure?", LabelOf(tap));
                Assert.IsTrue(InstrumentInputRouter.Activate(tap));
                Assert.AreEqual(1, QuitButton.QuitRequests, $"{button.name}: the second tap quits");
            }
            yield return null;
        }
    }
}
