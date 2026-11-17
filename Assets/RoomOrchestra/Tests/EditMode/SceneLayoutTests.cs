using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// Checks the real RoomOrchestra scene: every critical interactive element is reachable seated
    /// (pointer ray, no turning, no overhead or floor reach) and select-and-place is wired.
    /// </summary>
    public class SceneLayoutTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";

        [OneTimeSetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // The scene is saved in the opening's first frame; layout checks are about the finished layout.
            UnityEngine.Object.FindFirstObjectByType<IntroSequence>()?.Finish();
            FoldMenu(true); // as in play: the menu opens only when asked (TheOpenMenu... checks it open)
        }

        [SetUp]
        public void FoldMenuForEachTest() =>
            FoldMenu(TestContext.CurrentContext.Test.Name != nameof(NothingAuthoredAtNearZeroScale)); // that one checks sizes as saved

        // The leaf's menu tray (owner, 2026-11-08), folded away as it is in play, or open.
        static void FoldMenu(bool folded)
        {
            var tray = UnityEngine.Object.FindFirstObjectByType<MenuTray>();
            if (tray == null) return;
            tray.transform.localScale = Vector3.one * (folded ? MenuTray.ClosedScale : 1f);
            foreach (var c in tray.GetComponentsInChildren<Collider>(true)) c.enabled = !folded;
            Physics.SyncTransforms();
        }

        // Open, every button on the menu is the first thing a pointer ray from either seated hand meets.
        [Test]
        public void TheOpenMenusButtonsAreTheFirstThingAPointerRayHits()
        {
            FoldMenu(false);
            try
            {
                var tray = Object.FindFirstObjectByType<MenuTray>();
                Assert.IsNotNull(tray, "the leaf has a menu");
                var onTray = tray.GetComponentsInChildren<Collider>().Where(c => InstrumentInputRouter.PressableOwner(c.gameObject) != null).ToArray();
                Assert.GreaterOrEqual(onTray.Select(c => InstrumentInputRouter.PressableOwner(c.gameObject)).Distinct().Count(), 10, "every extra is on it");
                var failures = new System.Collections.Generic.List<string>();
                foreach (var c in onTray)
                {
                    var owner = InstrumentInputRouter.PressableOwner(c.gameObject);
                    foreach (var (name, origin) in PointerOrigins.Where(o => o.name.StartsWith("seated")))
                    {
                        var to = c.bounds.center - origin;
                        var hits = Physics.RaycastAll(origin, to.normalized, to.magnitude + 0.1f).OrderBy(h => h.distance).ToArray();
                        if (hits.Length == 0 || InstrumentInputRouter.PressableOwner(hits[0].collider.gameObject) != owner)
                            failures.Add($"{owner.name} from {name}: hits {(hits.Length > 0 ? hits[0].collider.name : "nothing")} first");
                    }
                }
                Assert.IsEmpty(failures, string.Join("\n", failures));
            }
            finally
            {
                FoldMenu(true);
            }
        }

        static void AssertSeated(string label, Vector3 p)
        {
            var result = ReachEnvelope.Seated.Check(p.x, p.y, p.z);
            Assert.AreEqual(ReachResult.Ok, result, $"{label} at {p} is {result} for a seated player");
        }

        [Test]
        public void ThreeTargetsReachableSeated()
        {
            var targets = Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None);
            Assert.AreEqual(3, targets.Length);
            Assert.AreEqual(3, targets.Select(t => t.Instrument).Distinct().Count());
            foreach (var t in targets) AssertSeated(t.name, t.AimPosition);
        }

        [Test]
        public void TrayTokensOneOfEachOrbReachableSeated()
        {
            var tokens = Object.FindObjectsByType<OrbToken>(FindObjectsSortMode.None);
            Assert.AreEqual(3, tokens.Length);
            Assert.AreEqual(3, tokens.Select(t => t.Orb).Distinct().Count());
            foreach (var t in tokens)
            {
                AssertSeated(t.name, t.transform.position);
                Assert.IsNotNull(t.GetComponentInChildren<Collider>(), $"{t.name} must be tappable");
            }
        }

        [Test]
        public void EveryTargetHasFiveReadableClips()
        {
            foreach (var t in Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None))
            {
                Assert.GreaterOrEqual(t.Notes.Count, NoteMapping.Count, t.name);
                for (int i = 0; i < NoteMapping.Count; i++)
                {
                    var clip = t.Notes[i];
                    Assert.IsNotNull(clip, $"{t.name} clip {i}");
                    clip.LoadAudioData();
                    var data = new float[clip.samples * clip.channels];
                    Assert.IsTrue(clip.GetData(data, 0), $"{clip.name} sample data must be readable (Decompress On Load)");
                    Assert.Greater(data.Max(x => Mathf.Abs(x)), 0.01f, $"{clip.name} is silent");
                }
            }
        }

        [Test]
        public void EveryTargetHasAPitchColumnOfFiveLabeledTilesLowToHigh()
        {
            var columns = Object.FindObjectsByType<PitchColumn>(FindObjectsSortMode.None);
            CollectionAssert.AreEquivalent(
                Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None).Select(t => t.Instrument),
                columns.Select(c => c.Target.Instrument));
            foreach (var column in columns)
            {
                Assert.AreEqual(NoteMapping.Count, column.Tiles.Length, column.name);
                for (int i = 0; i < column.Tiles.Length; i++)
                {
                    var tile = column.Tiles[i];
                    Assert.AreEqual(i, tile.Petal);
                    Assert.AreEqual(column.Target, tile.Target);
                    Assert.IsNotNull(tile.GetComponent<Collider>(), "tappable");
                    AssertSeated(tile.name + " of " + column.name, tile.transform.position);
                    if (i > 0) Assert.Greater(tile.transform.position.y, column.Tiles[i - 1].transform.position.y, "higher tile = higher note");
                    var note = new MusicalNote(column.Target.Instrument, i);
                    string expected = UiText.Tile(note, UiLanguage.English); // the udu's sounds in plain words (Deep, Soft, Slap, Wobble, Tap)
                    var label = column.transform.Find($"TileLabel{i}")?.GetComponentInChildren<TMP_Text>();
                    Assert.IsNotNull(label, $"{column.name} tile {i} has a label");
                    Assert.AreEqual(expected, label.text);
                    Assert.Less(Vector3.Distance(label.transform.position, tile.transform.position), 0.05f, "label sits on its tile");
                }
            }
        }

        [Test]
        public void EveryControlHasAClearTextLabel()
        {
            string[] Texts(Component c) => c.GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToArray();
            var tray = Object.FindFirstObjectByType<OrbToken>().transform.parent;
            CollectionAssert.IsSupersetOf(Texts(tray), new[] { "Flutter", "Bop", "Boom" });
            Assert.IsNull(Object.FindFirstObjectByType<TierButton>(FindObjectsInactive.Include), "Loudness is gone (owner, 2026-11-08)");
            CollectionAssert.Contains(Texts(Object.FindFirstObjectByType<UndoButton>()), "Undo");
            foreach (var column in Object.FindObjectsByType<PitchColumn>(FindObjectsSortMode.None))
                Assert.IsTrue(Texts(column).Any(t => new[] { "udu", "mbira", "erhu" }.Contains(t.Split('\n')[0])), $"{column.name} names its instrument");
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                Assert.IsNotNull(text.font, text.name);
                Assert.IsInstanceOf<TextMeshPro>(text, "world-space TMP (PICO Spatial syncs TMP, not TextMesh)");
                // A Chinese-only label (1=D) is blank in English: measure it in Chinese.
                var localized = text.GetComponentInParent<LocalizedLabel>();
                string authored = text.text;
                if (string.IsNullOrEmpty(authored) && localized != null) text.text = localized.TextFor(UiLanguage.Chinese);
                bool blankInBoth = string.IsNullOrEmpty(text.text); // 1=D over a drum: nothing to read
                text.ForceMeshUpdate(true, true);
                float height = text.textBounds.size.y;
                text.text = authored;
                text.ForceMeshUpdate(true, true);
                if (!blankInBoth) Assert.Greater(height, 0.015f, $"{text.transform.parent.name} is readable size");
                Assert.Less(text.transform.localPosition.sqrMagnitude, 1e-8f, "text sits at its holder's origin");
                Assert.IsFalse(string.IsNullOrWhiteSpace(text.text) && localized == null, $"{text.name} has text (blank only for a Chinese-only label)");
                var toLabel = text.transform.position - new Vector3(0f, 1.15f, 0f);
                // Words parked inside the ball stand's cloud (Flutter's bubble between lessons) are out of sight.
                if (text.transform.lossyScale.x > 0.2f)
                    Assert.Greater(Vector3.Dot(text.transform.forward, toLabel.normalized), 0.5f, $"{text.transform.parent.name} faces the player");
            }
        }

        [Test]
        public void StartPitchTileAuthoredSelectedOnTheFocusedTargetOnly()
        {
            foreach (var column in Object.FindObjectsByType<PitchColumn>(FindObjectsSortMode.None))
                for (int i = 0; i < column.Tiles.Length; i++)
                {
                    bool selected = column.Target.Instrument == InstrumentType.Mbira && i == 2;
                    var expected = column.TileScale * (selected ? 1.2f : 1f);
                    Assert.Less((column.Tiles[i].transform.localScale - expected).magnitude, 1e-4f, $"{column.name} tile {i}");
                }
        }

        [Test]
        public void BallSpawnReachableSeated()
        {
            var launcher = Object.FindFirstObjectByType<BallLauncher>();
            Assert.IsNotNull(launcher);
            var spawn = launcher.transform.Find("SpawnPoint");
            Assert.IsNotNull(spawn);
            AssertSeated("Ball spawn", spawn.position);
        }

        [TestCase(1.15f, TestName = "TargetsNotOccludedByNewControls(seated eye)")]
        [TestCase(1.6f, TestName = "TargetsNotOccludedByNewControls(standing eye)")]
        public void TargetsNotOccludedByNewControls(float eyeHeight)
        {
            var root = Object.FindFirstObjectByType<OrbSelector>();
            Assert.IsNotNull(root);
            var eye = new Vector3(0f, eyeHeight, 0f);
            var room = Object.FindFirstObjectByType<VirtualRoom>();
            // the meadow surrounds everything (its sky domes over all): it is the backdrop, not a control in the way
            var renderers = root.GetComponentsInChildren<Renderer>().Where(r => room == null || !r.transform.IsChildOf(room.transform)).ToArray();
            Assert.Greater(renderers.Length, 0);
            foreach (var target in Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None))
            {
                var toTarget = target.AimPosition - eye;
                var ray = new Ray(eye, toTarget.normalized);
                var targetColliders = target.GetComponentsInChildren<Collider>();
                foreach (var r in renderers)
                {
                    // Things parked inside the target itself (unused echo minis in the lamp shade) are hidden.
                    if (targetColliders.Any(c => c.bounds.Contains(r.bounds.center))) continue;
                    if (r.bounds.IntersectRay(ray, out float distance) && distance < toTarget.magnitude)
                        Assert.Fail($"{r.name} blocks the view of {target.name} from eye height {eyeHeight} m");
                }
            }
        }

        // PICO Spatial picks what a tap hits with a ray from the hand/controller, not the eye, so a control
        // low behind the front row can be shadowed by a front-row button (owner tapped mbira volume and hit
        // Clear on the emulator). Every control must be the first thing hit from every plausible origin.
        static Component TapOwner(Collider c)
        {
            Component owner = c.GetComponentInParent<OrbToken>();
            if (owner == null) owner = c.GetComponentInParent<TierButton>();
            if (owner == null) owner = c.GetComponentInParent<PitchTile>();
            if (owner == null) owner = c.GetComponentInParent<UndoButton>();
            if (owner == null) owner = c.GetComponentInParent<MusicControlButton>();
            if (owner == null) owner = c.GetComponentInParent<RecenterButton>();
            if (owner == null) owner = c.GetComponentInParent<MixButton>();
            if (owner == null) owner = c.GetComponentInParent<HeightButton>();
            if (owner == null) owner = c.GetComponentInParent<LanguageButton>();
            if (owner == null) owner = c.GetComponentInParent<TrayCallButton>();
            if (owner == null) owner = c.GetComponentInParent<TitleStartButton>();
            if (owner == null) owner = c.GetComponentInParent<InstrumentTarget>();
            return owner;
        }

        static readonly (string name, Vector3 origin)[] PointerOrigins =
        {
            ("seated eye", new Vector3(0f, 1.15f, 0f)),
            ("standing eye", new Vector3(0f, 1.6f, 0f)),
            ("seated right hand", new Vector3(0.2f, 0.85f, 0.3f)),
            ("seated left hand", new Vector3(-0.2f, 0.85f, 0.3f)),
            ("standing right hand", new Vector3(0.2f, 1.15f, 0.3f)),
            ("standing left hand", new Vector3(-0.2f, 1.15f, 0.3f)),
        };

        [Test]
        public void EveryControlIsTheFirstThingAPointerRayHits()
        {
            var failures = ShadowedControls(PointerOrigins);
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        static System.Collections.Generic.List<string> ShadowedControls((string name, Vector3 origin)[] origins)
        {
            Physics.SyncTransforms();
            var controls = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .Where(c => c.enabled && TapOwner(c) != null && !(TapOwner(c) is InstrumentTarget))
                .ToArray();
            Assert.Greater(controls.Length, 20);
            var failures = new System.Collections.Generic.List<string>();
            foreach (var control in controls)
            {
                var owner = TapOwner(control);
                foreach (var (name, origin) in origins)
                {
                    var toCenter = control.bounds.center - origin;
                    // Any collider counts, not only other controls: a post or prop in the way eats the tap too.
                    var hits = Physics.RaycastAll(origin, toCenter.normalized, toCenter.magnitude + 0.1f)
                        .OrderBy(h => h.distance)
                        .ToArray();
                    if (hits.Length > 0 && TapOwner(hits[0].collider) != owner)
                        failures.Add($"{owner.name} ({control.name}) from {name}: hits {hits[0].collider.name} first");
                }
            }
            return failures;
        }

        // ---- Height: Standing lifts every control together ----

        static ControlsRig Rig()
        {
            var rig = Object.FindFirstObjectByType<ControlsRig>();
            Assert.IsNotNull(rig, "Controls holder with ControlsRig");
            return rig;
        }

        [Test]
        public void HeightButtonLabeledReachableAndStartsSeated()
        {
            var height = Object.FindFirstObjectByType<HeightButton>();
            Assert.IsNotNull(height);
            AssertSeated(height.name, height.transform.position);
            Assert.IsNotNull(height.GetComponentInChildren<Collider>(), "tappable");
            Assert.AreEqual("Height\nSeated", height.Label.text, "starts seated: nothing moves at startup");
            Assert.Less(Rig().transform.localPosition.sqrMagnitude, 1e-8f, "controls authored at the seated height");
        }

        [Test]
        public void EveryControlLivesUnderTheControlsHolder()
        {
            var rig = Rig();
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                var owner = TapOwner(c);
                if (owner == null || owner is InstrumentTarget || !c.enabled) continue; // off: not tappable (the parked title's Start)
                Assert.IsTrue(c.transform.IsChildOf(rig.transform), $"{owner.name} must lift with the other controls");
            }
        }

        static readonly (string name, Vector3 origin)[] StandingOrigins =
        {
            ("standing eye", new Vector3(0f, 1.6f, 0f)),
            ("standing right hand", new Vector3(0.2f, 1.15f, 0.3f)),
            ("standing left hand", new Vector3(-0.2f, 1.15f, 0.3f)),
            ("tall standing right hand", new Vector3(0.2f, 1.3f, 0.3f)),
            ("tall standing left hand", new Vector3(-0.2f, 1.3f, 0.3f)),
        };

        [Test]
        public void StandingLiftKeepsEveryControlReachableAndUnshadowed()
        {
            var rig = Rig();
            try
            {
                rig.Apply(PlayPosture.Standing);
                Assert.AreEqual(PostureLayout.StandingLift, rig.transform.localPosition.y, 1e-5f);
                var envelope = PostureLayout.Reach(PlayPosture.Standing);
                foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                {
                    var owner = TapOwner(c);
                    if (owner == null || owner is InstrumentTarget) continue;
                    var p = c.bounds.center;
                    Assert.AreEqual(ReachResult.Ok, envelope.Check(p.x, p.y, p.z), $"{owner.name} at {p} standing");
                }
                var failures = ShadowedControls(StandingOrigins);
                Assert.IsEmpty(failures, string.Join("\n", failures));

                var eye = new Vector3(0f, PostureLayout.EyeHeight(PlayPosture.Standing), 0f);
                foreach (var target in Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None))
                {
                    var toTarget = target.AimPosition - eye;
                    foreach (var r in rig.GetComponentsInChildren<Renderer>())
                        if (r.bounds.IntersectRay(new Ray(eye, toTarget.normalized), out float d) && d < toTarget.magnitude)
                            Assert.Fail($"{r.name} blocks the standing view of {target.name}");
                }
            }
            finally
            {
                rig.Apply(PlayPosture.Seated);
            }
            Assert.Less(rig.transform.localPosition.sqrMagnitude, 1e-8f, "Seated puts the controls back");
        }

        [Test]
        public void NothingAuthoredAtNearZeroScale()
        {
            // PICO Spatial exports authored poses; a 0.001-scaled marker was drawn full size in the wrong
            // place on the emulator, alongside other misplaced objects. Hide things by parking, never by scale.
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                var s = t.lossyScale;
                float min = Mathf.Min(Mathf.Abs(s.x), Mathf.Min(Mathf.Abs(s.y), Mathf.Abs(s.z)));
                if (t.GetComponent<Renderer>() != null)
                    Assert.Greater(min, 0.001f, $"{t.name} is authored at near-zero scale {s}");
            }
        }

        [Test]
        public void NoEditorOnlyObjectsInSpatialExportedScene()
        {
            // Kept from PICO Spatial mode (its exporter shifted every later object by one): the preview camera uses
            // EditorPreviewOnly rather than the EditorOnly tag.
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Assert.IsFalse(t.CompareTag("EditorOnly"), $"{t.name} is EditorOnly; use EditorPreviewOnly instead");
        }

        [Test]
        public void RetiredLoopStationIsGone()
        {
            Assert.IsNull(Object.FindFirstObjectByType<LoopStation>(FindObjectsInactive.Include),
                "the loop recorder is replaced by echo orbits");
        }

        [Test]
        public void OneOrbitPerTargetWithSixteenTicksMarkerAndSixMinisPerOrb()
        {
            var conductor = Object.FindFirstObjectByType<EchoConductor>();
            Assert.IsNotNull(conductor, "EchoConductor in scene");
            Assert.AreEqual(3, conductor.Orbits.Count);
            CollectionAssert.AreEquivalent(
                Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None).Select(t => t.Instrument),
                conductor.Orbits.Select(o => o.Target.Instrument));
            foreach (var orbit in conductor.Orbits)
            {
                var children = orbit.GetComponentsInChildren<Transform>();
                Assert.AreEqual(16, children.Count(c => c.name.StartsWith("Tick")), orbit.name);
                Assert.AreEqual(1, children.Count(c => c.name == "PlayMarker"), orbit.name);
                var minis = orbit.GetComponentsInChildren<EchoMini>();
                Assert.AreEqual(OrbCatalog.Count * EchoSequencer.MaxPerTarget, minis.Length, orbit.name);
                Assert.IsTrue(minis.All(m => m.Orbit == orbit && m.GetComponentInChildren<Collider>() != null), "minis are tappable and wired");
                var park = minis[0].transform.position;
                Assert.IsTrue(minis.All(m => (m.transform.position - park).sqrMagnitude < 1e-8f), "all minis authored parked");
            }
        }

        [Test]
        public void OrbitMarkerIsAtTheTopAndEchoesReachItOnTheirStep()
        {
            foreach (var orbit in Object.FindFirstObjectByType<EchoConductor>().Orbits)
            {
                Assert.Greater(orbit.MarkerPosition.y, orbit.transform.position.y, "marker at 12 o'clock");
                for (int step = 0; step < 16; step++)
                    Assert.Less((orbit.PositionAt(step / 16.0, step) - orbit.MarkerPosition).magnitude, 1e-4f, $"{orbit.name} step {step}");
                // a quarter bar earlier, a step-0 echo is a quarter turn before the marker (left side as seen)
                var before = orbit.PositionAt(-0.25, 0) - orbit.transform.position;
                Assert.Less(Vector3.Dot(before, orbit.transform.right), -0.2f, "travels clockwise toward the marker");
            }
        }

        [Test]
        public void PauseAndClearButtonsLabeledAndReachableSeated()
        {
            var buttons = Object.FindObjectsByType<MusicControlButton>(FindObjectsSortMode.None);
            CollectionAssert.AreEquivalent(new[] { MusicControlButton.Kind.Pause, MusicControlButton.Kind.Clear }, buttons.Select(b => b.ButtonKind));
            foreach (var b in buttons)
            {
                AssertSeated(b.name, b.transform.position);
                Assert.IsNotNull(b.GetComponentInChildren<Collider>(), "tappable");
                Assert.IsNotNull(b.Label, "has a label");
                Assert.AreEqual(b.ButtonKind == MusicControlButton.Kind.Pause ? "Pause" : "Clear", b.Label.text);
            }
        }

        [Test]
        public void PauseButtonShowsBarsWithPlayTriangleParkedInsideSlab()
        {
            var pause = Object.FindObjectsByType<MusicControlButton>(FindObjectsSortMode.None)
                .Single(b => b.ButtonKind == MusicControlButton.Kind.Pause);
            Assert.IsNotNull(pause.PauseIcon, "pause bars wired");
            Assert.IsNotNull(pause.PlayIcon, "play triangle wired");
            Assert.AreEqual(pause.IconShown, pause.PauseIcon.localPosition, "bars visible while playing");
            Assert.AreEqual(pause.IconParked, pause.PlayIcon.localPosition, "triangle starts parked");

            var slab = pause.transform.Find("PauseSlab").GetComponent<Renderer>().bounds;
            foreach (var r in pause.PlayIcon.GetComponentsInChildren<Renderer>())
            {
                Assert.IsTrue(slab.Contains(r.bounds.min) && slab.Contains(r.bounds.max), $"{r.name} hidden inside the slab");
                Assert.IsTrue(r.gameObject.activeInHierarchy, "parked, not deactivated (PICO draws inactive objects)");
            }
            foreach (var r in pause.PauseIcon.GetComponentsInChildren<Renderer>())
                Assert.Greater(r.bounds.max.y, slab.max.y + 0.01f, $"{r.name} stands above the slab");

            // Simulate a pause: the icons swap places, so the triangle shows and the bars hide.
            pause.PauseIcon.localPosition = pause.IconParked;
            pause.PlayIcon.localPosition = pause.IconShown;
            foreach (var r in pause.PauseIcon.GetComponentsInChildren<Renderer>())
                Assert.IsTrue(slab.Contains(r.bounds.min) && slab.Contains(r.bounds.max), $"{r.name} hidden when parked");
            foreach (var r in pause.PlayIcon.GetComponentsInChildren<Renderer>())
                Assert.Greater(r.bounds.max.y, slab.max.y + 0.01f, $"{r.name} shown when paused");
        }

        [Test]
        public void PlayTriangleFacesOutward()
        {
            var mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/RoomOrchestra/Meshes/PlayTriangle.asset");
            Assert.IsNotNull(mesh);
            var center = new Vector3(-1f / 6f, 0f, 0f); // centroid of the triangle prism
            var v = mesh.vertices;
            var t = mesh.triangles;
            Assert.AreEqual(8 * 3, t.Length, "2 caps + 3 quad sides = 8 triangles");
            for (int i = 0; i < t.Length; i += 3)
            {
                var normal = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                var mid = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f;
                Assert.Greater(Vector3.Dot(normal, mid - center), 0f, $"triangle {i / 3} faces outward");
            }
            Assert.AreEqual(new Vector3(-0.5f, -0.5f, -0.5f), mesh.bounds.min);
            Assert.AreEqual(new Vector3(0.5f, 0.5f, 0.5f), mesh.bounds.max);
        }

        [Test]
        public void RecenterButtonLabeledReachableAndStageAnchored()
        {
            var button = Object.FindFirstObjectByType<RecenterButton>();
            Assert.IsNotNull(button, "Recenter button in the scene");
            AssertSeated(button.name, button.transform.position);
            Assert.IsNotNull(button.GetComponentInChildren<Collider>(), "tappable");
            Assert.AreEqual("Recenter", button.GetComponentInChildren<TMP_Text>().text);

            var anchor = Object.FindFirstObjectByType<StageAnchor>();
            Assert.IsNotNull(anchor, "stage root can be recentered");
            Assert.IsNull(anchor.transform.parent, "anchor is a scene root, so it moves the whole stage");
            foreach (var mustMove in new Component[] { button, Object.FindFirstObjectByType<OrbSelector>(), Object.FindFirstObjectByType<EchoConductor>() })
                Assert.IsTrue(mustMove.transform.IsChildOf(anchor.transform), $"{mustMove.name} moves with the stage");
            foreach (var target in Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None))
                Assert.IsTrue(target.transform.IsChildOf(anchor.transform), $"{target.name} moves with the stage");
        }

        [Test]
        public void EachInstrumentHasALabeledReachableVolumeButton()
        {
            var mixes = Object.FindObjectsByType<MixButton>(FindObjectsSortMode.None);
            CollectionAssert.AreEquivalent(
                Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None),
                mixes.Select(m => m.Target), "one volume button per instrument");
            foreach (var mix in mixes)
            {
                AssertSeated(mix.name, mix.transform.position);
                Assert.IsNotNull(mix.GetComponent<Collider>(), "tappable");
                StringAssert.EndsWith("\nVol 100%", mix.Label.text);
                StringAssert.StartsWith(mix.Target.Instrument.ToString().ToLowerInvariant(), mix.Label.text);
            }
        }

        // ---- Prompt 06: tray characters built from the approved reference (Blender meshes) ----
        [Test]
        public void TrayTokensAreTheThreeCharactersFacingThePlayer()
        {
            string[] parts = { "Outline", "EyeL", "EyeR", "Smile", "Mouth_O", "Highlight", "Motif" };
            foreach (var token in Object.FindObjectsByType<OrbToken>(FindObjectsSortMode.None))
            {
                var visual = token.transform.Find("Visual");
                Assert.IsNotNull(visual, $"{token.name} has a wobbling visual");
                var body = visual.GetComponent<MeshFilter>();
                Assert.IsNotNull(body, $"{token.name} has a character body");
                Assert.AreEqual($"{token.Orb}_Body", body.sharedMesh.name);
                int triangles = body.sharedMesh.triangles.Length / 3;
                foreach (var part in parts)
                {
                    var child = visual.Find(part);
                    Assert.IsNotNull(child, $"{token.name} has {part}");
                    var mesh = child.GetComponent<MeshFilter>().sharedMesh;
                    Assert.AreEqual($"{token.Orb}_{part}", mesh.name);
                    triangles += mesh.triangles.Length / 3;
                }
                Assert.Less(triangles, 3500, $"{token.name} stays low-poly (3 tray characters; orbit minis use a lighter set)");

                // Face toward the seated player: the eyes are nearer the eye than the body centre.
                var eye = new Vector3(0f, 1.15f, 0f);
                var eyes = visual.Find("EyeL").GetComponent<Renderer>().bounds.center;
                Assert.Less(Vector3.Distance(eye, eyes), Vector3.Distance(eye, token.transform.position) - 0.02f, $"{token.name} faces the player");
                // The player's left eye is on the player's left.
                var right = Vector3.Cross(Vector3.up, token.transform.position - eye).normalized;
                Assert.Less(Vector3.Dot(visual.Find("EyeL").GetComponent<Renderer>().bounds.center - token.transform.position, right),
                    Vector3.Dot(visual.Find("EyeR").GetComponent<Renderer>().bounds.center - token.transform.position, right), $"{token.name} eyes not mirrored");

                // The surprised mouth waits wholly inside the body (PICO Spatial draws inactive objects).
                var mouth = visual.Find("Mouth_O").GetComponent<Renderer>().bounds;
                var bodyBounds = visual.GetComponent<Renderer>().bounds;
                var inner = new Bounds(bodyBounds.center, bodyBounds.size * 0.8f);
                Assert.IsTrue(inner.Contains(mouth.min) && inner.Contains(mouth.max), $"{token.name} oh-mouth parked inside the body");
            }
        }

        [Test]
        public void UnusedEchoCharactersHideWhollyInsideTheirParkingSpot()
        {
            // PICO Spatial draws everything, so unused minis must be fully inside the hub (or the lamp shade).
            Physics.SyncTransforms();
            foreach (var orbit in Object.FindObjectsByType<OrbitView>(FindObjectsSortMode.None))
            {
                var hub = orbit.transform.Find("Hub");
                var shade = orbit.Target.GetComponentsInChildren<Collider>();
                foreach (var mini in orbit.GetComponentsInChildren<EchoMini>())
                    foreach (var filter in mini.GetComponentsInChildren<MeshFilter>())
                        foreach (var v in filter.sharedMesh.vertices)
                        {
                            var p = filter.transform.TransformPoint(v);
                            bool hidden = hub != null
                                ? (p - hub.position).magnitude <= hub.lossyScale.x * 0.5f + 1e-4f
                                : shade.Any(c => (c.ClosestPoint(p) - p).sqrMagnitude < 1e-8f);
                            Assert.IsTrue(hidden, $"{filter.name} of {mini.name} on {orbit.name} shows at {p}");
                        }
            }
        }

        static void AssertAlive(Component character, CharacterFace face, bool awake)
        {
            Assert.IsNotNull(face, $"{character.name} face");
            var jelly = character.GetComponent<JellyBody>();
            Assert.IsNotNull(jelly, $"{character.name} jelly body");
            Assert.AreEqual(awake, jelly.IsAwake, $"{character.name} authored awake state");
            Assert.IsNotNull(jelly.Visual, character.name);
            Assert.AreNotEqual(character.transform, jelly.Visual, $"{character.name} wobbles a child, never the root its owner moves and scales");
            Assert.IsTrue(face.EyeL.IsChildOf(jelly.Visual) && face.Smile.IsChildOf(jelly.Visual), $"{character.name} face rides on the jelly");
            Assert.AreEqual(Vector3.one, jelly.Visual.localScale, $"{character.name} authored at rest");
            Assert.AreEqual(Quaternion.identity, jelly.Visual.localRotation, character.name);
        }

        [Test]
        public void EveryCharacterHasAFaceAndTheDirectorListens()
        {
            foreach (var token in Object.FindObjectsByType<OrbToken>(FindObjectsSortMode.None))
            {
                var face = token.GetComponent<CharacterFace>();
                Assert.IsNotNull(face, token.name);
                Assert.IsNotNull(face.MouthO, $"{token.name} can say oh");
                AssertAlive(token, face, awake: true);
            }
            foreach (var mini in Object.FindObjectsByType<EchoMini>(FindObjectsSortMode.None))
                AssertAlive(mini, mini.GetComponent<CharacterFace>(), awake: false); // parked in the hub
            foreach (var ball in Object.FindObjectsByType<Ball>(FindObjectsSortMode.None))
            {
                AssertAlive(ball, ball.GetComponent<CharacterFace>(), awake: false); // the launcher wakes the one it hands out
                Assert.IsFalse(ball.GetComponent<JellyBody>().FacesViewer, $"{ball.name} rolls and tumbles freely (owner)");
            }
            var director = Object.FindFirstObjectByType<CharacterDirector>();
            Assert.IsNotNull(director, "CharacterDirector in scene");
        }

        [Test]
        public void FloorPoofsAndTheCloudsHideInsideTheBallStand()
        {
            var floor = GameObject.Find("FloorCollider");
            Assert.IsNotNull(floor);
            Assert.IsNotNull(floor.GetComponent<GroundSurface>(), "landing on the floor poofs");
            var clouds = Object.FindObjectsByType<PoofCloud>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(clouds.Length, 16, "enough for quick falls and for Clear's smoke over everything that grew");
            var post = GameObject.Find("RoomOrchestra").transform.Find("BallDispenser").GetComponentsInChildren<Collider>()
                .First(c => c.name == "Pedestal");
            Physics.SyncTransforms();
            foreach (var cloud in clouds)
            {
                Assert.AreEqual(7, cloud.Puffs.Count, cloud.name);
                foreach (var puff in cloud.Puffs)
                {
                    Assert.IsNull(puff.GetComponent<Collider>(), "puffs never block a tap or a ball");
                    foreach (var v in puff.GetComponent<MeshFilter>().sharedMesh.vertices)
                    {
                        var p = puff.TransformPoint(v);
                        Assert.Less((post.ClosestPoint(p) - p).sqrMagnitude, 1e-8f, $"{puff.name} of {cloud.name} shows outside the stand at {p}");
                    }
                }
            }
        }

        [Test]
        public void TheReadyBallFloatsOnACloudNotAPost()
        {
            var stand = GameObject.Find("RoomOrchestra").transform.Find("BallDispenser");
            var core = stand.Find("Pedestal");
            Physics.SyncTransforms();
            var solid = core.GetComponent<Collider>();
            Assert.IsNotNull(solid, "a solid cloud core hides the spares and stops throws");
            Assert.Less(solid.bounds.size.y, 0.2f, "no post");
            Assert.AreEqual("StandCloud", core.GetComponent<Renderer>().sharedMaterial.name);
            var puffs = stand.Find("CloudPuffs");
            Assert.GreaterOrEqual(puffs.childCount, 6, "puffy");
            foreach (Transform puff in puffs) Assert.IsNull(puff.GetComponent<Collider>(), $"{puff.name} never blocks a tap or a ball");

            float cloudTop = stand.GetComponentsInChildren<Renderer>().Where(r => r.transform.IsChildOf(puffs) || r.transform == core).Max(r => r.bounds.max.y);
            float cloudBottom = stand.GetComponentsInChildren<Renderer>().Where(r => r.transform.IsChildOf(puffs) || r.transform == core).Min(r => r.bounds.min.y);
            var spawn = Object.FindFirstObjectByType<BallLauncher>().transform.Find("SpawnPoint").position;
            float tallestHalf = OrbCatalog.All.Max(o => o.ScaleY) * 0.5f * 0.1f; // balls are about 0.1 m
            Assert.Greater(spawn.y - tallestHalf, cloudTop - 0.01f, "the ready ball sits on top of the cloud, not inside it");
            Assert.Less(spawn.y - tallestHalf, cloudTop + 0.05f, "and close enough to look like it rests there");
            Assert.Greater(cloudBottom, 0.7f, "floating: nothing reaches down to the floor");
            // Spares are authored inside the cloud (nothing moves at startup on PICO Spatial); one ball waits on top.
            int ready = 0;
            foreach (var ball in stand.Find("BallPool").GetComponentsInChildren<Ball>(true))
            {
                if ((ball.transform.position - spawn).sqrMagnitude < 1e-6f) { ready++; continue; }
                Assert.Less((solid.ClosestPoint(ball.transform.position) - ball.transform.position).sqrMagnitude, 1e-8f, $"{ball.name} waits inside the cloud");
            }
            Assert.AreEqual(1, ready, "one ball ready on the cloud");
        }

        // ---- The opening: the scene is saved in its first frame (PICO Spatial keeps startup poses) ----
        [Test]
        public void TheSceneOpensWithTheLeafOutOfViewAndTheCharactersInTheirBush()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); // as saved, without Finish()
            var intro = Object.FindFirstObjectByType<IntroSequence>();
            Assert.IsNotNull(intro, "the opening is wired");
            Assert.IsFalse(intro.Done);
            var pad = Object.FindFirstObjectByType<RoamingTray>().transform;
            Assert.AreEqual(pad, intro.Leaf.parent, "the swoop moves the leaf within the pad, which follows the player");
            var leafBody = intro.Leaf.Find("Leaf");
            var eye = new Vector3(0f, 1.15f, 0f);
            var toLeaf = leafBody.position - eye;
            Assert.Greater(Vector3.Angle(Vector3.forward, new Vector3(toLeaf.x, 0f, toLeaf.z)), 55f, "the leaf waits out of view");
            var stem = pad.GetComponentInChildren<LeafStalk>();
            Assert.IsNotNull(stem, "a stalk for the leaf to land on");
            Assert.LessOrEqual(stem.Stalk.lossyScale.y, LeafStalk.Stub + 1e-6f, "the stalk has not grown yet");
            var cloud = GameObject.Find("RoomOrchestra").transform.Find("BallDispenser/Pedestal").GetComponent<Collider>();
            Assert.IsTrue(stem.Hole.GetComponentsInChildren<Renderer>().All(r => cloud.bounds.Contains(r.bounds.min) && cloud.bounds.Contains(r.bounds.max)), "the closed hole waits inside the ball stand's cloud");
            var furniture = GameObject.Find("RoomOrchestra").transform.Find("Furniture").GetComponentsInChildren<Collider>();
            foreach (var token in Object.FindObjectsByType<OrbToken>(FindObjectsSortMode.None))
            {
                var p = token.transform.position;
                Assert.Less(p.y, 0.12f, $"{token.name} sits on the floor");
                Assert.IsTrue(token.transform.IsChildOf(intro.Leaf), $"{token.name} belongs on the leaf");
                // owner, headset 2026-11-12: not on the floor in front of the player at the title, hidden in a bush
                var bush = GameObject.Find("FriendsBush").GetComponent<Renderer>().bounds;
                Assert.IsTrue(bush.Contains(token.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; }).center),
                    $"{token.name} waits hidden in the friends' bush");
                Assert.IsFalse(furniture.Any(c => c.bounds.Contains(p)), $"{token.name} is not inside furniture");
            }

            intro.Finish();
            Assert.AreEqual(Vector3.zero, intro.Leaf.localPosition, "finished: the leaf is home");
            Assert.AreEqual(0.475f, stem.Stalk.lossyScale.y, 0.005f, "the stalk reaches the leaf's underside");
            Assert.AreEqual(0f, stem.Stalk.position.y - stem.Stalk.lossyScale.y / 2f, 1e-4f, "standing on the floor");
            Assert.Greater(stem.Hole.position.y, 0.5f, "the hole has closed and gone back inside the cloud: just the stalk on the floor");
            Assert.IsTrue(stem.Hole.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterial.color.maxColorComponent < 0.05f), "a black hole and black cracks");
            Assert.GreaterOrEqual(stem.Hole.GetComponentsInChildren<Renderer>().Count(r => r.name.StartsWith("Crack")), 7, "cracks run out from it");
            var rig = Object.FindFirstObjectByType<ControlsRig>();
            rig.Apply(PlayPosture.Standing);
            stem.Apply();
            Assert.AreEqual(0f, stem.Stalk.position.y - stem.Stalk.lossyScale.y / 2f, 1e-4f, "standing: the stalk still reaches the floor");
            Assert.AreEqual(0.475f + PostureLayout.StandingLift, stem.Stalk.lossyScale.y, 0.005f, "and the lifted leaf");
            rig.Apply(PlayPosture.Seated);
            stem.Apply();
            Assert.AreEqual(Quaternion.identity, intro.Leaf.localRotation);
            foreach (var token in Object.FindObjectsByType<OrbToken>(FindObjectsSortMode.None))
                Assert.Greater(token.transform.position.y, 0.5f, $"{token.name} is on the leaf");
        }

        static readonly Vector3 DispenserHome = new Vector3(0.62f, 0f, 1.15f); // the stand's place beside the leaf

        // ---- The title card (Version 1 feedback: "boot into a proper title screen") ----
        [Test]
        public void TheGameOpensOnATitleCardInFrontWithAStartButton()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); // as saved, without Finish()
            var title = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(title, "a title card");
            Assert.IsTrue(title.Showing, "showing at launch");
            var p = title.Card.position;
            // close and still from the first frame (owner, headset 2026-11-14: within reach of a hand, and it never moves)
            Assert.That(new Vector2(p.x, p.z).magnitude, Is.InRange(0.8f, 0.9f), "within arm's reach");
            Assert.Less(Vector3.Angle(Vector3.forward, new Vector3(p.x, 0f, p.z)), 10f, "straight in front");
            Assert.That(p.y, Is.InRange(1.15f, 1.45f), "at or just above seated eye height");
            // The ball stand with its ball greets the player beside the sign: at least 1 m out, clear of the sign's post,
            // and inside the headset's view.
            var stand = GameObject.Find("RoomOrchestra").transform.Find("BallDispenser");
            var s = stand.position;
            Assert.GreaterOrEqual(new Vector2(s.x, s.z).magnitude, 1f, "at least 1 m out");
            var ball = stand.Find("SpawnPoint").position;
            var frame = title.Card.Find("TitleSign").GetComponent<Renderer>().bounds;
            float postBearing = Mathf.Atan2(frame.max.x, p.z) * Mathf.Rad2Deg, ballBearing = Mathf.Atan2(ball.x, ball.z) * Mathf.Rad2Deg;
            Assert.Greater(ballBearing, postBearing, "the ball shows beside the sign, not hidden behind it");
            Assert.Less(ballBearing, 50f, "and inside the view when looking at the sign");
            var arc = GameObject.Find("RoomOrchestra").transform.Find("AimArc");
            Assert.Less(Vector3.Distance(arc.localPosition, stand.position - DispenserHome), 1e-3f, "the aim arc starts with the stand, offset once (never more)");
            var start = title.Card.GetComponentInChildren<TitleStartButton>();
            Assert.IsNotNull(start, "a Start button");
            Assert.Greater(Mathf.Abs(title.Card.InverseTransformPoint(start.transform.position).x), 0.15f, "off the centre line, clear of PICO's window handle");
            var hit = start.GetComponentInChildren<Collider>();
            Assert.IsNotNull(hit, "Start is tappable");
            Assert.IsTrue(InstrumentInputRouter.PressableOwner(hit.gameObject) is TitleStartButton, "and the pointer knows it can tap it (its ray turns gold)");
            var texts = title.Card.GetComponentsInChildren<LocalizedLabel>().Select(l => l.TextFor(UiLanguage.English)).ToArray();
            CollectionAssert.Contains(texts, "Start");
            CollectionAssert.Contains(texts, "A mixed-reality musical playground");
            var letters = title.Card.Find("Hanging/TitleLettering").GetComponentsInChildren<MeshFilter>();
            Assert.AreEqual(2, letters.Length, "the key-art lettering: cream letters on a plum outline");
            Assert.IsTrue(letters.All(f => f.sharedMesh.triangles.Length / 3 > 100), "traced shapes, not a textured quad (PICO Spatial did not draw that)");

            Object.FindFirstObjectByType<IntroSequence>().Finish();
            var cloud = GameObject.Find("RoomOrchestra").transform.Find("BallDispenser/Pedestal").GetComponent<Collider>();
            Assert.IsTrue(title.Parked);
            Assert.IsTrue(title.Card.GetComponentsInChildren<Renderer>().All(r => cloud.bounds.Contains(r.bounds.center)), "parked inside the ball stand's cloud");
            Assert.IsFalse(hit.enabled, "and its button no longer catches taps");
        }

        // Version 1 feedback: nothing showed what the eye or pointer was on. In PICO XR mode the controller's ray turns
        // gold over anything it can tap; each of those colliders sits on a visible mesh.
        [Test]
        public void EverythingTappableIsSomethingYouCanSee()
        {
            // TapOwner leaves out the echoes on the rings (the pointer tests skip them); they are tappable too.
            Component Owner(Collider c) => TapOwner(c) ?? c.GetComponentInParent<EchoMini>();
            var tappable = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c => c.enabled && Owner(c) != null).ToArray();
            Assert.Greater(tappable.Length, 90, "controls, tiles, characters, the 54 echoes and furniture");
            var missing = tappable
                .Where(c => !c.TryGetComponent<MeshRenderer>(out _))
                .Select(c => $"{Owner(c).name} ({c.name})").ToArray();
            Assert.IsEmpty(missing, "nothing visible under: " + string.Join(", ", missing));
        }

        [Test]
        public void ClearsSmokeHasAGentleSoundAndLandingsAreSilent()
        {
            var smoke = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RoomOrchestra/Audio/Effects/Smoke.wav");
            Assert.IsNotNull(smoke);
            foreach (var cloud in Object.FindObjectsByType<PoofCloud>(FindObjectsSortMode.None))
            {
                var so = new UnityEditor.SerializedObject(cloud);
                var source = (AudioSource)so.FindProperty("voice").objectReferenceValue;
                Assert.IsNotNull(source, cloud.name);
                Assert.AreSame(smoke, so.FindProperty("smokeSound").objectReferenceValue, cloud.name);
                Assert.IsFalse(source.playOnAwake, "silent until it bursts");
                Assert.AreEqual(0f, source.spatialBlend, "2D like the instruments");
                Assert.LessOrEqual(source.volume, 0.5f, "quieter than the music");
            }
            // Peak and onset measured on the clips: soft, and the big smoke swells rather than bangs.
            float PeakSeconds(AudioClip clip, out float peak)
            {
                clip.LoadAudioData();
                var data = new float[clip.samples * clip.channels];
                clip.GetData(data, 0);
                int at = 0;
                peak = 0f;
                for (int i = 0; i < data.Length; i++)
                    if (Mathf.Abs(data[i]) > peak) { peak = Mathf.Abs(data[i]); at = i; }
                Assert.Less(Mathf.Abs(data[data.Length - 1]), 1e-3f, $"{clip.name} ends without a click");
                return at / (float)clip.frequency;
            }
            float smokeOnset = PeakSeconds(smoke, out float smokePeak);
            Assert.LessOrEqual(smokePeak, 0.55f);
            Assert.Greater(smokeOnset, 0.03f, "Clear's smoke swells in, never a bang");
        }

        // ---- Roaming: the leaf pad (A) and its call leaf (C) ----

        [Test]
        public void TheLeafPadCarriesTheTrayButtonsAndBallStandButNotTheNoteColumns()
        {
            var pad = Object.FindFirstObjectByType<RoamingTray>();
            Assert.IsNotNull(pad, "LeafPad with RoamingTray");
            Assert.AreEqual(Vector3.zero, pad.transform.localPosition, "authored at home");
            Assert.AreEqual(Quaternion.identity, pad.transform.localRotation);
            Assert.IsTrue(Object.FindFirstObjectByType<OrbToken>().transform.IsChildOf(pad.transform), "the ball tray");
            Assert.IsTrue(Object.FindFirstObjectByType<UndoButton>().transform.IsChildOf(pad.transform));
            foreach (var b in Object.FindObjectsByType<MusicControlButton>(FindObjectsSortMode.None)) Assert.IsTrue(b.transform.IsChildOf(pad.transform), b.name);
            Assert.IsTrue(Object.FindFirstObjectByType<RecenterButton>().transform.IsChildOf(pad.transform));
            Assert.IsTrue(Object.FindFirstObjectByType<HeightButton>().transform.IsChildOf(pad.transform));
            Assert.IsTrue(Object.FindFirstObjectByType<LanguageButton>().transform.IsChildOf(pad.transform));
            Assert.IsTrue(Object.FindFirstObjectByType<TrayCallButton>().transform.IsChildOf(pad.transform), "the call leaf");
            foreach (var column in Object.FindObjectsByType<PitchColumn>(FindObjectsSortMode.None))
                Assert.IsFalse(column.transform.IsChildOf(pad.transform), $"{column.name} stays by its instrument");
            Assert.IsTrue(pad.transform.IsChildOf(Rig().transform), "Height still lifts the pad");

            var so = new UnityEditor.SerializedObject(pad);
            var carried = so.FindProperty("carried");
            var names = Enumerable.Range(0, carried.arraySize).Select(i => carried.GetArrayElementAtIndex(i).objectReferenceValue.name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "BallDispenser", "PoofPark" }, names, "the ball stand and the clouds waiting in it");
            Assert.AreEqual("BallPool", so.FindProperty("keepStill").objectReferenceValue.name, "thrown balls stay where they are");
            Assert.IsNotNull(so.FindProperty("launcher").objectReferenceValue);
        }

        // Owner, headset 2026-11-08: Start over (scan included), a clearer name than "Places", and no Loudness or Loop.
        [Test]
        public void TheMenuHasStartOverAndChooseFurniture()
        {
            var tray = Object.FindFirstObjectByType<MenuTray>(FindObjectsInactive.Include);
            Assert.IsNull(Object.FindFirstObjectByType<TierButton>(FindObjectsInactive.Include), "no Loudness (owner, 2026-11-08)");
            Assert.IsTrue(Object.FindFirstObjectByType<StartOverButton>(FindObjectsInactive.Include).transform.IsChildOf(tray.transform), "Start over on the menu");
            var placesButton = Object.FindFirstObjectByType<PlacesButton>(FindObjectsInactive.Include);
            var places = placesButton.GetComponentInChildren<LocalizedLabel>(true);
            Assert.AreEqual("Choose\nfurniture", places.TextFor(UiLanguage.English));
            // owner, headset 2026-11-08: on the leaf itself, next to Boom
            Assert.IsFalse(placesButton.transform.IsChildOf(tray.transform), "Choose furniture is on the leaf, not the menu");
            var boom = Object.FindObjectsByType<OrbToken>(FindObjectsSortMode.None).First(t => t.Orb == OrbType.Boom);
            Assert.Greater(placesButton.transform.position.x, boom.transform.position.x, "right of Boom");
            Assert.Less(Vector3.Distance(placesButton.transform.position, boom.transform.position), 0.3f, "beside Boom");
            Assert.AreEqual((UiTerm.ConfirmStartOver, UiTerm.ConfirmRestart), ConfirmCard.TermsFor(Object.FindFirstObjectByType<StartOverButton>(FindObjectsInactive.Include)), "Start over asks first");
        }

        [Test]
        public void CallLeafIsLabeledAndTappable()
        {
            var call = Object.FindFirstObjectByType<TrayCallButton>();
            Assert.IsNotNull(call);
            Assert.IsNotNull(call.GetComponentInChildren<Collider>());
            var label = call.GetComponentInChildren<LocalizedLabel>();
            Assert.IsNotNull(label, "labeled, and the label follows the language");
            Assert.AreEqual("Come here", label.Text.text);
            Assert.AreEqual("过来", label.TextFor(UiLanguage.Chinese));
            // owner, headset 2026-11-08: "the 'come here' should be on the first leaf, not buried in the menu"
            Assert.IsNull(call.GetComponentInParent<MenuTray>(true), "on the leaf itself, not on the menu tray");
            var menu = Object.FindObjectsByType<MenuButton>(FindObjectsSortMode.None).First(m => m.name == "MenuButton"); // not the tray's Close
            Assert.Less(call.transform.position.x, menu.transform.position.x, "first on the leaf's row, before Menu");
            Assert.AreEqual(menu.transform.position.y, call.transform.position.y, 0.01f, "in the same row");
        }

        // ---- Rainforest that grows with the music (vines, butterflies, toadstools, sparkles) ----
        // The largest world size of anything a sprout draws.
        static float Seen(Component sprout) => sprout.GetComponentsInChildren<Renderer>().Max(r => r.bounds.size.magnitude);

        static bool InsideAny(Collider[] solids, Vector3 p) => solids.Any(c => (c.ClosestPoint(p) - p).sqrMagnitude < 1e-8f);

        [Test]
        public void RainforestDecorIsTapThroughLightAndWaitsHidden()
        {
            var rainforest = GameObject.Find("Rainforest");
            Assert.IsNotNull(rainforest, "rainforest built");
            Assert.IsEmpty(rainforest.GetComponentsInChildren<Collider>(), "decoration never eats a tap or a ball");
            int triangles = rainforest.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3);
            Assert.Less(triangles, 50000, "light enough for a standalone headset");
            Assert.IsNotNull(rainforest.GetComponent<JungleDirector>(), "the jungle listens to the music");
            Physics.SyncTransforms();

            // Vines start folded inside their furniture: every vertex of every piece and flower is out of sight.
            var stage = GameObject.Find("RoomOrchestra").transform;
            var solids = stage.Find("Furniture").GetComponentsInChildren<Collider>()
                .Concat(stage.Find("BallDispenser").GetComponentsInChildren<Collider>().Where(c => c.name == "Pedestal")).ToArray();
            var vines = rainforest.GetComponentsInChildren<VineGrowth>();
            Assert.AreEqual(3, vines.Length, "table (both legs), chair, lamp (the ball stand is a cloud now)");
            foreach (var vine in vines)
            {
                Assert.AreEqual(0, vine.Shown, $"{vine.name} starts as a bud");
                foreach (var piece in vine.Segments.Concat(vine.Flowers))
                {
                    Assert.AreEqual(VineGrowth.HiddenScale, piece.localScale.x, 1e-6f, piece.name);
                    foreach (var filter in piece.GetComponentsInChildren<MeshFilter>())
                        foreach (var v in filter.sharedMesh.vertices)
                        {
                            var p = filter.transform.TransformPoint(v);
                            Assert.IsTrue(InsideAny(solids, p), $"{filter.name} of {piece.parent.name} shows at {p} before it has grown");
                        }
                }
            }

            // Butterflies (and their trails) wait inside the lamp shade.
            var lamp = Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None).First(t => t.Instrument == InstrumentType.Erhu);
            var shade = lamp.GetComponentsInChildren<Collider>();
            var butterflies = rainforest.GetComponentsInChildren<Butterfly>();
            Assert.AreEqual(3, butterflies.Length);
            foreach (var b in butterflies)
            {
                Assert.AreEqual(8, b.Trail.Count, $"{b.name} sparkle trail");
                Assert.Greater(b.WingL.localEulerAngles.z, 180f, $"{b.name} rests with its wings up in a V");
                foreach (var filter in b.GetComponentsInChildren<MeshFilter>().Concat(b.Trail.SelectMany(t => t.GetComponentsInChildren<MeshFilter>())))
                    foreach (var v in filter.sharedMesh.vertices)
                    {
                        var p = filter.transform.TransformPoint(v);
                        Assert.IsTrue(InsideAny(shade, p), $"{filter.name} of {b.name} shows outside the lamp shade at {p}");
                    }
            }

            // Toadstools and flowers wait out of sight (owner: their waiting state should not be seen).
            var toadstools = rainforest.GetComponentsInChildren<Toadstool>();
            Assert.AreEqual(JungleGrowth.MushroomAt.Length, toadstools.Length);
            foreach (var t in toadstools)
            {
                Assert.AreEqual(SproutSync.HiddenScale, t.transform.localScale.x, 1e-6f, $"{t.name} hidden at startup");
                Assert.Less(Seen(t), 0.005f, $"{t.name} too small to see");
                Assert.Less(t.transform.position.y, 0.01f, $"{t.name} on the floor");
            }

            // Flowers wait on the floor around the room, clear of the furniture, the seat and each other.
            var flowers = rainforest.GetComponentsInChildren<FloorFlower>();
            Assert.AreEqual(20, flowers.Length, "a meadow's worth (RainforestArt.FloorFlowers)");
            var furniture = stage.Find("Furniture").GetComponentsInChildren<Collider>().Select(c => c.bounds).ToArray();
            foreach (var f in flowers)
            {
                var p = f.transform.position;
                Assert.AreEqual(SproutSync.HiddenScale, f.transform.localScale.x, 1e-6f, $"{f.name} hidden at startup");
                Assert.Less(Seen(f), 0.005f, $"{f.name} too small to see");
                Assert.IsNotNull(f.Bloom, $"{f.name} has petals to open");
                Assert.IsNotNull(f.Bud, $"{f.name} grows as a bud");
                Assert.AreEqual(FloorFlower.ClosedScale, f.Bloom.localScale.x, 1e-6f, $"{f.name}'s petals start closed");
                Assert.Less(Mathf.Abs(p.y), 0.01f, $"{f.name} on the floor");
                Assert.Greater(new Vector2(p.x, p.z).magnitude, 0.7f, $"{f.name} clear of the player's feet");
                Assert.IsFalse(furniture.Any(b => p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z), $"{f.name} not under furniture");
                foreach (var g in flowers)
                    if (g != f) Assert.Greater(Vector3.Distance(p, g.transform.position), 0.3f, $"{f.name} and {g.name} apart");
            }

            var sparkles = rainforest.GetComponentsInChildren<SparkleTwinkle>();
            Assert.AreEqual(15, sparkles.Length);
            foreach (var instrument in Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None))
                Assert.AreEqual(5, sparkles.Count(sp => sp.Instrument == instrument), $"sparkles around {instrument.name}");
        }

        [Test]
        public void RingsAreHalosTippedTowardTheSeatWithUprightCharacters()
        {
            var eye = new Vector3(0f, 1.15f, 0f);
            foreach (var orbit in Object.FindObjectsByType<OrbitView>(FindObjectsSortMode.None))
            {
                var t = orbit.transform;
                Assert.AreEqual(OrbitView.HaloTilt, Vector3.Angle(t.up, new Vector3(t.up.x, 0f, t.up.z)), 0.5f, $"{orbit.name} tipped {OrbitView.HaloTilt} degrees from flat");
                var away = t.position - eye;
                away.y = 0f;
                var marker = new Vector3(t.up.x, 0f, t.up.z);
                Assert.Greater(Vector3.Dot(marker.normalized, away.normalized), 0.99f, $"{orbit.name}: 12 o'clock on the far side, as the seated player sees it");
                foreach (var mini in orbit.GetComponentsInChildren<EchoMini>())
                {
                    Assert.Greater(Vector3.Dot(mini.transform.up, Vector3.up), 0.999f, $"{mini.name} stands upright");
                    var facing = -mini.transform.forward;
                    Assert.Less(Vector3.Dot(new Vector3(facing.x, 0f, facing.z).normalized, away.normalized), -0.99f, $"{mini.name} faces the player");
                }
            }
        }

        [Test]
        public void ThrowableBallsWearTheCharacterFace()
        {
            foreach (var ball in Object.FindObjectsByType<Ball>(FindObjectsSortMode.None))
            {
                Assert.IsNotNull(ball.Rim, $"{ball.name} rim");
                Assert.AreEqual(4, ball.FaceParts.Count, $"{ball.name} eyes, smile and the scared oh-mouth");
                var oh = ball.FaceParts[3].bounds;
                var look = ball.transform.Find("Visual").GetComponent<Renderer>().bounds;
                var inner = new Bounds(look.center, look.size * 0.85f);
                Assert.IsTrue(inner.Contains(oh.min) && inner.Contains(oh.max), $"{ball.name} oh-mouth parked inside the body");
                Assert.AreEqual("Bop_Body", ball.transform.Find("Visual").GetComponent<MeshFilter>().sharedMesh.name);
                Assert.IsNull(ball.GetComponent<MeshRenderer>(), $"{ball.name}: the look lives on the wobbling child only");
                var faceColor = ball.FaceParts[0].sharedMaterial.GetColor("_BaseColor");
                var expected = OrbCatalog.Get(ball.Orb).Face;
                Assert.Less(Mathf.Abs(faceColor.r - expected.R) + Mathf.Abs(faceColor.g - expected.G) + Mathf.Abs(faceColor.b - expected.B), 1e-3f,
                    $"{ball.name} authored with its character's face colour, so startup changes nothing");
            }
        }

        [Test]
        public void UndoButtonReachableSeated()
        {
            var undo = Object.FindFirstObjectByType<UndoButton>();
            Assert.IsNotNull(undo);
            AssertSeated("UndoButton", undo.transform.position);
        }

        [Test]
        public void SelectAndPlaceIsWired()
        {
            var selector = Object.FindFirstObjectByType<OrbSelector>();
            Assert.IsNotNull(selector, "OrbSelector in scene");
            var controller = Object.FindFirstObjectByType<ControllerButtonInput>();
            Assert.IsNotNull(controller, "controller-only input in scene");
        }

        [Test]
        public void SelectedTokenAuthoredLargerSoStartupChangesNothing()
        {
            var flutter = Object.FindObjectsByType<OrbToken>(FindObjectsSortMode.None).Single(t => t.Orb == OrbType.Flutter);
            Assert.IsTrue(flutter.IsSelected, "start orb is authored selected");
            foreach (var other in Object.FindObjectsByType<OrbToken>(FindObjectsSortMode.None).Where(t => t.Orb != OrbType.Flutter))
                Assert.IsFalse(other.IsSelected);
        }

        [Test]
        public void PoolBallsPreSkinnedAsStartOrb()
        {
            var balls = Object.FindObjectsByType<Ball>(FindObjectsSortMode.None);
            Assert.Greater(balls.Length, 0);
            foreach (var b in balls) Assert.AreEqual(OrbType.Flutter, b.Orb, b.name);
        }
    }
}
