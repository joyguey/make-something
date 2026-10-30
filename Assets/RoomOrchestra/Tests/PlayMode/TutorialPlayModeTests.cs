using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// The guided first song on the REAL scene in the Editor: the chair alone, Flutter's line and the glowing
    /// tile, right notes snapping into the bar, wrong ones taken back, the lamp and table appearing in a poof,
    /// Undo stepping back, Skip. Notes go in by tile taps and by the controller's place, as a player would.
    /// Editor simulation, not headset evidence.
    /// </summary>
    public class TutorialPlayModeTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";

        TutorialDirector m_Tutorial;
        EchoConductor m_Conductor;
        OrbSelector m_Selector;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            ComfortSettings.SetPosture(PlayPosture.Seated);
            MusicalHitPipeline.Reset();
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey); // a first launch on this device
            PlayerPrefs.DeleteKey(InstrumentPacks.PackPrefKey);
            PlayerPrefs.DeleteKey(InstrumentPacks.PlacesPrefKey);
            LogAssert.ignoreFailingMessages = true; // the PICO SDK's scene components throw in the Editor while loading
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            m_Conductor = EchoConductor.Instance;
            m_Selector = OrbSelector.Instance;
            m_Tutorial = TutorialDirector.Instance;
            Assert.IsNotNull(m_Tutorial, "the scene has the guided song");
            Object.FindFirstObjectByType<IntroSequence>().Finish(skipTutorial: false); // the opening done; the lesson to come
            while (!m_Tutorial.Active) yield return null;
            yield return new WaitForSeconds(TutorialDirector.PopSeconds + 0.05f);
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            var empty = SceneManager.CreateScene($"TutorialEmpty{Time.frameCount}");
            SceneManager.SetActiveScene(empty);
            var real = SceneManager.GetSceneByPath(ScenePath);
            if (real.isLoaded) yield return SceneManager.UnloadSceneAsync(real);
            MusicalHitPipeline.Reset();
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
        }

        static StationReveal Station(InstrumentType type) =>
            Object.FindObjectsByType<StationReveal>(FindObjectsSortMode.None).First(s => s.Instrument == type);

        static PitchTile Tile(InstrumentType type, int note) =>
            Object.FindObjectsByType<PitchTile>(FindObjectsSortMode.None).First(t => t.Target.Instrument == type && t.Petal == note);

        // A tap on a tile, then a short wait (taps on one tile within the debounce count once).
        IEnumerator Play(InstrumentType type, int note)
        {
            Assert.IsTrue(InstrumentInputRouter.Activate(Tile(type, note).gameObject), $"{type} tile {note}");
            yield return new WaitForSeconds(0.3f);
        }

        int Count(InstrumentType type) => m_Conductor.Echoes.Count(e => e.Target == type);

        [UnityTest]
        public IEnumerator FlutterAsksForTheFirstNoteOnTheChairAlone()
        {
            Assert.IsTrue(VirtualRoom.Instance.Standing, "the lesson happens in the virtual room");
            Assert.AreEqual(UiTerm.TutorialHello, m_Tutorial.Line);
            Assert.IsTrue(m_Tutorial.BubbleOut);
            Assert.IsTrue(Station(InstrumentType.Udu).Hidden, "the table waits in the cloud");
            Assert.IsTrue(Station(InstrumentType.Erhu).Hidden, "the lamp waits in the cloud");
            Assert.IsTrue(Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None).First(t => t.Instrument == InstrumentType.Udu)
                .GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "nothing hits or taps a hidden table");
            var tile = Tile(InstrumentType.Mbira, 2).transform;
            Assert.AreSame(tile, m_Tutorial.WantedTile(), "the chair's F# glows");
            Assert.Less(Vector3.Distance(m_Tutorial.Glow.position, tile.position), 0.05f, "the glow sits right behind it");
            Assert.Greater(Vector3.Dot(m_Tutorial.Glow.position - tile.position, tile.forward), 0f, "behind, not in front");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RightNotesSnapIntoTheTuneAndWrongOnesAreTakenBack()
        {
            yield return Play(InstrumentType.Mbira, 2);
            Assert.AreEqual(1, m_Tutorial.Plan.Index, "F# done");
            Assert.AreEqual(0, m_Conductor.Echoes.Single().Step, "on the bar's first step, whatever the timing");
            Assert.AreEqual(UiTerm.TutorialMa, m_Tutorial.Line);

            yield return Play(InstrumentType.Mbira, 4); // B, not the E asked for
            Assert.AreEqual(1, Count(InstrumentType.Mbira), "the wrong note is taken back out of the song");
            Assert.AreEqual(UiTerm.TutorialTryAgain, m_Tutorial.Line);
            Assert.AreEqual(1, m_Tutorial.Plan.Index);

            yield return Play(InstrumentType.Mbira, 1);
            Assert.AreEqual(2, m_Tutorial.Plan.Index, "E done");
            Assert.AreEqual(UiTerm.TutorialMary, m_Tutorial.Line, "the right note replaces the try-again line");
            Assert.IsTrue(m_Conductor.Echoes.Any(e => e.Step == 2 && e.NoteIndex == 1), "E snapped to step 2");

            Assert.IsTrue(m_Conductor.Undo(), "one Undo");
            yield return null;
            Assert.AreEqual(1, m_Tutorial.Plan.Index, "Undo takes the whole note back and the lesson steps back");
            Assert.AreEqual(UiTerm.TutorialMa, m_Tutorial.Line);
        }

        [UnityTest]
        public IEnumerator TheLampAndThenTheTableAppearInAPoofWhenTheSongNeedsThem()
        {
            foreach (var note in new[] { 2, 1, 0 }) yield return Play(InstrumentType.Mbira, note);
            Assert.IsTrue(Station(InstrumentType.Erhu).Hidden, "not yet");
            yield return Play(InstrumentType.Mbira, 1);
            Assert.AreEqual(UiTerm.TutorialLamp, m_Tutorial.Line);
            var lamp = Station(InstrumentType.Erhu);
            Assert.IsFalse(lamp.Hidden, "the lamp is out of the cloud");
            Assert.IsTrue(Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None).First(t => t.Instrument == InstrumentType.Erhu)
                .GetComponentsInChildren<Collider>().Any(c => c.enabled), "and can be hit again");
            Assert.AreEqual(1.1f, lamp.Parts[0].position.x, 0.01f, "home beside the chair");
            Assert.AreEqual(InstrumentType.Erhu, m_Selector.FocusedTarget.Instrument, "the controller's focus moves to it");
            Assert.IsTrue(Station(InstrumentType.Udu).Hidden, "the table still waits");

            // The controller's select-and-place counts the same as a tap.
            m_Selector.SetPetal(2);
            m_Selector.PlaceOnFocused();
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(5, m_Tutorial.Plan.Index, "a controller place is a note like any other");
            yield return Play(InstrumentType.Erhu, 2);
            Assert.AreEqual(UiTerm.TutorialDrum, m_Tutorial.Line);
            yield return new WaitForSeconds(StationReveal.AppearAfterSeconds + 0.05f);
            Assert.IsFalse(Station(InstrumentType.Udu).Hidden, "the table appears for the beat");

            foreach (var hit in new[] { 0, 3, 1, 2 }) yield return Play(InstrumentType.Udu, hit); // any udu sound keeps the beat
            Assert.AreEqual(UiTerm.TutorialOnGo, m_Tutorial.Line, "now the last note, in time");
            Assert.IsTrue(TutorialPlan.Song[m_Tutorial.Plan.Index].OnTime);
        }

        // Plays the guided steps up to the timed last note (tile taps, snapped into place).
        IEnumerator ReachTheCount()
        {
            for (int i = 0; i < TutorialPlan.Song.Length - 1; i++)
            {
                var step = TutorialPlan.Song[i];
                yield return Play(step.Instrument, step.Note == TutorialPlan.AnyNote ? 2 : step.Note);
            }
            Assert.IsTrue(m_Tutorial.Plan.Current.OnTime);
        }

        double Phase => m_Conductor.Clock.BarPhase(AudioSettings.dspTime);

        // Waits for a moment of the bar (0..1), measured on the audio clock.
        IEnumerator WaitForPhase(double from, double to)
        {
            float end = Time.realtimeSinceStartup + 10f;
            while (!(Phase >= from && Phase < to) && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(Phase >= from && Phase < to, $"reached bar phase {from}-{to}");
        }

        [UnityTest]
        public IEnumerator TheCountShowsThreeTwoOneGoOnTheBeatsInItsColours()
        {
            yield return ReachTheCount();
            var park = GameObject.Find("RoomOrchestra").transform.Find("BallDispenser/ParkPoint");
            for (int beat = 0; beat < 4; beat++)
            {
                yield return WaitForPhase(beat * 0.25 + 0.05, beat * 0.25 + 0.2);
                yield return null;
                Assert.AreEqual(beat, m_Tutorial.CountdownBeat);
                var shown = m_Tutorial.ShownCountPiece;
                Assert.AreSame(m_Tutorial.CountPieces[beat], shown, $"beat {beat + 1} shows {new[] { "3", "2", "1", "GO!" }[beat]}");
                Assert.Greater(Vector3.Distance(shown.position, park.position), 0.5f, "out over the stage");
                Assert.Greater(shown.localScale.x, 0.99f, "full size");
                var face = shown.Find("Solid").GetComponent<MeshRenderer>().sharedMaterials[0];
                Assert.Less(Vector4.Distance(TutorialDirector.CountColours[beat], face.color), 1e-3f, "its colour");
                Assert.AreEqual(2, shown.Find("Solid").GetComponent<MeshFilter>().sharedMesh.subMeshCount, "a 3D solid: face and sides");
                foreach (var other in m_Tutorial.CountPieces)
                    if (other != shown) Assert.Less(Vector3.Distance(other.position, park.position), 1e-3f, $"{other.name} waits in the cloud");
            }

            m_Conductor.TogglePause();
            yield return null;
            Assert.AreEqual(-1, m_Tutorial.CountdownBeat, "no count while the music is paused");
            m_Conductor.TogglePause();
        }

        [UnityTest]
        public IEnumerator TheLastNoteLandsOnGoToFinishTheTune()
        {
            yield return ReachTheCount();
            yield return WaitForPhase(0.76, 0.79); // just after Go (step 12)
            Assert.IsTrue(InstrumentInputRouter.Activate(Tile(InstrumentType.Erhu, 2).gameObject));
            yield return null;
            yield return null;
            Assert.IsTrue(m_Tutorial.Plan.Done);
            Assert.IsFalse(m_Tutorial.Plan.Skipped);
            Assert.AreEqual(UiTerm.TutorialDone, m_Tutorial.Line);
            Assert.AreEqual(TutorialPlan.Song.Length, m_Conductor.Echoes.Count, "the whole tune, one bar");
            CollectionAssert.AreEquivalent(new[] { 8, 10, 12 }, m_Conductor.Echoes.Where(e => e.Target == InstrumentType.Erhu).Select(e => e.Step));
            CollectionAssert.AreEquivalent(new[] { 0, 4, 8, 12 }, m_Conductor.Echoes.Where(e => e.Target == InstrumentType.Udu).Select(e => e.Step));
            Assert.AreEqual(-1, m_Tutorial.CountdownBeat, "the count is put away");
            Assert.AreEqual(TutorialDirector.CheerHops - 1, m_Tutorial.CheerHopsLeft, "the friends start cheering: the lesson is clearly over");
            Assert.IsFalse(m_Tutorial.GlowOut, "nothing glows once the song is made");

            var room = VirtualRoom.Instance;
            Assert.IsTrue(room.TearingDown, "the virtual room comes apart: the lesson is over");
            yield return new WaitForSeconds(room.TearDownSeconds + 0.2f);
            Assert.IsTrue(room.Gone, "and the player's own room is the stage");
            var park = GameObject.Find("RoomOrchestra").transform.Find("BallDispenser/ParkPoint");
            Assert.IsTrue(room.Panels.All(p => Vector3.Distance(p.position, park.position) < 1e-3f), "every panel waits in the cloud");
        }

        [UnityTest]
        public IEnumerator EarlyNotesAreTakenBackThenCountAnywhereAfterTwoTries()
        {
            yield return ReachTheCount();
            int before = m_Conductor.Echoes.Count;
            yield return WaitForPhase(0.26, 0.29); // beat 2: far too early for Go
            yield return Play(InstrumentType.Erhu, 2);
            Assert.AreEqual(before, m_Conductor.Echoes.Count, "taken back");
            Assert.AreEqual(UiTerm.TutorialEarly, m_Tutorial.Line);
            Assert.AreEqual(1, m_Tutorial.Misses);

            yield return WaitForPhase(0.26, 0.29);
            yield return Play(InstrumentType.Erhu, 2);
            Assert.AreEqual(2, m_Tutorial.Misses);

            yield return WaitForPhase(0.26, 0.29);
            yield return Play(InstrumentType.Erhu, 2);
            Assert.IsTrue(m_Tutorial.Plan.Done, "timing is taught, never required: the third try counts");
            Assert.AreEqual(12, m_Conductor.Echoes.Last(e => e.Target == InstrumentType.Erhu).Step, "and snaps onto Go");
        }

        [UnityTest]
        public IEnumerator SkipBringsEverythingOutAndTucksTheBubbleAway()
        {
            var skip = Object.FindFirstObjectByType<TutorialSkipButton>();
            // A pointer tap from a seated hand reaches the Skip leaf first.
            Physics.SyncTransforms();
            var origin = new Vector3(-0.2f, 0.85f, 0.3f);
            var aim = skip.GetComponentInChildren<Collider>().bounds.center - origin;
            var hit = Physics.RaycastAll(origin, aim.normalized, aim.magnitude + 0.2f).OrderBy(h => h.distance).First();
            Assert.IsTrue(hit.collider.transform.IsChildOf(skip.transform),
                $"the tap lands on {hit.collider.transform.parent.name}/{hit.collider.name} at {hit.point}, not the Skip leaf at {skip.transform.position}");
            Assert.IsTrue(InstrumentInputRouter.Activate(hit.collider.gameObject));

            Assert.IsTrue(m_Tutorial.Plan.Skipped);
            Assert.IsTrue(VirtualRoom.Instance.TearingDown, "Skip also brings the real room in");
            yield return new WaitForSeconds(Mathf.Max(StationReveal.AppearAfterSeconds, TutorialDirector.PopSeconds) + 0.1f);
            Assert.IsFalse(Station(InstrumentType.Udu).Hidden);
            Assert.IsFalse(Station(InstrumentType.Erhu).Hidden);
            var park = GameObject.Find("RoomOrchestra").transform.Find("BallDispenser/ParkPoint");
            Assert.Less(Vector3.Distance(m_Tutorial.Bubble.position, park.position), 1e-3f, "the bubble waits in the cloud");
            Assert.IsFalse(skip.GetComponentInChildren<Collider>(true).enabled, "the parked Skip leaf catches no taps");

            yield return Play(InstrumentType.Mbira, 4);
            Assert.AreEqual(1, m_Conductor.Echoes.Count, "free play: any note stays");
        }

        [UnityTest]
        public IEnumerator FinishingIsRememberedAndTheNextLaunchStartsInFreePlay()
        {
            Assert.IsFalse(TutorialDirector.RememberedDone);
            m_Tutorial.Skip();
            Assert.IsTrue(TutorialDirector.RememberedDone, "skipping (or finishing) is remembered on this device");

            // The next launch: same scene, the opening done.
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            var tutorial = TutorialDirector.Instance;
            Object.FindFirstObjectByType<IntroSequence>().Finish(skipTutorial: false);
            while (Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds + 0.1f) yield return null;
            yield return null;
            Assert.IsFalse(tutorial.Active, "no lesson the second time");
            Assert.IsFalse(tutorial.BubbleOut);
            Assert.IsTrue(VirtualRoom.Instance.TearingDown || VirtualRoom.Instance.Gone, "the real room straight away");
            yield return new WaitForSeconds(StationReveal.AppearAfterSeconds + 0.1f);
            Assert.IsFalse(Station(InstrumentType.Udu).Hidden, "every instrument is out");
            Assert.IsFalse(Station(InstrumentType.Erhu).Hidden);
        }

        [UnityTest]
        public IEnumerator TheLessonButtonPlaysTheSongAgainFromTheStart()
        {
            m_Tutorial.Skip();
            yield return new WaitForSeconds(StationReveal.AppearAfterSeconds + TutorialDirector.PopSeconds + 0.1f);
            yield return Play(InstrumentType.Erhu, 4); // free play
            Assert.AreEqual(1, m_Conductor.Echoes.Count);

            var lesson = Object.FindFirstObjectByType<TutorialReplayButton>();
            Assert.IsNotNull(lesson, "a Lesson button on the leaf");
            Assert.IsTrue(InstrumentInputRouter.Activate(lesson.GetComponentInChildren<Collider>().gameObject));
            Assert.AreEqual(0, m_Conductor.Echoes.Count, "a fresh song (Undo brings the old one back)");
            Assert.IsTrue(Station(InstrumentType.Erhu).Hidden, "the lamp waits in the cloud again");
            Assert.IsTrue(Station(InstrumentType.Udu).Hidden);
            yield return null;
            yield return new WaitForSeconds(TutorialDirector.PopSeconds + 0.05f);
            Assert.IsTrue(m_Tutorial.Active);
            Assert.AreEqual(0, m_Tutorial.Plan.Index);
            Assert.AreEqual(UiTerm.TutorialHello, m_Tutorial.Line);
            Assert.IsTrue(m_Tutorial.BubbleOut);
            Assert.AreEqual(InstrumentType.Mbira, m_Selector.FocusedTarget.Instrument, "the controller's focus back on the chair");
            yield return Play(InstrumentType.Mbira, 2);
            Assert.AreEqual(1, m_Tutorial.Plan.Index, "and it runs as before");
        }
    }
}
