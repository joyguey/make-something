using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// Owner, headset 2026-11-08: "I CAN'T REACH ANYTHING, PLEASE REDO THE HEIGHT". The owner's log: eyes 1.22 m up, a desk
    /// 0.79 m high starting 0.24 m out, a monitor and things on it at about 1.0 m. The leaf and the ball on its cloud are
    /// placed from the head, within a forearm's reach, just above the desk itself (not the things on it).
    /// </summary>
    public class ReachPlayModeTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";
        static readonly Vector3 Head = new(0f, 1.22f, 0f);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            Object.FindFirstObjectByType<IntroSequence>().Finish();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            var empty = SceneManager.CreateScene($"ReachEmpty{Time.frameCount}");
            SceneManager.SetActiveScene(empty);
            var real = SceneManager.GetSceneByPath(ScenePath);
            if (real.isLoaded) yield return SceneManager.UnloadSceneAsync(real);
        }

        static Mesh Flat(float w, float d, float step = 0.05f)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            int nx = Mathf.RoundToInt(w / step), nz = Mathf.RoundToInt(d / step);
            for (int z = 0; z <= nz; z++) for (int x = 0; x <= nx; x++) verts.Add(new Vector3(-w / 2f + x * step, 0f, -d / 2f + z * step));
            for (int z = 0; z < nz; z++) for (int x = 0; x < nx; x++)
            {
                int a = z * (nx + 1) + x, b = a + 1, c = a + nx + 1, e = c + 1;
                tris.AddRange(new[] { a, c, b, b, c, e });
            }
            var m = new Mesh { vertices = verts.ToArray(), triangles = tris.ToArray() };
            m.RecalculateNormals();
            return m;
        }

        static Bounds LeafBody() => GameObject.Find("RoomOrchestra").GetComponentInChildren<IntroSequence>().Leaf.Find("Leaf").GetComponent<Renderer>().bounds;

        static void AssertReachable(string what, Vector3 p, float reach = 0.75f) =>
            Assert.Less(Vector3.Distance(p, Head + new Vector3(0f, -0.2f, 0f)), reach, $"{what} at {p:F2} is within reach of the shoulders");

        [UnityTest]
        public IEnumerator WithoutADeskTheLeafAndBallAreAForearmAway()
        {
            Object.FindFirstObjectByType<RoamingTray>().FitForTest(Head);
            yield return null;
            var leaf = LeafBody();
            Assert.AreEqual(Head.y - RoamingTray.LeafBelowEyes, leaf.max.y, 0.05f, "the leaf's top 40 cm below the eyes");
            Assert.AreEqual(RoamingTray.ReachOut, leaf.center.z, 0.08f, "a forearm out");
            AssertReachable("the leaf", leaf.center);
            var launcher = Object.FindFirstObjectByType<BallLauncher>();
            var ball = launcher.ReadyBall.transform.position;
            var stage = GameObject.Find("RoomOrchestra").transform;
            Assert.AreEqual(leaf.max.y + RoamingTray.BallAboveLeaf, ball.y, 0.06f, $"the ball a little above the leaf (spawn {launcher.SpawnPosition:F2}, ball {ball:F2}, stage {stage.position:F2} {stage.eulerAngles:F0}, cloud {stage.Find("BallDispenser").position:F2})");
            AssertReachable("the ball", ball);
            AssertCloudApart(leaf);
            foreach (var b in Object.FindObjectsByType<UndoButton>(FindObjectsSortMode.None)) AssertReachable("Undo", b.transform.position);
        }

        // Owner, headset 2026-11-08, standing (eyes 1.52 m): the leaf sat right under the face; "the cloud needs to move
        // with the leaf, but should be separate, maybe slightly higher than the height of the leaf".
        [UnityTest]
        public IEnumerator StandingTheLeafIsLowerAndFurtherAndTheCloudSitsApartJustAboveIt()
        {
            var head = new Vector3(0f, 1.52f, 0f);
            Object.FindFirstObjectByType<RoamingTray>().FitForTest(head);
            yield return null;
            var leaf = LeafBody();
            Assert.AreEqual(1.52f - RoamingTray.StandingLeafBelowEyes, leaf.max.y, 0.05f, "near elbow height, not under the chin");
            Assert.AreEqual(RoamingTray.StandingReachOut, leaf.center.z, 0.08f, "a little further out");
            var ball = Object.FindFirstObjectByType<BallLauncher>().ReadyBall.transform.position;
            Assert.Greater(ball.y, leaf.max.y + 0.08f, "the ball a little above the leaf");
            Assert.Less(ball.y, leaf.max.y + 0.25f, "only a little");
            AssertCloudApart(leaf);
            var shoulder = head + new Vector3(0.18f, -0.25f, 0f);
            Assert.Less(Vector3.Distance(ball, shoulder), 0.75f, "the ball within reach of the right shoulder");
            Assert.Less(Vector3.Distance(leaf.center, head + new Vector3(0f, -0.25f, 0f)), 0.8f, "the leaf within reach");
        }

        static void AssertCloudApart(Bounds leaf)
        {
            var cloud = GameObject.Find("RoomOrchestra").transform.Find("BallDispenser/CloudPuffs");
            var puffs = cloud.GetComponentsInChildren<Renderer>();
            float cloudLeft = float.MaxValue;
            foreach (var r in puffs) cloudLeft = Mathf.Min(cloudLeft, r.bounds.min.x);
            Assert.Greater(cloudLeft, leaf.max.x - 0.08f, $"the cloud ({cloudLeft:F2}) is beside the leaf (right edge {leaf.max.x:F2}), not over it");
        }

        sealed class FakePointers : IPointerSource
        {
            public readonly PointerFrame[] Frames = new PointerFrame[2];
            public PointerFrame Read(int hand) => Frames[hand];
        }

        // Owner, headset 2026-11-08: "make the leaf grabbable from the side so if I try and grab it with my full hand or
        // controller, I can adjust it to where the location makes sense and the cloud will keep its position relative to
        // the leaf".
        [UnityTest]
        public IEnumerator AGripAtTheLeafsEdgeMovesItAndTheCloudKeepsItsPlaceBesideIt()
        {
            var tray = Object.FindFirstObjectByType<RoamingTray>();
            tray.FitForTest(Head);
            yield return null;
            var pointers = new FakePointers();
            Object.FindFirstObjectByType<InstrumentInputRouter>().Pointers = pointers;
            var rig = XrRig.Instance != null ? XrRig.Instance.transform : null;
            Vector3 Local(Vector3 world) => rig != null ? rig.InverseTransformPoint(world) : world;
            var cloud = GameObject.Find("RoomOrchestra").transform.Find("BallDispenser");
            var leaf = LeafBody();
            var cloudFromLeaf = cloud.position - leaf.center;
            var edge = new Vector3(leaf.min.x - 0.05f, leaf.center.y, leaf.center.z); // just off its left side

            // a grip far from the leaf does nothing
            pointers.Frames[0] = new PointerFrame { Grip = true, GripPosition = Local(edge + new Vector3(-0.6f, 0f, 0f)) };
            yield return null;
            Assert.IsFalse(tray.Grabbing, "a grip away from the leaf doesn't take it");
            pointers.Frames[0] = default;
            yield return null;

            pointers.Frames[0] = new PointerFrame { Grip = true, GripPosition = Local(edge) };
            yield return null;
            Assert.IsTrue(tray.Grabbing, "a grip at its edge takes hold");
            var move = new Vector3(-0.2f, 0.1f, 0.15f);
            pointers.Frames[0] = new PointerFrame { Grip = true, GripPosition = Local(edge + move) };
            yield return null;
            var moved = LeafBody();
            Assert.Less(Vector3.Distance(moved.center, leaf.center + move), 0.02f, "the leaf follows the hand");
            Assert.Less(Vector3.Distance(cloud.position - moved.center, cloudFromLeaf), 0.02f, "the cloud keeps its place beside the leaf");
            var ball = Object.FindFirstObjectByType<BallLauncher>();
            Assert.Less(Vector3.Distance(ball.ReadyBall.transform.position, ball.SpawnPosition.Value), 0.02f, "the ball rides on its cloud");

            pointers.Frames[0] = default; // let go
            yield return null;
            Assert.IsFalse(tray.Grabbing);
            Assert.IsTrue(tray.HandPlaced, "it stays where it was put");
            yield return new WaitForSeconds(0.5f);
            Assert.Less(Vector3.Distance(LeafBody().center, leaf.center + move), 0.02f, "and doesn't drift back");

            tray.Call(); // Come here: back to following and fitting the player
            Assert.IsFalse(tray.HandPlaced, "Come here brings it back to the player");
        }

        [UnityTest]
        public IEnumerator OverTheOwnersDeskBothSitJustAboveItNotOnTheMonitor()
        {
            var walls = RealWalls.Instance;
            walls.SetSurface("desk", Flat(1.2f, 0.96f), new Vector3(-0.12f, 0.79f, 0.72f), Quaternion.identity, Vector3.one);
            walls.SetSurface("monitor", Flat(0.5f, 0.2f), new Vector3(-0.1f, 1.05f, 0.95f), Quaternion.identity, Vector3.one);
            Physics.SyncTransforms();
            Object.FindFirstObjectByType<RoamingTray>().FitForTest(Head);
            yield return null;
            var leaf = LeafBody();
            Assert.Greater(leaf.min.y, 0.79f, "the leaf is above the desk");
            Assert.Less(leaf.min.y, 0.90f, "just above it, not up on the monitor");
            AssertReachable("the leaf", leaf.center);
            var ball = Object.FindFirstObjectByType<BallLauncher>().ReadyBall.transform.position;
            Assert.GreaterOrEqual(ball.y, 0.79f + RoamingTray.BallAboveDesk - 0.02f, "the cloud clears the desk");
            AssertReachable("the ball", ball);
        }
    }
}
