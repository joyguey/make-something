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
        public void OpenScene() => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

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
                Assert.IsNotNull(t.GetComponent<Collider>(), $"{t.name} must be tappable");
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
                    string expected = note.IsPercussion ? note.Articulation.ToString() : NoteMapping.NoteName(i);
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
            CollectionAssert.Contains(Texts(Object.FindFirstObjectByType<TierButton>()), "Loudness");
            CollectionAssert.Contains(Texts(Object.FindFirstObjectByType<UndoButton>()), "Undo");
            foreach (var column in Object.FindObjectsByType<PitchColumn>(FindObjectsSortMode.None))
                Assert.IsTrue(Texts(column).Any(t => t == "udu" || t == "mbira" || t == "erhu"), $"{column.name} names its instrument");
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                Assert.IsNotNull(text.font, text.name);
                Assert.IsInstanceOf<TextMeshPro>(text, "world-space TMP (PICO Spatial syncs TMP, not TextMesh)");
                text.ForceMeshUpdate(true, true);
                Assert.Greater(text.textBounds.size.y, 0.015f, $"{text.transform.parent.name} is readable size");
                Assert.Less(text.transform.localPosition.sqrMagnitude, 1e-8f, "text sits at its holder's origin");
                Assert.IsFalse(string.IsNullOrWhiteSpace(text.text), text.name);
                var toLabel = text.transform.position - new Vector3(0f, 1.15f, 0f);
                Assert.Greater(Vector3.Dot(text.transform.forward, toLabel.normalized), 0.5f, $"{text.name} faces the player");
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
        public void TierButtonAndBallSpawnReachableSeated()
        {
            var tier = Object.FindFirstObjectByType<TierButton>();
            Assert.IsNotNull(tier);
            AssertSeated("TierButton", tier.transform.position);
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
            var renderers = root.GetComponentsInChildren<Renderer>();
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
            // EditorOnly objects are stripped from the player but still exported by SpatialAdapter, which
            // shifted every later object by one on the emulator (wrong meshes, colours and positions).
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
                Assert.IsTrue(minis.All(m => m.Orbit == orbit && m.GetComponent<Collider>() != null), "minis are tappable and wired");
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
                Assert.IsNotNull(b.GetComponent<Collider>(), "tappable");
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
            Assert.IsNotNull(button.GetComponent<Collider>(), "tappable");
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
