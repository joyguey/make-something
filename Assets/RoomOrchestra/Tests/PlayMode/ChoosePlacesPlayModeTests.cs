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
        static readonly Vector3 DeskTop = new(1.5f, 0.72f, -0.4f); // a real desk to the player's right, clear of the virtual furniture

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
            RealHomeStore.Clear();
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
            OrbSelector.ThrowOnTap = false;
            RealHomeStore.Clear();
            var empty = SceneManager.CreateScene($"ChooseEmpty{Time.frameCount}");
            SceneManager.SetActiveScene(empty);
            var real = SceneManager.GetSceneByPath(ScenePath);
            if (real.isLoaded) yield return SceneManager.UnloadSceneAsync(real);
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
        }

        // A desk top as the scan gives it: a flat 1 m x 0.6 m grid of small triangles (scene space), and its mesh.
        static (Mesh mesh, Vector3[] world) DeskTopScan(Vector3 at, float width = 1f, float depth = 0.6f, float step = 0.05f)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            int nx = Mathf.RoundToInt(width / step), nz = Mathf.RoundToInt(depth / step);
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                    verts.Add(new Vector3(-width / 2f + x * step, 0f, -depth / 2f + z * step));
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    int a = z * (nx + 1) + x, b = a + 1, c = a + nx + 1, d = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }
            var mesh = new Mesh { vertices = verts.ToArray(), triangles = tris.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var world = new Vector3[tris.Count];
            for (int i = 0; i < tris.Count; i++) world[i] = at + verts[tris[i]];
            return (mesh, world);
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

        static bool Drawn(InstrumentTarget t)
        {
            foreach (var r in t.transform.root.Find("Furniture")?.Find(t.name)?.GetComponentsInChildren<Renderer>() ?? t.GetComponentsInChildren<Renderer>())
                if (r.enabled) return true;
            return false;
        }

        // Owner, headset 2026-11-08: the instruments are asked for in the lesson's order; pointing lights up the real
        // object; a press moves the instrument onto it, its virtual furniture goes and the object's own shape takes its
        // hits; its notes then run along the object's longest side, and pressing a band plays that note.
        [UnityTest]
        public IEnumerator TheRoomGlowsAndEachInstrumentMovesOntoTheRealObjectPointedAt()
        {
            var walls = RealWalls.Instance;
            Assert.IsNotNull(walls);
            var (mesh, world) = DeskTopScan(DeskTop);
            walls.SetSurface("desk", mesh, DeskTop, Quaternion.identity, Vector3.one);
            walls.SetPiece(default, world);
            var near = DeskTopScan(new Vector3(0f, 0.75f, 0.8f), 0.6f, 0.4f, 0.1f); // the player's own desk
            walls.SetSurface("near", near.mesh, new Vector3(0f, 0.75f, 0.8f), Quaternion.identity, Vector3.one);
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
            Assert.Greater(shield.GetComponent<MeshFilter>().sharedMesh.triangles.Length, 0, "the far desk hides what is behind it");
            var nearShield = walls.transform.Find("RoomSurface_near/RoomOccluder_near").GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(0, nearShield.triangles.Length, "the player's own desk never hides the leaf in front of them");
            Assert.IsTrue(RealWalls.ShowScan, "the scanned room is shown");
            Assert.IsTrue(walls.transform.Find("RoomSurface_desk").GetComponent<MeshRenderer>().enabled, "and glows");
            Assert.IsFalse(Object.FindObjectsByType<PitchColumn>(FindObjectsSortMode.None).Length > 0, "the lesson's note columns are put away");

            // the lesson's order: the chair's mbira first
            var first = placer.ChoosingTarget;
            Assert.AreEqual(InstrumentType.Mbira, first.Instrument, "the chair's mbira is asked for first");
            Assert.AreEqual("Chair", first.name);
            var hand = new Vector3(0.3f, 1.2f, 0.2f);
            Point(hand, DeskTop, false);
            yield return null;
            yield return null;
            var preview = GameObject.Find("ChoosingPreview");
            Assert.IsNotNull(preview, "the object pointed at glows");
            Assert.IsTrue(preview.activeSelf);
            Assert.Greater(preview.GetComponent<MeshFilter>().sharedMesh.vertexCount, 100, "in its own scanned shape");
            var cursor = placer.SpotAt(0);
            Assert.IsTrue(cursor.Shown, "with the instrument's name over it");
            Assert.AreEqual(first.Instrument.ToString().ToLowerInvariant(), cursor.Text.ToLowerInvariant());

            // a press: the mbira lives on the desk; the virtual chair is gone
            yield return Press(hand, DeskTop);
            Assert.AreEqual(1, placer.MatchedCount);
            Assert.AreEqual(InstrumentType.Erhu, placer.ChoosingTarget.Instrument, "then the lamp's erhu");
            var home = first.Home;
            Assert.IsNotNull(home, "the mbira has a real home");
            Assert.IsTrue(home.HasShape, "the desk's scanned shape takes its hits");
            Assert.AreEqual(1f, home.WorldBounds.size.x, 0.1f, "as wide as the desk");
            Assert.AreEqual(DeskTop.y, home.WorldBounds.size.y, 0.08f, "standing on the floor up to the desk's top");
            Assert.IsFalse(placer.IsRemoved(first) == false, "its virtual furniture is gone");
            Assert.IsFalse(Drawn(first), "and not drawn");
            yield return null;
            Assert.Less(Vector3.Distance(first.AimPosition, DeskTop + Vector3.up * 0.06f), 0.1f, "balls aim at the desk's top");
            var vine = JungleDirector.Instance.transform.Find("Vine_Chair");
            var flat = vine.position - DeskTop;
            flat.y = 0f;
            Assert.Less(flat.magnitude, 1f, "its vine grows around the desk now");

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
                Assert.IsTrue(Drawn(next), $"{next.name} stays");
                Assert.IsNotNull(next.Home, $"its notes run along {next.name} too");
            }
            Assert.AreEqual(RealFurniture.Phase.Placed, placer.State, "all three chosen");
            Assert.AreEqual(1, placer.MatchedCount, "one on the desk, two kept virtual");
            Assert.IsFalse(RealWalls.ShowScan, "the room stops glowing");
            yield return null;
            Assert.IsTrue(RealWalls.Occlude, "and now real walls and furniture hide what is behind them");
            Assert.IsTrue(shield.GetComponent<MeshRenderer>().enabled);
            Assert.IsFalse(cursor.Shown);

            // the notes run along the desk's long side: pointing at either end lights the lowest or highest band
            var left = DeskTop + new Vector3(-0.45f, 0f, 0f);
            var right = DeskTop + new Vector3(0.45f, 0f, 0f);
            Point(hand, left, false);
            yield return null;
            int leftBand = home.Hover;
            Assert.IsTrue(home.BandsShown, "pointing shows the five bands");
            Point(hand, right, false);
            yield return null;
            int rightBand = home.Hover;
            CollectionAssert.AreEquivalent(new[] { 0, 4 }, new[] { leftBand, rightBand }, "one end is the lowest note, the other the highest");
            // owner, headset 2026-11-08: "when I point at the partition, can the note be clearly labeled?" every band
            // says its note, the pointed one biggest
            for (int i = 0; i < RealHome.Bands; i++)
            {
                var label = home.LabelOf(i);
                Assert.IsTrue(label.gameObject.activeInHierarchy, $"band {i} says its note");
                Assert.AreEqual(UiText.Tile(new MusicalNote(home.Target.Instrument, i), Localization.Current), label.text);
            }
            Assert.Greater(home.LabelOf(rightBand).transform.lossyScale.x, home.LabelOf(leftBand).transform.lossyScale.x * 1.5f, "the pointed band's label is the biggest");
            Assert.IsNotNull(home.LabelOf(rightBand).transform.Find("NoteLabelBacking"), "on a dark backing that reads against the room");
            int before = EchoConductor.Instance.Echoes.Count;
            OrbSelector.ThrowOnTap = false;
            yield return Press(hand, right);
            Assert.AreEqual(before + 1, EchoConductor.Instance.Echoes.Count, "a press on the desk plays and keeps a note, no throw needed");
            Assert.AreEqual(rightBand, EchoConductor.Instance.Echoes[EchoConductor.Instance.Echoes.Count - 1].NoteIndex, "the band pressed");
        }

        // Choosing again (Menu: Places) puts everything back and asks again from the first instrument.
        [UnityTest]
        public IEnumerator PlacesChoosesAgainFromTheStart()
        {
            var walls = RealWalls.Instance;
            var (mesh, world) = DeskTopScan(DeskTop);
            walls.SetSurface("desk", mesh, DeskTop, Quaternion.identity, Vector3.one);
            walls.SetPiece(default, world);
            var placer = RealFurniture.Instance;
            RealFurniture.Enabled = true;
            VirtualRoom.Instance.HideNow();
            float until = Time.time + 3f;
            while (!placer.Choosing && Time.time < until) yield return null;
            Assert.IsTrue(placer.Choosing);
            var first = placer.ChoosingTarget;
            yield return Press(new Vector3(0.3f, 1.2f, 0.2f), DeskTop);
            Assert.IsNotNull(first.Home);
            Assert.IsFalse(Drawn(first));
            Object.FindFirstObjectByType<PlacesButton>().Press();
            yield return null;
            Assert.IsTrue(placer.Choosing, "choosing again");
            Assert.AreEqual(first, placer.ChoosingTarget, "from the first instrument");
            Assert.IsTrue(Drawn(first), "its virtual furniture is back meanwhile");
            Assert.IsNull(first.Home);
        }
    }
}
