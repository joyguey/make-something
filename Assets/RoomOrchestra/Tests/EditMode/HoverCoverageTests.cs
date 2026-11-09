using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// Everything you can tap is something you can see and point at (George, 2026-11-03: "highlight buttons and other
    /// objects when I target them so I know what I can pinch"). In PICO XR mode the controller's ray turns gold over
    /// any of them (InstrumentInputRouter), so every tap collider must sit on a visible mesh.
    /// </summary>
    public class HoverCoverageTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";

        [OneTimeSetUp]
        public void OpenScene() => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        [Test]
        public void EveryTapColliderIsOnAVisiblePart()
        {
            var missing = new List<string>();
            int checkedCount = 0;
            foreach (var collider in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Component owner = InstrumentInputRouter.PressableOwner(collider.gameObject);
                if (owner == null) owner = collider.GetComponentInParent<Ball>(); // the ball you pinch and throw
                if (owner == null) continue;
                if (owner is Ball && !collider.isTrigger) continue; // its physics collider: the body's shell takes hover
                if (collider.isTrigger && !(owner is Ball)) continue;
                checkedCount++;
                var go = collider.gameObject;
                var renderer = go.GetComponent<MeshRenderer>();
                // a control tucked away for the lesson is not drawn (nor tappable) until it pops up: its mesh is what counts
                var tuck = go.GetComponentInParent<TuckAway>();
                bool shown = renderer != null && (renderer.enabled || (tuck != null && tuck.Tucked));
                bool visible = shown && go.GetComponent<MeshFilter>()?.sharedMesh != null;
                if (!visible)
                    missing.Add($"{Path(go.transform)} (owner {owner.GetType().Name}: no visible mesh)");
            }
            Assert.Greater(checkedCount, 50, "the scene's tap colliders were found");
            Assert.IsEmpty(missing, "tap colliders on nothing visible:\n" + string.Join("\n", missing.Distinct()));
        }

        [Test]
        public void EveryBallHasAHoverShellThatLeavesPhysicsAlone()
        {
            var balls = Object.FindObjectsByType<Ball>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.IsNotEmpty(balls);
            foreach (var ball in balls)
            {
                var solid = ball.GetComponent<Collider>();
                Assert.IsNotNull(solid, $"{ball.name}: its physics collider stays on the root");
                Assert.IsFalse(solid.isTrigger, ball.name);
                var shells = ball.GetComponentsInChildren<Collider>(true).Where(c => c != solid).ToArray();
                Assert.AreEqual(1, shells.Length, $"{ball.name}: one hover shell");
                Assert.IsTrue(shells[0].isTrigger, $"{ball.name}: the shell is a trigger, so bounces are unchanged");
            }
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
