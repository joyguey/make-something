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
        public void OpenScene() => EditorSceneManager.OpenScene("Assets/RoomOrchestra/Scenes/RoomOrchestra.unity", OpenSceneMode.Single);

        [Test]
        public void ControlsAndFurnitureHaveRoundedEdgesAtTheirOwnSize()
        {
            var slabs = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(f => f.name.EndsWith("Slab") || f.name.StartsWith("PitchTile") || f.name == "MixTile" || f.name == "Top" || f.name == "Seat").ToArray();
            Assert.Greater(slabs.Length, 20);
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
    }
}
