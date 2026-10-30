using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>A11Y-08 interface scale and contrast: the rules, and what the scene wires.</summary>
    public class ViewModeTests
    {
        [Test]
        public void ViewStepsThroughFourSettingsAndBack()
        {
            var seen = new System.Collections.Generic.List<ViewMode>();
            var mode = ViewMode.Standard;
            for (int i = 0; i < ViewModes.Count; i++) { seen.Add(mode); mode = ViewModes.Next(mode); }
            Assert.AreEqual(ViewMode.Standard, mode, "back to Standard");
            Assert.AreEqual(ViewModes.Count, seen.Distinct().Count());
            Assert.IsTrue(ViewModes.IsLarge(ViewMode.LargeHighContrast) && ViewModes.IsHighContrast(ViewMode.LargeHighContrast));
            Assert.AreEqual(1f, ViewModes.TextScale(ViewMode.HighContrast));
            Assert.Greater(ViewModes.TextScale(ViewMode.LargeText), 1.2f);
        }

        [Test]
        public void HighContrastTextMeetsTheStrongestContrastLevel()
        {
            var slab = ViewSettings.ContrastSlab;
            var text = ViewSettings.ContrastText;
            double ratio = ViewModes.Contrast(ViewModes.Luminance(text.r, text.g, text.b), ViewModes.Luminance(slab.r, slab.g, slab.b));
            Assert.Greater(ratio, 7.0, "WCAG AAA for text: 7:1");
            var icon = ViewSettings.ContrastIcon;
            Assert.Greater(ViewModes.Contrast(ViewModes.Luminance(icon.r, icon.g, icon.b), ViewModes.Luminance(slab.r, slab.g, slab.b)), 4.5, "icons stand out too");
        }

        [Test]
        public void TheSceneWiresTheViewButtonAndWhatItChanges()
        {
            EditorSceneManager.OpenScene("Assets/RoomOrchestra/Scenes/RoomOrchestra.unity", OpenSceneMode.Single);
            var view = Object.FindFirstObjectByType<ViewSettings>();
            Assert.IsNotNull(view);
            Assert.IsNotNull(Object.FindFirstObjectByType<ViewButton>(), "a View button on the leaf");
            Assert.Greater(view.Labels.Count, 10, "the leaf's labels grow");
            Assert.IsTrue(view.Labels.All(l => !l.enableAutoSizing), "words that fit their tiles are left alone");
            Assert.Greater(view.Slabs.Count, 20, "buttons and tiles darken");
            Assert.Greater(view.Icons.Count, 8, "icons brighten");
            Assert.IsFalse(view.Icons.Any(r => r.GetComponentInParent<OrbToken>() != null), "the characters keep their colours");
        }
    }
}
