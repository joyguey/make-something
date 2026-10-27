using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>English / Chinese switching on real components (no headset).</summary>
    public class LanguagePlayModeTests
    {
        GameObject m_Root;
        bool m_HadPref;
        int m_SavedPref;

        [SetUp]
        public void SetUp()
        {
            m_HadPref = PlayerPrefs.HasKey(Localization.PrefKey);
            m_SavedPref = PlayerPrefs.GetInt(Localization.PrefKey, 0);
            m_Root = new GameObject("LanguageTest");
        }

        [TearDown]
        public void TearDown()
        {
            Localization.Set(UiLanguage.English, remember: false);
            if (m_HadPref) PlayerPrefs.SetInt(Localization.PrefKey, m_SavedPref);
            else PlayerPrefs.DeleteKey(Localization.PrefKey);
            ComfortSettings.SetPosture(PlayPosture.Seated);
            Object.Destroy(m_Root);
        }

        TMP_Text Text(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(m_Root.transform, false);
            return go.AddComponent<TextMeshPro>();
        }

        [UnityTest]
        public IEnumerator LanguageButtonSwitchesEveryLabelAndBack()
        {
            var undoText = Text("Undo");
            undoText.text = "Undo";
            undoText.gameObject.AddComponent<LocalizedLabel>().Configure(undoText, UiTerm.Undo);
            var tileText = Text("Tile");
            tileText.text = "F#";
            tileText.gameObject.AddComponent<LocalizedLabel>().ConfigureTile(tileText, InstrumentType.Erhu, 2);

            var heightGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            heightGo.transform.SetParent(m_Root.transform, false);
            var heightText = Text("Height");
            var height = heightGo.AddComponent<HeightButton>();
            height.Configure(heightText);

            var target = new GameObject("Chair").AddComponent<InstrumentTarget>();
            target.transform.SetParent(m_Root.transform, false);
            var mixGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mixGo.transform.SetParent(m_Root.transform, false);
            var mixText = Text("Mix");
            var mix = mixGo.AddComponent<MixButton>();
            mix.Configure(target, mixText);

            var buttonGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            buttonGo.transform.SetParent(m_Root.transform, false);
            var buttonText = Text("Language");
            buttonText.text = "中文";
            buttonText.gameObject.AddComponent<LocalizedLabel>().Configure(buttonText, UiTerm.SwitchLanguage);
            buttonGo.AddComponent<LanguageButton>();
            var plate = new GameObject("KeyBacking").transform;
            plate.SetParent(m_Root.transform, false);
            var backing = new GameObject("KeyLabel").AddComponent<ChineseOnlyBacking>();
            backing.transform.SetParent(m_Root.transform, false);
            backing.Configure(plate, new Vector3(0f, 1.26f, 0f), new Vector3(0f, 1.15f, 0f));
            yield return null;
            Assert.IsFalse(backing.Shown, "English: the 1=D plate waits in the tile");

            Assert.AreEqual(UiLanguage.English, Localization.Current);
            Assert.IsTrue(InstrumentInputRouter.Activate(buttonGo), "a tap on the button switches");
            Assert.AreEqual(UiLanguage.Chinese, Localization.Current);
            Assert.AreEqual("撤销", undoText.text);
            Assert.AreEqual("3", tileText.text, "F# is 3 in jianpu (1 = D)");
            Assert.AreEqual("高度\n坐姿", heightText.text);
            Assert.AreEqual($"{UiText.Name(target.Instrument, UiLanguage.Chinese)}\n音量 100%", mixText.text);
            Assert.AreEqual("English", buttonText.text, "the button now offers English");
            Assert.IsTrue(backing.Shown, "Chinese: the plate sits behind 1=D");
            Assert.AreEqual((int)UiLanguage.Chinese, PlayerPrefs.GetInt(Localization.PrefKey, -1), "choice remembered");
            Assert.AreEqual(UiLanguage.Chinese, Localization.Preferred(), "the saved choice wins over the system language");

            height.Press();
            Assert.AreEqual("高度\n站姿", heightText.text, "state changes keep the language");

            yield return new WaitForSecondsRealtime(0.25f); // past the duplicate-tap debounce
            InstrumentInputRouter.Activate(buttonGo);
            Assert.AreEqual(UiLanguage.English, Localization.Current);
            Assert.AreEqual("Undo", undoText.text);
            Assert.AreEqual("F#", tileText.text);
            Assert.AreEqual("Height\nStanding", heightText.text);
            Assert.AreEqual("中文", buttonText.text);
            Assert.IsFalse(backing.Shown, "back in English: parked again");
        }

        [UnityTest]
        public IEnumerator DirectorAppliesTheSavedChoiceAfterTheStartDelay()
        {
            PlayerPrefs.SetInt(Localization.PrefKey, (int)UiLanguage.Chinese);
            var text = Text("Undo");
            text.text = "Undo";
            text.gameObject.AddComponent<LocalizedLabel>().Configure(text, UiTerm.Undo);
            var director = m_Root.AddComponent<LanguageDirector>();
            float wait = Mathf.Max(0f, JellyBody.StartDelaySeconds - Time.timeSinceLevelLoad) + 0.5f;
            yield return new WaitForSeconds(wait);
            Assert.AreEqual(UiLanguage.Chinese, Localization.Current);
            Assert.AreEqual("撤销", text.text);
            Assert.IsFalse(director.enabled, "applies once");
        }
    }
}
