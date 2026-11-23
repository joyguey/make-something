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
        public void TextSizeSwitchesSizeAndHighContrastIsAlwaysOn()
        {
            // owner, headset 2026-11-08: high contrast by default; the button only changes the text size
            Assert.AreEqual(ViewMode.HighContrast, ViewModes.Default);
            Assert.AreEqual(ViewMode.LargeHighContrast, ViewModes.Next(ViewModes.Default));
            Assert.AreEqual(ViewMode.HighContrast, ViewModes.Next(ViewMode.LargeHighContrast));
            Assert.AreEqual(ViewMode.HighContrast, ViewModes.WithContrast(ViewMode.Standard), "an older saved setting gets contrast");
            Assert.AreEqual(ViewMode.LargeHighContrast, ViewModes.WithContrast(ViewMode.LargeText));
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
            // The toy buttons' plum caps and the instrument-tinted tiles keep their colours in high contrast (the default
            // view): their white words and gold icons already clear 4.5:1 (GameplayLookTests). The plain dark plates still darken.
            Assert.Greater(view.Slabs.Count, 2, "the plain plates darken");
            Assert.Greater(view.Icons.Count, 8, "icons brighten");
            Assert.IsFalse(view.Icons.Any(r => r.GetComponentInParent<OrbToken>() != null), "the characters keep their colours");
        }
    }
}
