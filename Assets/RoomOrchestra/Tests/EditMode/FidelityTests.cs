using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>Version 1 feedback "higher fidelity": rounded blocks of unchanged size, and a real lampshade.</summary>
    public class FidelityTests
    {
        [OneTimeSetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene("Assets/RoomOrchestra/Scenes/RoomOrchestra.unity", OpenSceneMode.Single);
            Object.FindFirstObjectByType<IntroSequence>().Finish(); // the table and lamp home (saved in the cloud for the lesson)
        }

        [Test]
        public void ControlsAndFurnitureHaveRoundedEdgesAtTheirOwnSize()
        {
            var slabs = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(f => f.name.EndsWith("Slab") || f.name.StartsWith("PitchTile") || f.name == "MixTile").ToArray();
            Assert.Greater(slabs.Length, 20);
            Assert.IsTrue(slabs.Count(f => f.name.StartsWith("PitchTile")) >= 15, "every note tile");
            foreach (var f in slabs)
            {
                StringAssert.StartsWith("RoundedBox_", f.sharedMesh.name, $"{f.name} has rounded edges");
                var b = f.sharedMesh.bounds;
                Assert.Less((b.size - Vector3.one).magnitude, 1e-3f, $"{f.name}'s mesh fills a unit box: its size is unchanged");
                Assert.Less(b.center.magnitude, 1e-3f);
            }
        }

        [Test]
        public void TheLampHasAShadeWithAClosedDiffuser()
        {
            var shade = GameObject.Find("RoomOrchestra").transform.Find("Furniture/Lamp/Shade").GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual("LampShade", shade.name);
            Assert.Less(shade.bounds.max.x, 0.51f, "within the old shade's size");
            Assert.IsTrue(shade.vertices.Any(v => v.sqrMagnitude < 1e-6f + 0.34f * 0.34f && Mathf.Abs(v.x) < 1e-4f && Mathf.Abs(v.z) < 1e-4f), "a closed bottom (its centre point)");
        }

        [Test]
        public void TheFurnitureUsesTheBlenderModelsAtTheSameSize()
        {
            var furniture = GameObject.Find("RoomOrchestra").transform.Find("Furniture");
            var expected = new (string furniture, string part, string mesh)[]
            {
                ("Table", "Top", "Furniture_TableTop"), ("Table", "Leg", "Furniture_TableLeg"), ("Chair", "Seat", "Furniture_ChairSeat"),
                ("Chair", "Back", "Furniture_ChairBack"), ("Chair", "Leg", "Furniture_ChairLeg"), ("Lamp", "Base", "Furniture_LampBase"), ("Lamp", "Pole", "Furniture_LampPole"),
            };
            foreach (var (f, p, mesh) in expected)
                foreach (Transform part in furniture.Find(f))
                    if (part.name == p)
                    {
                        Assert.AreEqual(mesh, part.GetComponent<MeshFilter>().sharedMesh.name, $"{f}/{p}");
                        Assert.IsNotNull(part.GetComponent<Collider>(), $"{f}/{p} still has its collider");
                    }
            // The legs reach the floor and the top sits at the table's height (the models keep each part's size).
            var top = furniture.Find("Table/Top").GetComponent<Renderer>().bounds;
            Assert.AreEqual(0.75f, top.max.y, 0.01f, "the table top at 75 cm");
            foreach (Transform leg in furniture.Find("Table"))
                if (leg.name == "Leg") Assert.Less(leg.GetComponent<Renderer>().bounds.min.y, 0.01f, "a leg stands on the floor");
        }
    }
}
