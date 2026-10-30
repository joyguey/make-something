using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// The virtual starting room as saved: standing around the whole stage and the seat, walls solid to balls only,
    /// no shadows on the stage; skipping the opening puts it away in the cloud, safely sized.
    /// </summary>
    public class VirtualRoomTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";

        [SetUp]
        public void OpenScene() => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        static VirtualRoom Room => Object.FindFirstObjectByType<VirtualRoom>();
        static Vector3 Park => GameObject.Find("RoomOrchestra").transform.Find("BallDispenser/ParkPoint").position;

        [Test]
        public void TheRoomStandsAroundTheWholeStageAtLaunch()
        {
            var room = Room;
            Assert.IsNotNull(room);
            Assert.IsTrue(room.Standing);
            var inside = new Bounds();
            inside.SetMinMax(new Vector3(-2.55f, -0.02f, -1.55f), new Vector3(2.55f, 2.65f, 3.75f));
            var stage = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => !r.transform.IsChildOf(room.transform) && r.transform.root.name == "RoomOrchestra" && r.bounds.size.sqrMagnitude > 0f);
            foreach (var r in stage)
                Assert.IsTrue(inside.Contains(r.bounds.center), $"{r.name} at {r.bounds.center} is outside the virtual room");
            Assert.IsTrue(inside.Contains(new Vector3(0f, 1.15f, 0f)), "the seat is inside");
            Assert.AreEqual(90, room.Panels.Count, "4 walls of 10, floor and ceiling of 25");
        }

        [Test]
        public void WallsAreSolidToBallsButCatchNoTapsAndCastNoShadows()
        {
            foreach (var panel in Room.Panels)
            {
                bool floor = panel.name.StartsWith("Floor");
                Assert.AreEqual(!floor, panel.GetComponent<Collider>() != null, $"{panel.name}: walls and ceiling stop balls, the floor is the real one");
                Assert.AreEqual(!floor, panel.GetComponent<BounceSurface>() != null, $"{panel.name}: a ball bounces off it with its own sound");
                Assert.IsFalse(InstrumentInputRouter.Activate(panel.gameObject), $"a tap on {panel.name} does nothing");
                Assert.IsFalse(panel.GetComponents<MonoBehaviour>().Any(m => m != null && m.GetType().Name == "SpatialAdapterHoverEffect"), $"{panel.name} does not light up");
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, panel.GetComponent<MeshRenderer>().shadowCastingMode, panel.name);
            }
        }

        [Test]
        public void TheRippleStartsAtTheFarWallAndTakesAFewSeconds()
        {
            Assert.AreEqual(0f, VirtualRoom.DelayFor(0f));
            Assert.Greater(VirtualRoom.DelayFor(6f), VirtualRoom.DelayFor(1f), "farther panels go later");
            float last = VirtualRoom.DelayFor(Vector3.Distance(new Vector3(0f, 1.35f, 3.8f), new Vector3(-2.6f, 0f, -1.6f))) + VirtualRoom.PanelSeconds;
            Assert.That(last, Is.InRange(2f, 5f), "gentle, not abrupt, and not too long");
        }

        [Test]
        public void SkippingTheOpeningPutsTheRoomAwayInTheCloud()
        {
            Object.FindFirstObjectByType<IntroSequence>().Finish();
            var room = Room;
            Assert.IsTrue(room.Gone);
            foreach (var panel in room.Panels)
            {
                Assert.Less(Vector3.Distance(panel.position, Park), 1e-3f, panel.name);
                var s = panel.lossyScale;
                Assert.Greater(Mathf.Min(s.x, Mathf.Min(s.y, s.z)), 0.001f, $"{panel.name} is not at near-zero scale");
                Assert.Less(Mathf.Max(s.x, Mathf.Max(s.y, s.z)), 0.06f, $"{panel.name} fits inside the cloud");
                Assert.IsFalse(panel.TryGetComponent<Collider>(out var c) && c.enabled, $"{panel.name} catches nothing in the cloud");
            }
        }
    }
}
