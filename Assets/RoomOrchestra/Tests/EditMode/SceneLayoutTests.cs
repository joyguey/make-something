using System.Linq;
using NUnit.Framework;
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
        public void PetalButtonReachableSeatedAndAuthoredAtStartPetal()
        {
            var petal = Object.FindFirstObjectByType<PetalButton>();
            Assert.IsNotNull(petal);
            AssertSeated("PetalButton", petal.transform.position);
            Assert.IsNotNull(petal.GetComponent<Collider>());
            var steps = petal.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("PetalStep")).ToArray();
            Assert.AreEqual(NoteMapping.Count, steps.Length);
            Assert.Greater(steps[2].localScale.y, petal.BaseScale(2).y, "middle petal authored selected");
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
                foreach (var r in renderers)
                {
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
