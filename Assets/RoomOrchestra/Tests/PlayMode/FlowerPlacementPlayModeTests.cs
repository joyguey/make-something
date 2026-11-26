using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// On the real scene with a pretend room scan (a single table top): flowers that sprout grow on it, keep apart,
    /// and keep their own places once it is full (George, 2026-11-03: flat, unobstructed surfaces from the room mesh).
    /// </summary>
    public class FlowerPlacementPlayModeTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";
        static readonly Vector3 TableCentre = new Vector3(1.6f, 0.72f, 1.8f);

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
            var empty = SceneManager.CreateScene($"FlowerEmpty{Time.frameCount}");
            SceneManager.SetActiveScene(empty);
            var real = SceneManager.GetSceneByPath(ScenePath);
            if (real.isLoaded) yield return SceneManager.UnloadSceneAsync(real);
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
        }

        // A 1 m x 80 cm table top as a scan would give it: 10 cm quads facing up, in scene space.
        static Vector3[] TableTop()
        {
            var tris = new System.Collections.Generic.List<Vector3>();
            for (float x = -0.5f; x < 0.5f - 1e-4f; x += 0.1f)
                for (float z = -0.4f; z < 0.4f - 1e-4f; z += 0.1f)
                {
                    var a = TableCentre + new Vector3(x, 0f, z);
                    var b = a + new Vector3(0f, 0f, 0.1f);
                    var c = a + new Vector3(0.1f, 0f, 0.1f);
                    var d = a + new Vector3(0.1f, 0f, 0f);
                    tris.AddRange(new[] { a, b, c, a, c, d });
                }
            return tris.ToArray();
        }

        [UnityTest]
        public IEnumerator SproutingFlowersGrowOnTheScannedTableTop()
        {
            var walls = RealWalls.Instance;
            Assert.IsNotNull(walls, "the scene listens to the room scan");
            walls.SetPiece(default(MeshId), TableTop());
            var jungle = Object.FindFirstObjectByType<JungleDirector>();
            var flowers = Object.FindObjectsByType<FloorFlower>(FindObjectsSortMode.None);
            var before = flowers.ToDictionary(f => f, f => f.transform.position);

            for (int i = 0; i < 12; i++) jungle.NoteAdded(Instruments.Chair);
            yield return null;
            var sprouted = flowers.Where(f => f.Sprouted).ToArray();
            Assert.Greater(sprouted.Length, 3, "the song grew some flowers");
            var onTable = sprouted.Where(f => Mathf.Abs(f.transform.position.y - TableCentre.y) < 0.01f).ToArray();
            Assert.GreaterOrEqual(onTable.Length, 2, "flowers grow on the scanned table top");
            foreach (var f in onTable)
                Assert.Less(Vector2.Distance(new Vector2(f.transform.position.x, f.transform.position.z), new Vector2(TableCentre.x, TableCentre.z)), 0.65f, $"{f.name} is on the table");
            for (int i = 0; i < onTable.Length; i++)
                for (int j = i + 1; j < onTable.Length; j++)
                    Assert.GreaterOrEqual(Vector3.Distance(onTable[i].transform.position, onTable[j].transform.position), FlatSpots.MinSpacing - 1e-3f, "and keep apart");
            foreach (var f in sprouted.Except(onTable))
                Assert.AreEqual(before[f], f.transform.position, $"{f.name}: the table is full, so it keeps its own place");
            foreach (var f in flowers.Where(f => !f.Sprouted))
                Assert.AreEqual(before[f], f.transform.position, $"{f.name}: not sprouted, not moved");
        }
    }
}
