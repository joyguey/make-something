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
        public IEnumerator ViewGrowsLabelsThenSwitchesHighContrastAndIsRemembered()
        {
            var view = ViewSettings.Instance;
            var button = Object.FindFirstObjectByType<ViewButton>();
            var label = view.Labels.First(l => l.text == "Undo");
            float before = label.transform.localScale.x;
            var slab = view.Slabs[0];
            var slabMaterial = slab.sharedMaterial;
            Assert.AreEqual("View\nStandard", button.Label.text);

            Assert.IsTrue(InstrumentInputRouter.Activate(button.GetComponentInChildren<Collider>().gameObject));
            Assert.AreEqual(ViewMode.LargeText, view.Mode);
            Assert.AreEqual(before * ViewModes.LargeTextScale, label.transform.localScale.x, 1e-4f, "labels grow");
            Assert.AreEqual("View\nLarge text", button.Label.text);

            view.Next();
            Assert.AreEqual(ViewMode.HighContrast, view.Mode);
            Assert.AreEqual(before, label.transform.localScale.x, 1e-4f, "back to normal size");
            Assert.Less(Vector4.Distance(ViewSettings.ContrastSlab, slab.sharedMaterial.color), 1e-3f, "near-black slabs");
            Assert.AreEqual(Color.white, label.color, "pure white text");
            Assert.Less(Vector4.Distance(ViewSettings.ContrastIcon, view.Icons[0].sharedMaterial.color), 1e-3f, "bright icons");
            Assert.Greater(Vector4.Distance(ViewSettings.ContrastSlab, slabMaterial.color), 0.05f, "the project's own material is untouched (a runtime copy is used)");

            yield return Load(); // the next launch
            view = ViewSettings.Instance;
            Assert.AreEqual(ViewMode.HighContrast, view.Mode, "remembered");
            while (Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds + 0.1f) yield return null;
            yield return null;
            Assert.Less(Vector4.Distance(ViewSettings.ContrastSlab, view.Slabs[0].sharedMaterial.color), 1e-3f, "applied once the renderer is ready");

            view.Next();
            view.Next();
            Assert.AreEqual(ViewMode.Standard, view.Mode);
            Assert.AreNotEqual(ViewSettings.ContrastSlab, view.Slabs[0].sharedMaterial.color, "Standard restores the slabs");
        }
    }
}
