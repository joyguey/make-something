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
                    if (r.transform.localScale.sqrMagnitude < 1e-4f) continue; // hidden focus markers
                    if (r.bounds.IntersectRay(ray, out float distance) && distance < toTarget.magnitude)
                        Assert.Fail($"{r.name} blocks the view of {target.name} from eye height {eyeHeight} m");
                }
            }
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
