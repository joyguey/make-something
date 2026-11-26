using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// Prompt 05 walkthrough of A11Y-01..09 on the REAL scene in the Editor: one simulated controller
    /// (stick/buttons through ControllerButtonInput) plus pointer taps cast from a seated hand, the way
    /// PICO Spatial picks taps. No throwing, no hand tracking. Editor simulation, not headset evidence.
    /// </summary>
    public class A11yWalkthroughPlayModeTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";

        class FakeControllerSource : IControllerSource
        {
            public readonly ControllerFrame[] Hands = new ControllerFrame[2];
            public ControllerFrame Read(int hand) => Hands[hand];
        }

        FakeControllerSource m_Fake;
        EchoConductor m_Conductor;

        static Vector3 SeatedHand(int hand) => new Vector3(hand == 0 ? -0.2f : 0.2f, 0.85f, 0.3f);
        static Vector3 StandingHand(int hand) => new Vector3(hand == 0 ? -0.2f : 0.2f, 1.15f, 0.3f);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            ComfortSettings.SetPosture(PlayPosture.Seated);
            MusicalHitPipeline.Reset();
            // The PICO SDK's scene components ask Android for currentActivity, which throws in the Editor.
            // Tolerated only while the scene loads; the walkthrough itself runs with strict logs.
            LogAssert.ignoreFailingMessages = true;
            UnhookPlayToDeviceSceneDialog();
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            m_Conductor = EchoConductor.Instance;
            Assert.IsNotNull(m_Conductor, "real scene loaded");
            Object.FindFirstObjectByType<IntroSequence>()?.Finish(); // skip the opening: the walkthrough is about play
            m_Fake = new FakeControllerSource();
            Object.FindFirstObjectByType<ControllerButtonInput>().Source = m_Fake;
            yield return null;
        }

        // The SpatialAdapter Play-to-Device check opens a modal dialog (and offers to change a project
        // setting) when play mode loads a scene that was not open in the Editor. Tests load the real
        // scene on purpose, so drop that editor-only hook for the test session instead.
        static void UnhookPlayToDeviceSceneDialog()
        {
#if UNITY_EDITOR
            var type = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("ByteDance.PICO.SpatialAdapter.Exporter.Editor.PlayToDeviceValidator"))
                .FirstOrDefault(t => t != null);
            var check = type?.GetMethod("CheckValidSceneLoaded");
            if (check != null)
                SceneManager.sceneLoaded -= (UnityEngine.Events.UnityAction<Scene, LoadSceneMode>)
                    System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction<Scene, LoadSceneMode>), check);
#endif
        }

        // Unload the real scene so its conductor, targets and controller input can't leak into later tests.
        [UnityTearDown]
        public IEnumerator Unload()
        {
            ComfortSettings.SetPosture(PlayPosture.Seated);
            var empty = SceneManager.CreateScene($"A11yEmpty{Time.frameCount}");
            SceneManager.SetActiveScene(empty);
            var real = SceneManager.GetSceneByPath(ScenePath);
            if (real.isLoaded) yield return SceneManager.UnloadSceneAsync(real);
            MusicalHitPipeline.Reset();
        }

        // One discrete press on one hand: down for a frame, then released.
        IEnumerator Press(int hand, ControllerFrame pressed)
        {
            pressed.Connected = true;
            m_Fake.Hands[hand] = pressed;
            yield return null;
            m_Fake.Hands[hand] = new ControllerFrame { Connected = true };
            yield return null;
        }

        IEnumerator Connect(int hand)
        {
            m_Fake.Hands[hand] = new ControllerFrame { Connected = true };
            yield return null;
        }

        // A pointer tap: the first collider on the ray from the hand must belong to the control.
        static void Tap(Vector3 origin, Component control)
        {
            Physics.SyncTransforms();
            var aim = control.GetComponentInChildren<Collider>().bounds.center - origin;
            var hits = Physics.RaycastAll(origin, aim.normalized, aim.magnitude + 0.2f).OrderBy(h => h.distance).ToArray();
            Assert.IsNotEmpty(hits, $"ray from {origin} to {control.name} hits nothing");
            Assert.IsTrue(hits[0].collider.transform.IsChildOf(control.transform),
                $"tap on {control.name} from {origin} lands on {hits[0].collider.name}");
            Assert.IsTrue(InstrumentInputRouter.Activate(hits[0].collider.gameObject), $"{control.name} did something");
        }

        static T Find<T>(System.Func<T, bool> where = null) where T : Object =>
            Object.FindObjectsByType<T>(FindObjectsSortMode.None).First(t => where == null || where(t));

        static MusicControlButton Music(MusicControlButton.Kind kind) => Find<MusicControlButton>(b => b.ButtonKind == kind);

        int Count(InstrumentType target, OrbType orb) => m_Conductor.Sequencer.Echoes.Count(e => e.Target == target && e.Orb == orb);

        static readonly WaitForSeconds NextStep = new(0.4f); // > one 16th at 88 BPM, so places land on new steps

        // ---- A11Y-01 + A11Y-06 + A11Y-08 (handedness): controller only, one hand, seated, no throw ----
        [UnityTest]
        public IEnumerator A11y01_ControllerOnlyOneHandSeatedLoopEditUndoClear([Values(0, 1)] int hand)
        {
            yield return Connect(hand);
            var selector = OrbSelector.Instance;
            Assert.AreEqual(OrbType.Flutter, selector.SelectedOrb);
            Assert.AreEqual(Instruments.Chair, selector.FocusedTarget.Instrument, "starts on the chair");

            // Two Flutter on the lamp (two pitches)
            yield return Press(hand, new ControllerFrame { StickY = 1f });            // chair -> lamp
            yield return Press(hand, new ControllerFrame { Primary = true });          // place
            yield return NextStep;
            yield return Press(hand, new ControllerFrame { StickClick = true });      // next petal
            yield return Press(hand, new ControllerFrame { Primary = true });
            yield return NextStep;
            // One Bop on the chair
            yield return Press(hand, new ControllerFrame { StickX = 1f });            // Flutter -> Bop
            yield return Press(hand, new ControllerFrame { StickY = -1f });           // lamp -> chair
            yield return Press(hand, new ControllerFrame { Primary = true });
            yield return NextStep;
            // One Boom on the table
            yield return Press(hand, new ControllerFrame { StickX = 1f });            // Bop -> Boom
            yield return Press(hand, new ControllerFrame { StickY = -1f });           // chair -> table
            yield return Press(hand, new ControllerFrame { Primary = true });

            Assert.AreEqual(2, Count(InstrumentType.Erhu, OrbType.Flutter), "two Flutter on the lamp");
            Assert.AreEqual(1, Count(Instruments.Chair, OrbType.Bop), "one Bop on the chair");
            Assert.AreEqual(1, Count(InstrumentType.Udu, OrbType.Boom), "one Boom on the table");
            Assert.AreEqual(2, m_Conductor.Sequencer.Echoes.Where(e => e.Target == InstrumentType.Erhu).Select(e => e.NoteIndex).Distinct().Count(),
                "the lamp's two notes differ in pitch");

            // Edit timing: grip -> edit mode, stick right nudges the selected echo one step, B/Y undoes
            yield return Press(hand, new ControllerFrame { Grip = true });
            Assert.IsTrue(m_Conductor.EditMode);
            var selected = m_Conductor.Sequencer.Find(m_Conductor.SelectedEchoId);
            Assert.IsNotNull(selected, "an echo is selected on entering edit mode");
            int before = selected.Step;
            yield return Press(hand, new ControllerFrame { StickX = 1f });
            Assert.AreEqual((before + 1) % 16, m_Conductor.Sequencer.Find(selected.StableId).Step, "nudged one step later");
            yield return Press(hand, new ControllerFrame { Secondary = true });       // undo the nudge
            Assert.AreEqual(before, m_Conductor.Sequencer.Find(selected.StableId).Step, "undo restores the timing");
            yield return Press(hand, new ControllerFrame { Grip = true });
            Assert.IsFalse(m_Conductor.EditMode);

            // Clear with the same controller's pointer (trigger tap), then Undo brings the loop back
            Tap(SeatedHand(hand), Music(MusicControlButton.Kind.Clear)); // asks "Sure?" (George, 2026-11-03)...
            Assert.AreEqual(4, m_Conductor.Sequencer.Echoes.Count, "the first tap only asks");
            Tap(SeatedHand(hand), Music(MusicControlButton.Kind.Clear)); // ...the same button again clears
            Assert.AreEqual(0, m_Conductor.Sequencer.Echoes.Count, "clear empties every orbit");
            Tap(SeatedHand(hand), Find<UndoButton>());
            Assert.AreEqual(4, m_Conductor.Sequencer.Echoes.Count, "undo after clear restores the loop");
        }

        // ---- A11Y-01 by pointer taps only (no stick), and A11Y-08 standing ----
        [UnityTest]
        public IEnumerator A11y01_PointerTapsOnlyBuildTheSameLoop([Values(PlayPosture.Seated, PlayPosture.Standing)] PlayPosture posture)
        {
            const int hand = 1;
            var origin = posture == PlayPosture.Seated ? SeatedHand(hand) : StandingHand(hand);
            if (posture == PlayPosture.Standing)
            {
                // Height waits on the leaf's menu (owner, 2026-11-08): open it, press Height, close it
                Tap(SeatedHand(hand), Find<MenuButton>(b => b.name == "MenuButton"));
                yield return new WaitForSeconds(MenuTray.OpenSeconds + 0.05f);
                Tap(SeatedHand(hand), Find<HeightButton>()); // reached while still seated
                Tap(SeatedHand(hand), Find<MenuButton>(b => b.name == "MenuCloseButton")); // Close, where Menu was
                yield return new WaitForSeconds(MenuTray.OpenSeconds + 0.05f);
            }
            Assert.AreEqual(posture, ComfortSettings.Posture);

            PitchTile Tile(InstrumentType type, int petal) => Find<PitchTile>(t => t.Target.Instrument == type && t.Petal == petal);
            OrbToken Token(OrbType orb) => Find<OrbToken>(t => t.Orb == orb);

            Tap(origin, Token(OrbType.Flutter));
            Tap(origin, Tile(InstrumentType.Erhu, 1));
            yield return NextStep;
            Tap(origin, Tile(InstrumentType.Erhu, 3));
            yield return NextStep;
            Tap(origin, Token(OrbType.Bop));
            Tap(origin, Tile(Instruments.Chair, 2));
            yield return NextStep;
            Tap(origin, Token(OrbType.Boom));
            Tap(origin, Tile(InstrumentType.Udu, 0));

            Assert.AreEqual(2, Count(InstrumentType.Erhu, OrbType.Flutter));
            Assert.AreEqual(1, Count(Instruments.Chair, OrbType.Bop));
            Assert.AreEqual(1, Count(InstrumentType.Udu, OrbType.Boom));
            var lamp = m_Conductor.Sequencer.Echoes.Where(e => e.Target == InstrumentType.Erhu).Select(e => e.NoteIndex).OrderBy(n => n);
            CollectionAssert.AreEqual(new[] { 1, 3 }, lamp, "the tapped tile sets the pitch");
            Tap(origin, Music(MusicControlButton.Kind.Clear)); // asks "Sure?" first (George, 2026-11-03)
            Tap(origin, Music(MusicControlButton.Kind.Clear));
            Assert.AreEqual(0, m_Conductor.Sequencer.Echoes.Count);
        }

        // ---- A11Y-03: everything muted, the echoes stay visible ----
        [UnityTest]
        public IEnumerator A11y03_MutedMixStillShowsBeatAndEchoes()
        {
            var hand = SeatedHand(1);
            Tap(hand, Find<PitchTile>(t => t.Target.Instrument == Instruments.Chair && t.Petal == 4));
            foreach (var mix in Object.FindObjectsByType<MixButton>(FindObjectsSortMode.None))
            {
                for (int i = 0; i < 3; i++) Tap(hand, mix);
                Assert.IsTrue(mix.Label.text.EndsWith("Muted"), mix.Label.text);
                Assert.AreEqual(0f, mix.Target.MixGain);
            }
            var orbit = m_Conductor.Orbits.First(o => o.Target.Instrument == Instruments.Chair);
            var mini = orbit.GetComponentsInChildren<EchoMini>().FirstOrDefault(m => orbit.EchoFor(m.transform) != 0);
            yield return null;
            mini = orbit.GetComponentsInChildren<EchoMini>().FirstOrDefault(m => orbit.EchoFor(m.transform) != 0);
            Assert.IsNotNull(mini, "the muted echo still shows on its orbit");
            var start = mini.transform.position;
            yield return new WaitForSeconds(0.5f);
            Assert.Greater((mini.transform.position - start).magnitude, 0.01f, "it still travels with the beat while muted");
        }

        // ---- A11Y-05: controller lost mid-edit, the other one carries on (no softlock) ----
        [UnityTest]
        public IEnumerator A11y05_ControllerLossMidEditRecoversWithTheOtherHand()
        {
            yield return Connect(1);
            yield return Press(1, new ControllerFrame { Primary = true });
            m_Fake.Hands[1] = new ControllerFrame { Connected = true, Grip = true }; // grip down: edit mode on
            yield return null;
            Assert.IsTrue(m_Conductor.EditMode);
            m_Fake.Hands[1] = default; // lost while the grip is still held
            yield return null;
            yield return Connect(0);
            yield return Press(0, new ControllerFrame { Grip = true });
            Assert.IsFalse(m_Conductor.EditMode, "the left hand leaves edit mode");
            yield return Press(0, new ControllerFrame { Primary = true });
            Assert.AreEqual(2, m_Conductor.Sequencer.Echoes.Count, "and keeps playing");
            Tap(SeatedHand(0), Music(MusicControlButton.Kind.Pause));
            Assert.IsTrue(m_Conductor.IsPaused, "pointer controls still answer");
        }

        // ---- A11Y-07: reset the view from the seat (no walking to the furniture) ----
        [UnityTest]
        public IEnumerator A11y07_MenuButtonRecentersTheStageInFrontOfTheSeat()
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) cam.tag = "Untagged";
            var head = new GameObject("Head").AddComponent<Camera>();
            head.tag = "MainCamera";
            head.transform.SetPositionAndRotation(new Vector3(1f, 1.15f, -0.5f), Quaternion.Euler(0f, 90f, 0f)); // seat turned to the right
            try
            {
                yield return Connect(0);
                yield return Press(0, new ControllerFrame { Menu = true });
                var stage = StageAnchor.Instance.transform;
                Assert.Less((stage.position - new Vector3(1f, stage.position.y, -0.5f)).magnitude, 1e-3f, "stage start point under the seat");
                Assert.AreEqual(90f, stage.eulerAngles.y, 0.1f, "stage turned to face the player");
                var chair = InstrumentTarget.Find(Instruments.Chair);
                var local = head.transform.InverseTransformPoint(chair.AimPosition);
                var p = new Vector3(local.x, chair.AimPosition.y, local.z);
                Assert.AreEqual(ReachResult.Ok, ReachEnvelope.Seated.Check(p.x, p.y, p.z), "the chair is in seated reach again");
            }
            finally
            {
                Object.Destroy(head.gameObject);
            }
        }

        // ---- A11Y-09: pause at any time (even mid-edit), and the mixer manages every voice ----
        [UnityTest]
        public IEnumerator A11y09_PauseAnyTimeSilencesAndResumes()
        {
            var hand = SeatedHand(1);
            Tap(hand, Find<PitchTile>(t => t.Target.Instrument == InstrumentType.Erhu && t.Petal == 2));
            yield return Connect(1);
            yield return Press(1, new ControllerFrame { Grip = true }); // mid-edit
            Tap(hand, Music(MusicControlButton.Kind.Pause));
            Assert.IsTrue(m_Conductor.IsPaused);
            Assert.AreEqual("Play", Music(MusicControlButton.Kind.Pause).Label.text);
            foreach (var target in InstrumentTarget.Active)
            {
                Assert.IsFalse(target.IsSounding, $"{target.name} silent at once");
                Assert.AreEqual(0, target.ScheduledCount, $"{target.name} nothing queued");
            }
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(InstrumentTarget.Active.All(t => !t.IsSounding), "stays silent");
            Tap(hand, Music(MusicControlButton.Kind.Pause));
            Assert.IsFalse(m_Conductor.IsPaused);
            Assert.AreEqual(1, m_Conductor.Sequencer.Echoes.Count, "the loop survives pause");
        }

        // ---- Prompt 06: faces react to the music on the real scene ----
        [UnityTest]
        public IEnumerator CharactersReactCuriousThenSiblingsGlanceAtEachOther()
        {
            var hand = SeatedHand(1);
            PitchTile Tile(int petal) => Find<PitchTile>(t => t.Target.Instrument == InstrumentType.Erhu && t.Petal == petal);
            var orbit = m_Conductor.Orbits.First(o => o.Target.Instrument == InstrumentType.Erhu);
            var token = Find<OrbToken>(t => t.Orb == OrbType.Flutter).GetComponent<CharacterFace>();

            Tap(hand, Tile(1));
            Assert.AreEqual(Expression.Curious, token.Current, "the tray Flutter reacts to its own note");
            yield return null;
            yield return null;
            var first = m_Conductor.Sequencer.Echoes[0];
            var firstFace = orbit.MiniFor(first.StableId).GetComponent<CharacterFace>();
            Assert.AreEqual(Expression.Curious, firstFace.Current, "first Flutter on the lamp is curious");

            yield return NextStep;
            Tap(hand, Tile(3));
            yield return null;
            yield return null;
            var second = m_Conductor.Sequencer.Echoes.First(e => e.StableId != first.StableId);
            var secondFace = orbit.MiniFor(second.StableId).GetComponent<CharacterFace>();
            Assert.AreEqual(Expression.Sibling, secondFace.Current);
            Assert.AreEqual(Expression.Sibling, firstFace.Current, "the sibling glances back");
            float a = firstFace.EyeL.localPosition.x, b = secondFace.EyeL.localPosition.x;
            Assert.AreNotEqual(System.Math.Sign(a - firstFace.Smile.localPosition.x), System.Math.Sign(b - secondFace.Smile.localPosition.x),
                "they look in opposite directions, toward each other");
        }
    }
}
