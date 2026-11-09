using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>A11Y-08 on the REAL scene: the View button grows labels and switches high contrast, and is remembered.</summary>
    public class ViewPlayModeTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";

        IEnumerator Load()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            Object.FindFirstObjectByType<IntroSequence>().Finish();
            yield return null;
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            PlayerPrefs.DeleteKey(ViewSettings.PrefKey);
            yield return Load();
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            var empty = SceneManager.CreateScene($"ViewEmpty{Time.frameCount}");
            SceneManager.SetActiveScene(empty);
            var real = SceneManager.GetSceneByPath(ScenePath);
            if (real.isLoaded) yield return SceneManager.UnloadSceneAsync(real);
            PlayerPrefs.DeleteKey(ViewSettings.PrefKey);
        }

        [UnityTest]
        // Owner, headset 2026-11-08: "use the high contrast as the default and leave the button to adjust the size of the
        // text ... I like the highlighted yellow."
        public IEnumerator HighContrastFromTheStartAndTheButtonOnlyChangesTextSize()
        {
            var view = ViewSettings.Instance;
            var button = Object.FindFirstObjectByType<ViewButton>();
            var label = view.Labels.First(l => l.text == "Undo");
            yield return null;
            Assert.AreEqual(ViewMode.HighContrast, view.Mode, "high contrast for a new player");
            Assert.Less(Vector4.Distance(ViewSettings.ContrastSlab, view.Slabs[0].sharedMaterial.color), 1e-3f, "near-black slabs from the start");
            Assert.AreEqual(Color.white, label.color, "pure white text");
            Assert.Less(Vector4.Distance(ViewSettings.ContrastIcon, view.Icons[0].sharedMaterial.color), 1e-3f, "the bright yellow icons");
            Assert.AreEqual("Text size\nStandard", button.Label.text);
            float before = label.transform.localScale.x;

            Assert.IsTrue(InstrumentInputRouter.Activate(button.GetComponentInChildren<Collider>().gameObject));
            Assert.AreEqual(ViewMode.LargeHighContrast, view.Mode);
            Assert.AreEqual(before * ViewModes.LargeTextScale, label.transform.localScale.x, 1e-4f, "labels grow");
            Assert.AreEqual("Text size\nLarge", button.Label.text);
            Assert.Less(Vector4.Distance(ViewSettings.ContrastSlab, view.Slabs[0].sharedMaterial.color), 1e-3f, "still high contrast");

            yield return Load(); // the next launch
            view = ViewSettings.Instance;
            Assert.AreEqual(ViewMode.LargeHighContrast, view.Mode, "the size is remembered");
            view.Next();
            Assert.AreEqual(ViewMode.HighContrast, view.Mode, "back to standard size, still high contrast");
            Assert.Less(Vector4.Distance(ViewSettings.ContrastSlab, view.Slabs[0].sharedMaterial.color), 1e-3f);
        }
    }
}
