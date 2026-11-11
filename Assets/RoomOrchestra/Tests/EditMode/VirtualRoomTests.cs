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
            // the leaf waits out of sight until Start (owner, headset 2026-11-08), far above the room
            var intro = Object.FindFirstObjectByType<IntroSequence>();
            Assert.IsTrue(intro.LeafHidden, "the leaf is out of sight at launch");
            Assert.Greater(intro.Leaf.position.y, 50f);
            var stage = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => !r.transform.IsChildOf(room.transform) && !r.transform.IsChildOf(intro.Leaf)
                            && r.transform.root.name == "RoomOrchestra" && r.bounds.size.sqrMagnitude > 0f);
            foreach (var r in stage)
                Assert.IsTrue(inside.Contains(r.bounds.center), $"{r.name} at {r.bounds.center} is outside the virtual room");
            Assert.IsTrue(inside.Contains(new Vector3(0f, 1.15f, 0f)), "the seat is inside");
            // V3 review: a meadow instead of a box: grass, flowers, trees, hills, clouds and sky, inside invisible fences
            Assert.AreEqual(40, room.Panels.Count(p => p.name.StartsWith("Wall")), "4 invisible fences of 10");
            Assert.AreEqual(28, room.Panels.Count(p => p.name.StartsWith("GroundS")), "grass underfoot: the floor's ring sectors");
            foreach (var kind in new[] { "Tree", "Hill", "Flowers", "Bush", "Cloud", "SkyWedge" })
                Assert.IsTrue(room.Panels.Any(p => p.name.StartsWith(kind)), $"the meadow has {kind}");
        }

        [Test]
        public void WallsAreSolidToBallsButCatchNoTapsAndCastNoShadows()
        {
            foreach (var panel in Room.Panels)
            {
                bool fence = panel.name.StartsWith("Wall");
                Assert.AreEqual(fence, panel.GetComponent<Collider>() != null, $"{panel.name}: only the invisible fences stop balls");
                Assert.AreEqual(fence, panel.GetComponent<BounceSurface>() != null, $"{panel.name}: a ball bounces off a fence with its own sound");
                Assert.AreEqual(!fence, panel.GetComponent<MeshRenderer>() != null, $"{panel.name}: the fences are not drawn; the meadow is");
                Assert.IsFalse(InstrumentInputRouter.Activate(panel.gameObject), $"a tap on {panel.name} does nothing");
                Assert.IsNull(InstrumentInputRouter.PressableOwner(panel.gameObject), $"{panel.name}: the pointer's ray stays plain over it");
                if (panel.TryGetComponent<MeshRenderer>(out var look)) Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, look.shadowCastingMode, panel.name);
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
                if (panel.TryGetComponent<Renderer>(out var look)) Assert.IsFalse(look.enabled, $"{panel.name} is not drawn once gone");
                Assert.IsFalse(panel.TryGetComponent<Collider>(out var c) && c.enabled, $"{panel.name} catches nothing in the cloud");
            }
        }
    }
}
