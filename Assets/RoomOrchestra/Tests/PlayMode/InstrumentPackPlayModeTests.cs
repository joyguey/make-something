using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// On the REAL scene, after the lesson: Change moves instruments between furniture (labels and the song follow),
    /// Sounds switches the pack, and both are remembered. Editor simulation; nobody listened.
    /// </summary>
    public class InstrumentPackPlayModeTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";
        InstrumentPacks m_Packs;

        static void ClearPrefs()
        {
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
            PlayerPrefs.DeleteKey(InstrumentPacks.PackPrefKey);
            PlayerPrefs.DeleteKey(InstrumentPacks.PlacesPrefKey);
        }

        IEnumerator Load()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            m_Packs = InstrumentPacks.Instance;
            Object.FindFirstObjectByType<IntroSequence>().Finish(); // no lesson: free play
            yield return null;
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            ClearPrefs();
            MusicalHitPipeline.Reset();
            yield return Load();
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            var empty = SceneManager.CreateScene($"PacksEmpty{Time.frameCount}");
            SceneManager.SetActiveScene(empty);
            var real = SceneManager.GetSceneByPath(ScenePath);
            if (real.isLoaded) yield return SceneManager.UnloadSceneAsync(real);
            MusicalHitPipeline.Reset();
            ClearPrefs();
        }

        InstrumentTarget Furniture(string name) => m_Packs.Furniture.First(f => f.name == name);

        static ChangeInstrumentButton ChangeFor(InstrumentTarget f) =>
            Object.FindObjectsByType<ChangeInstrumentButton>(FindObjectsSortMode.None).First(c => c.Target == f);

        static PitchColumn ColumnFor(InstrumentTarget f) =>
            Object.FindObjectsByType<PitchColumn>(FindObjectsSortMode.None).First(c => c.Target == f);

        [UnityTest]
        public IEnumerator ChangeSwapsInstrumentsAndTheSongFollows()
        {
            var chair = Furniture("Chair");
            var lamp = Furniture("Lamp");
            var table = Furniture("Table");
            var selector = OrbSelector.Instance;
            selector.PlaceOnTile(ColumnFor(chair).Tiles[2]); // an mbira note on the chair
            yield return null;
            var echo = EchoConductor.Instance.Echoes.Single();
            Assert.AreEqual(InstrumentType.Mbira, echo.Target);

            Assert.IsTrue(InstrumentInputRouter.Activate(ChangeFor(chair).gameObject), "Change on the chair");
            yield return null;
            Assert.AreEqual(InstrumentType.Erhu, chair.Instrument, "the chair plays the next instrument");
            Assert.AreEqual(InstrumentType.Mbira, lamp.Instrument, "the lamp takes the chair's old one");
            Assert.AreSame(m_Packs.Current.For(InstrumentType.Erhu)[0], chair.Notes[0], "with its sound");
            Assert.AreSame(lamp, InstrumentTarget.Find(InstrumentType.Mbira), "the mbira's echo now plays from the lamp");
            StringAssert.StartsWith("erhu", ColumnFor(chair).GetComponentsInChildren<TMP_Text>().First(t => t.transform.parent.name == "InstrumentLabel").text);

            // then the V4 library (owner, 2026-11-11): five more, none on other furniture, and round to the drum
            foreach (var next in Instruments.Library)
            {
                Assert.IsTrue(InstrumentInputRouter.Activate(ChangeFor(chair).gameObject), $"Change to the {next}");
                yield return null;
                yield return new WaitForSeconds(0.2f); // past the tap debounce
                Assert.AreEqual(next, chair.Instrument);
                Assert.AreSame(m_Packs.Current.For(next)[0], chair.Notes[0], $"with the {next}'s own sound");
                StringAssert.StartsWith(UiText.Name(next, UiLanguage.English), ColumnFor(chair).GetComponentsInChildren<TMP_Text>().First(t => t.transform.parent.name == "InstrumentLabel").text);
            }
            Assert.AreEqual(InstrumentType.Mbira, lamp.Instrument, "the others keep theirs meanwhile");
            Assert.IsTrue(InstrumentInputRouter.Activate(ChangeFor(chair).gameObject), "Change again");
            yield return null;
            Assert.AreEqual(InstrumentType.Udu, chair.Instrument, "and then the drum");
            Assert.AreEqual(InstrumentType.SlitDrum, table.Instrument, "the table takes the chair's last one");
            var tileWords = ColumnFor(chair).GetComponentsInChildren<LocalizedLabel>().Where(l => l.name.StartsWith("TileLabel")).Select(l => l.Text.text).ToArray();
            CollectionAssert.AreEquivalent(Enumerable.Range(0, NoteMapping.Count).Select(i => UiText.Tile(new MusicalNote(InstrumentType.Udu, i), UiLanguage.English)), tileWords, "the tiles name drum sounds");

            Assert.AreEqual("SlitDrum,Udu,Mbira", PlayerPrefs.GetString(InstrumentPacks.PlacesPrefKey), "remembered left to right");
        }

        [UnityTest]
        public IEnumerator SoundsSwitchesThePackAndItIsRemembered()
        {
            var button = Object.FindFirstObjectByType<SoundPackButton>();
            Assert.AreEqual("Sounds\nClassic", button.Label.text);
            Assert.IsTrue(InstrumentInputRouter.Activate(button.GetComponentInChildren<Collider>().gameObject));
            yield return null;
            Assert.AreEqual(1, m_Packs.PackIndex);
            Assert.AreEqual("Sounds\nEarthy", button.Label.text);
            foreach (var f in m_Packs.Furniture)
                Assert.AreSame(m_Packs.Packs[1].For(f.Instrument)[0], f.Notes[0], $"{f.name} plays the Earthy set");

            // The next launch (after the lesson) keeps the pack and the places.
            PlayerPrefs.SetInt(TutorialDirector.DonePrefKey, 1);
            m_Packs.ChangeInstrument(m_Packs.Furniture[0]);
            yield return Load();
            Assert.AreEqual(1, m_Packs.PackIndex, "the pack is remembered");
            Assert.AreEqual(InstrumentType.Mbira, m_Packs.Furniture[0].Instrument, "and where each instrument is");
            Assert.AreSame(m_Packs.Packs[1].For(InstrumentType.Mbira)[0], m_Packs.Furniture[0].Notes[0]);
        }

        [UnityTest]
        public IEnumerator InstrumentsStayPutDuringTheLesson()
        {
            var tutorial = TutorialDirector.Instance;
            tutorial.Replay();
            while (!tutorial.Active) yield return null;
            Assert.IsFalse(ChangeFor(Furniture("Chair")).Press(), "the lesson is about the first arrangement");
            Assert.AreEqual(InstrumentType.Mbira, Furniture("Chair").Instrument);
        }
    }
}
