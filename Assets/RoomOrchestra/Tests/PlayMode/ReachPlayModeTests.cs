using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// Owner, headset 2026-11-08: "I CAN'T REACH ANYTHING, PLEASE REDO THE HEIGHT". Her log: eyes 1.22 m up, a desk
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
            Assert.AreEqual(Head.y - RoamingTray.BallBelowEyes, ball.y, 0.06f, $"the ball 30 cm below the eyes (spawn {launcher.SpawnPosition:F2}, ball {ball:F2}, stage {stage.position:F2} {stage.eulerAngles:F0}, cloud {stage.Find("BallDispenser").position:F2})");
            AssertReachable("the ball", ball);
            foreach (var b in Object.FindObjectsByType<UndoButton>(FindObjectsSortMode.None)) AssertReachable("Undo", b.transform.position);
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
