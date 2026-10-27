using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    public class UiTextTests
    {
        [Test]
        public void EveryTermHasEnglishAndChineseText()
        {
            foreach (UiTerm term in Enum.GetValues(typeof(UiTerm)))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(UiText.Get(term, UiLanguage.English)), term.ToString());
                Assert.IsFalse(string.IsNullOrWhiteSpace(UiText.Get(term, UiLanguage.Chinese)), term.ToString());
            }
            Assert.AreEqual("Undo", UiText.Get(UiTerm.Undo, UiLanguage.English));
            Assert.AreEqual("撤销", UiText.Get(UiTerm.Undo, UiLanguage.Chinese));
        }

        [Test]
        public void LanguageButtonNamesTheOtherLanguageInThatLanguage()
        {
            Assert.AreEqual("中文", UiText.Get(UiTerm.SwitchLanguage, UiLanguage.English));
            Assert.AreEqual("English", UiText.Get(UiTerm.SwitchLanguage, UiLanguage.Chinese));
            Assert.AreEqual(UiLanguage.Chinese, UiText.Other(UiLanguage.English));
            Assert.AreEqual(UiLanguage.English, UiText.Other(UiLanguage.Chinese));
        }

        [Test]
        public void TilesShowNotesOrJianpuAndUduSounds()
        {
            string Tiles(InstrumentType instrument, UiLanguage language) =>
                string.Join(" ", Enumerable.Range(0, NoteMapping.Count).Select(i => UiText.Tile(new MusicalNote(instrument, i), language)));
            Assert.AreEqual("D E F# A B", Tiles(InstrumentType.Mbira, UiLanguage.English));
            Assert.AreEqual("1 2 3 5 6", Tiles(InstrumentType.Erhu, UiLanguage.Chinese), "jianpu with 1 = D");
            Assert.AreEqual("Open Muted Slap Bend Tap", Tiles(InstrumentType.Udu, UiLanguage.English));
            Assert.AreEqual(5, Tiles(InstrumentType.Udu, UiLanguage.Chinese).Split(' ').Distinct().Count(), "five different udu words");
        }

        [Test]
        public void VolumeLabelsInBothLanguages()
        {
            Assert.AreEqual("Vol 60%", UiText.Volume(1, UiLanguage.English));
            Assert.AreEqual("音量 60%", UiText.Volume(1, UiLanguage.Chinese));
            Assert.AreEqual("静音", UiText.Volume(3, UiLanguage.Chinese));
            Assert.AreEqual(MixLevels.Label(2), UiText.Volume(2, UiLanguage.English), "English matches the log wording");
        }

        [Test]
        public void SystemLanguageChoosesChineseOnlyForChinese()
        {
            Assert.AreEqual(UiLanguage.Chinese, Localization.FromSystem(SystemLanguage.ChineseSimplified));
            Assert.AreEqual(UiLanguage.Chinese, Localization.FromSystem(SystemLanguage.ChineseTraditional));
            Assert.AreEqual(UiLanguage.Chinese, Localization.FromSystem(SystemLanguage.Chinese));
            Assert.AreEqual(UiLanguage.English, Localization.FromSystem(SystemLanguage.English));
            Assert.AreEqual(UiLanguage.English, Localization.FromSystem(SystemLanguage.Japanese));
        }
    }

    /// <summary>The real scene: authored in English (nothing changes at startup) with every label able to switch.</summary>
    public class SceneLanguageTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";
        const string FontPath = "Assets/RoomOrchestra/Fonts/LittleEchoesLabels SDF.asset";

        [OneTimeSetUp]
        public void OpenScene() => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        [Test]
        public void LabelFontDrawsEveryCharacterInBothLanguagesFromAStaticAtlas()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Assert.IsNotNull(font, FontPath);
            Assert.AreEqual(AtlasPopulationMode.Static, font.atlasPopulationMode, "nothing rasterized on the headset");
            var missing = UiText.AllCharacters().Where(c => c != ' ' && !font.characterLookupTable.ContainsKey(c)).ToArray();
            Assert.IsEmpty(missing, $"missing: {new string(missing)}");
            foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                Assert.AreEqual(font, text.font, $"{text.transform.parent.name} uses the label font");
        }

        [Test]
        public void FixedLabelsAreAuthoredInEnglishAndCanSwitch()
        {
            var labels = UnityEngine.Object.FindObjectsByType<LocalizedLabel>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(labels.Length, 3 + 4 + 15 + 1, "names, Loudness/Undo/Clear/Recenter, 15 tiles, language");
            foreach (var label in labels)
            {
                Assert.IsNotNull(label.Text, label.name);
                Assert.AreEqual(label.TextFor(UiLanguage.English), label.Text.text, $"{label.name} authored in English");
                Assert.AreNotEqual(label.TextFor(UiLanguage.English), label.TextFor(UiLanguage.Chinese), $"{label.name} has Chinese");
            }
            var clear = UnityEngine.Object.FindObjectsByType<MusicControlButton>(FindObjectsSortMode.None).First(b => b.ButtonKind == MusicControlButton.Kind.Clear);
            Assert.IsNotNull(clear.GetComponentInChildren<LocalizedLabel>(), "Clear's label switches");
        }

        [Test]
        public void LanguageButtonLabeledReachableAndAtTheEndOfTheFrontRow()
        {
            var button = UnityEngine.Object.FindFirstObjectByType<LanguageButton>();
            Assert.IsNotNull(button, "language button in the scene");
            var p = button.transform.position;
            Assert.AreEqual(ReachResult.Ok, ReachEnvelope.Seated.Check(p.x, p.y, p.z), "reachable seated");
            Assert.IsNotNull(button.GetComponent<Collider>(), "tappable");
            Assert.IsTrue(button.transform.IsChildOf(UnityEngine.Object.FindFirstObjectByType<ControlsRig>().transform), "lifts with the controls");
            Assert.AreEqual("中文", button.GetComponentInChildren<TMP_Text>().text);
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<LanguageDirector>(), "applies the player's language after the start delay");
        }
    }
}
