using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// The original vision on the real scene (owner, 2026-11-07): after the lesson the instruments move onto the real
    /// furniture the room scan names (a pretend scan here), a ball that hits the real piece plays it, and Lesson puts
    /// them back. And a tapped note flies there as a ball instead of skipping the throw.
    /// </summary>
    public class RealFurniturePlayModeTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";
        static readonly Vector3 RealTableMin = new(-1.6f, 0f, 1.6f), RealTableMax = new(-0.6f, 0.72f, 2.2f);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            Object.FindFirstObjectByType<IntroSequence>().Finish(); // free play
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            OrbSelector.ThrowOnTap = false;
            var empty = SceneManager.CreateScene($"RealFurnitureEmpty{Time.frameCount}");
            SceneManager.SetActiveScene(empty);
            var real = SceneManager.GetSceneByPath(ScenePath);
            if (real.isLoaded) yield return SceneManager.UnloadSceneAsync(real);
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
        }

        // A box's surface as a scan gives it: points 5 cm apart, all with one label.
        static (Vector3[] points, FurnitureKind[] kinds) Box(FurnitureKind kind, Vector3 min, Vector3 max)
        {
            var pts = new List<Vector3>();
            const float step = 0.05f;
            for (float x = min.x; x <= max.x + 1e-4f; x += step)
            for (float z = min.z; z <= max.z + 1e-4f; z += step)
            {
                pts.Add(new Vector3(x, max.y, z));
                for (float y = min.y; y < max.y; y += step)
                    if (x <= min.x + 1e-4f || x >= max.x - 1e-4f || z <= min.z + 1e-4f || z >= max.z - 1e-4f)
                        pts.Add(new Vector3(x, y, z));
            }
            return (pts.ToArray(), Enumerable.Repeat(kind, pts.Count).ToArray());
        }

        static void Scan(FurnitureKind kind, Vector3 min, Vector3 max)
        {
            var (p, k) = Box(kind, min, max);
            RoomScan.Instance.SetPiece(System.Guid.NewGuid(), p, k);
        }

        static InstrumentTarget TargetOn(string furniture) =>
            GameObject.Find("RoomOrchestra").transform.Find($"Furniture/{furniture}").GetComponentInChildren<InstrumentTarget>();

        [UnityTest]
        public IEnumerator AfterTheLessonInstrumentsMoveOntoRealFurnitureAndPlayThere()
        {
            Assert.IsNotNull(RoomScan.Instance, "the scene has the room scan");
            Assert.IsNotNull(RealFurniture.Instance, "and the furniture step");
            Scan(FurnitureKind.Table, RealTableMin, RealTableMax);
            Scan(FurnitureKind.Chair, new Vector3(0.3f, 0f, 2.6f), new Vector3(0.8f, 0.9f, 3.1f));
            Scan(FurnitureKind.Lamp, new Vector3(1.4f, 0f, 1.8f), new Vector3(1.7f, 1.5f, 2.1f));

            var room = VirtualRoom.Instance;
            room.TearDown();
            float until = Time.time + room.TearDownSeconds + 3f;
            while (!room.Gone && Time.time < until) yield return null;
            Assert.IsTrue(room.Gone);

            until = Time.time + RealFurniture.LookSeconds + 4f;
            while (RealFurniture.Instance.State != RealFurniture.Phase.Placed && Time.time < until) yield return null;
            Assert.AreEqual(RealFurniture.Phase.Placed, RealFurniture.Instance.State);
            Assert.AreEqual(3, RealFurniture.Instance.MatchedCount);
            yield return new WaitForSeconds(RealFurniture.MoveSeconds + 0.2f);

            var table = TargetOn("Table");
            var top = new Vector3((RealTableMin.x + RealTableMax.x) / 2f, RealTableMax.y, (RealTableMin.z + RealTableMax.z) / 2f);
            Assert.Less(Vector3.Distance(table.AimPosition, top + Vector3.up * 0.06f), 0.15f, "the udu aims at the real table top");
            var root = table.transform.root.Find("Furniture/Table");
            Assert.Less(root.localScale.x, 6f * 0.5f, "the virtual table is a small marker now");

            // a ball dropped onto the real table (not the marker) plays the udu
            MusicalHitEvent played = null;
            void OnHit(MusicalHitEvent hit) => played ??= hit;
            MusicalHitPipeline.Committed += OnHit;
            var launcher = Object.FindFirstObjectByType<BallLauncher>();
            launcher.ThrowFrom(new Vector3(-0.8f, 1.3f, 1.75f), Vector3.down * 3f);
            until = Time.time + 2f;
            while (played == null && Time.time < until) yield return null;
            MusicalHitPipeline.Committed -= OnHit;
            Assert.IsNotNull(played, "hitting the real table plays");
            Assert.AreEqual(table.Instrument, played.Target);

            // Lesson again: everything back in its virtual place
            TutorialDirector.Instance.Replay();
            yield return null;
            Assert.AreEqual(RealFurniture.Phase.Waiting, RealFurniture.Instance.State);
            Assert.AreEqual(0, RealFurniture.Instance.MatchedCount);
        }

        [UnityTest]
        public IEnumerator ATappedNoteFliesThereAsABallAndPlaysThatNote()
        {
            var selector = OrbSelector.Instance;
            var tile = Object.FindObjectsByType<PitchTile>(FindObjectsSortMode.None).First(t => t.Target != null && t.Petal == 4);
            MusicalHitEvent direct = null, thrown = null;

            OrbSelector.ThrowOnTap = false;
            direct = selector.PlaceOnTile(tile);
            Assert.IsNotNull(direct, "the direct path plays at once");
            yield return new WaitForSeconds(0.3f);

            OrbSelector.ThrowOnTap = true;
            void OnHit(MusicalHitEvent hit) => thrown ??= hit;
            MusicalHitPipeline.Committed += OnHit;
            var launcher = Object.FindFirstObjectByType<BallLauncher>();
            int throws = launcher.ThrowCount;
            double tapped = AudioSettings.dspTime;
            Assert.IsNull(selector.PlaceOnTile(tile), "the note waits for the ball");
            Assert.AreEqual(throws + 1, launcher.ThrowCount, "the tap threw the ball");
            float until = Time.time + 2f;
            while (thrown == null && Time.time < until) yield return null;
            MusicalHitPipeline.Committed -= OnHit;
            Assert.IsNotNull(thrown, "the carried note played");
            Assert.AreEqual(tile.Target.Instrument, thrown.Target);
            Assert.AreEqual(direct.Note.Index, thrown.Note.Index, "the tapped note, not one from where it hit");
            Assert.AreEqual(tapped, thrown.CreatedAtDspTime, 0.1, "it keeps its place in the bar from the tap");
        }
    }
}
