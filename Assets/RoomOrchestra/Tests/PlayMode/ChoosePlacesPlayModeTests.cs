using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// Owner, headset 2026-11-08: "Shouldn't there be a visual that shows me when the room is being mapped and where I
    /// can actually place and assign ... instead of having it auto-detect". After the lesson the scanned room glows, a
    /// spot follows the controller's ray on it, a press puts the instrument there, and a press on the instrument keeps
    /// it on its virtual furniture. Driven through the router with a pretend controller, as on the headset.
    /// </summary>
    public class ChoosePlacesPlayModeTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";
        static readonly Vector3 DeskTop = new(-0.9f, 0.75f, 1.9f);

        sealed class FakePointers : IPointerSource
        {
            public readonly PointerFrame[] Frames = new PointerFrame[2];
            public PointerFrame Read(int hand) => Frames[hand];
        }

        FakePointers m_Pointers;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
            RealFurniture.Enabled = false; // until the pretend room is in place
            RealFurniture.AutoPlace = false;
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            Object.FindFirstObjectByType<IntroSequence>().Finish(); // free play
            m_Pointers = new FakePointers();
            Object.FindFirstObjectByType<InstrumentInputRouter>().Pointers = m_Pointers;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            RealFurniture.Enabled = true;
            var empty = SceneManager.CreateScene($"ChooseEmpty{Time.frameCount}");
            SceneManager.SetActiveScene(empty);
            var real = SceneManager.GetSceneByPath(ScenePath);
            if (real.isLoaded) yield return SceneManager.UnloadSceneAsync(real);
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
        }

        // A flat 1 m square, as the scan gives a desk top.
        static Mesh Square()
        {
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) },
                triangles = new[] { 0, 2, 1, 0, 3, 2 }
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void Point(Vector3 from, Vector3 at, bool press)
        {
            m_Pointers.Frames[1] = new PointerFrame
            {
                Tracked = true,
                Position = from,
                Rotation = Quaternion.LookRotation(at - from),
                Select = press
            };
        }

        IEnumerator Press(Vector3 from, Vector3 at)
        {
            Point(from, at, false);
            yield return null;
            Point(from, at, true);
            yield return null;
            Point(from, at, false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheRoomGlowsASpotFollowsTheRayAndAPressPutsTheInstrumentThere()
        {
            var walls = RealWalls.Instance;
            Assert.IsNotNull(walls);
            walls.SetSurface("desk", Square(), DeskTop, Quaternion.identity, Vector3.one);
            walls.SetSurface("near", Square(), new Vector3(0f, 0.75f, 0.8f), Quaternion.identity, Vector3.one); // the player's own desk
            var placer = RealFurniture.Instance;
            RealFurniture.Enabled = true;
            var room = VirtualRoom.Instance;
            room.TearDown();
            float until = Time.time + room.TearDownSeconds + 3f;
            while (!placer.Choosing && Time.time < until) yield return null;
            Assert.IsTrue(placer.Choosing, "after the lesson, the player chooses");
            yield return null;
            Assert.IsFalse(RealWalls.Occlude, "while the room glows the shield is off (it hid the glow)");
            var shield = walls.transform.Find("RoomSurface_desk/RoomOccluder_desk");
            Assert.IsNotNull(shield);
            Assert.AreEqual(2, shield.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3, "the far desk hides what is behind it");
            var nearShield = walls.transform.Find("RoomSurface_near/RoomOccluder_near").GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(0, nearShield.triangles.Length, "the player's own desk never hides the leaf in front of them");
            Assert.IsTrue(RealWalls.ShowScan, "the scanned room is shown");
            var surface = walls.transform.Find("RoomSurface_desk");
            Assert.IsTrue(surface.GetComponent<MeshRenderer>().enabled, "and glows");
            Assert.AreEqual(RealWalls.SurfaceLayer, surface.gameObject.layer);

            // pointing at the desk: a spot shows where the first instrument would go
            var first = placer.ChoosingTarget;
            Assert.IsNotNull(first);
            var hand = new Vector3(-0.4f, 1.2f, 0.9f);
            Point(hand, DeskTop, false);
            yield return null;
            yield return null;
            var cursor = placer.SpotAt(0);
            Assert.IsTrue(cursor.Shown, "the spot follows the ray");
            Assert.Less(Vector3.Distance(cursor.transform.position, DeskTop), 0.05f, "where the ray touches the desk");
            Assert.AreEqual(first.Instrument.ToString().ToLowerInvariant(), cursor.Text.ToLowerInvariant(), "named for the instrument");

            // a press puts it there
            yield return Press(hand, DeskTop);
            Assert.AreEqual(1, placer.MatchedCount);
            Assert.AreNotEqual(first, placer.ChoosingTarget, "the next instrument's turn");
            yield return new WaitForSeconds(RealFurniture.MoveSeconds + 0.2f);
            Assert.Less(Vector3.Distance(first.AimPosition, DeskTop + Vector3.up * 0.06f), 0.1f, "it aims at the desk top now");

            // a press on the next instrument itself keeps it on its virtual furniture; the last too
            for (int i = 0; i < 2; i++)
            {
                var next = placer.ChoosingTarget;
                Assert.IsNotNull(next);
                Collider tap = null;
                foreach (var c in next.GetComponentsInChildren<Collider>())
                    if (c.enabled && InstrumentInputRouter.PressableOwner(c.gameObject) == next) { tap = c; break; }
                Assert.IsNotNull(tap, $"{next.name} can be tapped");
                var eye = new Vector3(0f, 1.15f, 0f);
                var body = tap.bounds.center;
                InstrumentInputRouter.Aim(new Ray(eye, body - eye), out var seen);
                Assert.AreEqual(next, InstrumentInputRouter.PressableOwner(seen.collider.gameObject),
                    $"the ray from the seat to {next.name} touches {seen.collider.name} first");
                yield return Press(eye, body);
                yield return new WaitForSeconds(0.3f); // past the double-tap guard
            }
            Assert.AreEqual(RealFurniture.Phase.Placed, placer.State, "all three chosen");
            Assert.AreEqual(1, placer.MatchedCount, "one on the desk, two kept virtual");
            Assert.IsFalse(RealWalls.ShowScan, "the room stops glowing");
            yield return null;
            Assert.IsTrue(RealWalls.Occlude, "and now real walls and furniture hide what is behind them");
            Assert.IsTrue(shield.GetComponent<MeshRenderer>().enabled);
            Assert.IsFalse(cursor.Shown);
        }
    }
}
