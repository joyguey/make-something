using System.Collections;
using System.Linq;
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
            var near = DeskTopScan(new Vector3(0f, 0.45f, 0.2f), 0.4f, 0.3f, 0.1f); // the seat the player sits on
            walls.SetSurface("near", near.mesh, new Vector3(0f, 0.45f, 0.2f), Quaternion.identity, Vector3.one);
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
            Assert.AreEqual(0, nearShield.triangles.Length, "the player's own seat (and they, scanned as room) never hide the leaf in front of them");
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
            var spotIcon = cursor.GetComponentInChildren<InstrumentIcon>();
            Assert.IsNotNull(spotIcon, "and its picture (owner, 2026-11-11)");
            Assert.AreEqual(first.Instrument, spotIcon.Showing);
            Assert.AreEqual(UiText.NameAndKind(first.Instrument, UiLanguage.English, 45), cursor.Text, "its name, and what it is (V4 review)");

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

            // owner, headset 2026-11-12: a kept virtual piece can be picked up and moved somewhere easier to see
            var kept = Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None).First(t => t != first && Drawn(t));
            var keptFrom = kept.transform.position;
            var grabAt = kept.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; }).center;
            Assert.IsTrue(placer.TryBeginMove(grabAt), $"a hand on the {kept.name} takes hold of it");
            var shift = new Vector3(0.6f, 0.3f, -0.4f);
            placer.MoveTo(grabAt + shift);
            yield return null;
            Assert.AreEqual(keptFrom.x + shift.x, kept.transform.position.x, 0.02f, "it follows the hand across the floor");
            Assert.AreEqual(keptFrom.z + shift.z, kept.transform.position.z, 0.02f);
            Assert.AreEqual(keptFrom.y + RealFurniture.HoldLift, kept.transform.position.y, 0.02f, "lifted a little while held, so it shows it is taken (owner, 2026-11-22)");
            placer.EndMove();
            Assert.AreEqual(keptFrom.y, kept.transform.position.y, 0.02f, "and set back on the floor");
            Assert.IsFalse(placer.Moving);
            Assert.IsFalse(placer.TryBeginMove(new Vector3(9f, 1f, 9f)), "nothing to take far from any furniture");
            // owner, headset 2026-11-12: "do I need to walk up to it?" no: point at it from the seat and drag
            var seatEye = new Vector3(0f, 1.15f, 0f);
            var keptMiddle = kept.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; }).center;
            Assert.IsTrue(placer.TryBeginMoveByRay(new Ray(seatEye, keptMiddle - seatEye)), "pointed at from the seat, it is taken");
            var floorTarget = new Vector3(-0.8f, 0f, 1.6f);
            placer.MoveAlong(new Ray(seatEye, floorTarget - seatEye));
            var firstSpot = kept.transform.position;
            placer.MoveAlong(new Ray(seatEye, floorTarget + Vector3.right * 0.3f - seatEye));
            yield return null;
            Assert.AreEqual(0.3f, kept.transform.position.x - firstSpot.x, 0.05f, "it slides with the pointed spot on the floor");
            Assert.AreEqual(0f, kept.transform.position.z - firstSpot.z, 0.05f);
            placer.EndMove();
            Assert.Less(Mathf.Abs(kept.transform.position.y - keptFrom.y), 0.02f, "still on the floor");
            Assert.IsFalse(placer.TryBeginMoveByRay(new Ray(seatEye, Vector3.up)), "pointing at the sky takes nothing");

            // owner, headset 2026-11-22: "I couldn't tell when I was suddenly dragging the virtual furniture". A bare
            // hand's fist, the gaze on the piece, takes it only once held a moment; a flicker open doesn't drop it.
            var rig = XrRig.Instance != null ? XrRig.Instance.transform : null;
            var middle = kept.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; }).center;
            var look = Quaternion.LookRotation(middle - seatEye);
            var awayFromLeaf = new Vector3(2.5f, 0.3f, -2.5f);
            m_Pointers.Frames[1] = new PointerFrame
            {
                Tracked = true, Gaze = true, Grip = true,
                Position = rig != null ? rig.InverseTransformPoint(seatEye) : seatEye,
                Rotation = rig != null ? Quaternion.Inverse(rig.rotation) * look : look,
                GripPosition = rig != null ? rig.InverseTransformPoint(awayFromLeaf) : awayFromLeaf,
            };
            m_Pointers.Frames[1].HandPosition = m_Pointers.Frames[1].GripPosition;
            yield return null;
            yield return null;
            Assert.IsFalse(placer.Moving, "a passing fist takes nothing");
            yield return new WaitForSecondsRealtime(LeafGrab.HandHoldSeconds + 0.15f);
            Assert.IsTrue(placer.Moving, "a fist held a moment takes it");
            Assert.AreEqual(RealFurniture.HoldLift, placer.Lifted, 1e-5f, "and it lifts");
            m_Pointers.Frames[1].Grip = false;
            yield return null;
            Assert.IsTrue(placer.Moving, "a flicker open doesn't drop it");
            yield return new WaitForSecondsRealtime(LeafGrab.HandLetGoSeconds + 0.15f);
            Assert.IsFalse(placer.Moving, "opened a moment: put down");
            Assert.AreEqual(0f, placer.Lifted);
            m_Pointers.Frames[1] = default;
            // and Flutter says how, on a line of its own, once the furniture is chosen with a virtual piece kept
            float waited = 0f;
            while (!placer.MoveHintSaid && waited < 8f) { waited += Time.deltaTime; yield return null; }
            Assert.IsTrue(placer.MoveHintSaid, "the move gesture is explained");
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
            Assert.IsTrue(home.Icon.gameObject.activeInHierarchy, "and the instrument's picture over the object");
            yield return null;
            Assert.AreEqual(home.Target.Instrument, home.Icon.Showing);
            // owner, headset 2026-11-12: "where can I switch out an instrument?" a tap on the picture changes it
            var was = home.Target.Instrument;
            Assert.IsTrue(InstrumentInputRouter.Activate(home.Icon.gameObject), "the picture takes a tap");
            yield return null;
            Assert.AreEqual(InstrumentPacks.Next(was), home.Target.Instrument, "and the furniture plays the next instrument");
            yield return null;
            Assert.AreEqual(home.Target.Instrument, home.Icon.Showing, "the picture follows");
            InstrumentPacks.Instance.Place(home.Target, was); // back, for the rest of the test
            yield return new WaitForSeconds(0.3f);
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
            var backing = home.LabelOf(rightBand).transform.Find("NoteLabelBacking");
            Assert.IsNotNull(backing, "on a dark backing that reads against the room");
            // owner, headset 2026-11-08: the names didn't show; the backing was drawn over the letters
            var letters = home.LabelOf(rightBand).GetComponent<MeshRenderer>();
            Assert.Less(backing.GetComponent<MeshRenderer>().sharedMaterial.renderQueue, letters.sharedMaterial.renderQueue, "the backing is drawn before the letters");
            Assert.Greater(letters.sortingOrder, backing.GetComponent<MeshRenderer>().sortingOrder);
            Assert.AreEqual(RealHome.SelectedLabel, home.LabelOf(rightBand).color, "the pointed note's name is yellow");
            Assert.AreEqual(RealHome.OtherLabel, home.LabelOf(leftBand).color, "the others white");

            // and they really draw: a camera at the player's eye sees the yellow name (it was hidden on the headset)
            {
                var label = home.LabelOf(rightBand).transform;
                var eyeGo = new GameObject("LabelCheckEye");
                var eye = eyeGo.AddComponent<Camera>();
                eye.transform.position = new Vector3(0f, 1.2f, 0f);
                eye.transform.LookAt(label.position);
                eye.fieldOfView = 20f;
                eye.clearFlags = CameraClearFlags.SolidColor;
                eye.backgroundColor = Color.black;
                var rt = new RenderTexture(256, 256, 24);
                eye.targetTexture = rt;
                eye.Render();
                RenderTexture.active = rt;
                var shot = new Texture2D(256, 256, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                shot.Apply();
                RenderTexture.active = null;
                int yellow = 0;
                foreach (var px in shot.GetPixels()) if (px.r > 0.7f && px.g > 0.55f && px.b < 0.45f) yellow++;
                var dump = System.Environment.GetEnvironmentVariable("LE_LABEL_SHOT");
                if (!string.IsNullOrEmpty(dump)) System.IO.File.WriteAllBytes(dump, shot.EncodeToPNG());
                eye.targetTexture = null;
                Object.Destroy(rt);
                Object.Destroy(eyeGo);
                Assert.Greater(yellow, 20, "the pointed note's yellow name is drawn, not hidden behind its backing");
            }

            // a note played on it (a tap or a ball) shows its name, in yellow over its band, for a moment
            Point(hand, hand + Vector3.up * 3f, false); // pointing away
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(home.BandsShown, "nothing shown once the pointer has gone");
            home.Target.PlayNote(1);
            yield return null;
            Assert.IsTrue(home.BandsShown, "playing a note shows the bands");
            Assert.AreEqual(1, home.Hover, "the band played");
            Assert.AreEqual(RealHome.SelectedLabel, home.LabelOf(1).color, "its name in yellow");
            Assert.AreEqual(UiText.Tile(new MusicalNote(home.Target.Instrument, 1), Localization.Current), home.LabelOf(1).text);
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
            // owner, headset 2026-11-08: "while selecting furniture, if the player hits the undo button, it should undo
            // the last furniture that was assigned"
            Assert.AreNotEqual(first, placer.ChoosingTarget, "the next instrument is asked for");
            Object.FindFirstObjectByType<UndoButton>().Press();
            yield return null;
            Assert.AreEqual(first, placer.ChoosingTarget, "Undo: the last one chosen is asked for again");
            Assert.IsNull(first.Home, "its real home is gone");
            Assert.IsTrue(Drawn(first), "its virtual furniture is back");
            Assert.AreEqual(0, placer.MatchedCount);
            yield return Press(new Vector3(0.3f, 1.2f, 0.2f), DeskTop);
            Assert.IsNotNull(first.Home, "chosen again");

            Object.FindFirstObjectByType<PlacesButton>().Press();
            yield return null;
            Assert.IsTrue(placer.Choosing, "choosing again");
            Assert.AreEqual(first, placer.ChoosingTarget, "from the first instrument");
            Assert.IsTrue(Drawn(first), "its virtual furniture is back meanwhile");
            Assert.IsNull(first.Home);
        }
    }
}
