using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>The meadow follows the headset's clock: golden hour by day, dusk at night (same shapes, other colours).</summary>
    public class MeadowTimeOfDayTests
    {
        [TestCase(0, true)]
        [TestCase(5, true)]
        [TestCase(6, false)]
        [TestCase(12, false)]
        [TestCase(17, false)]
        [TestCase(18, true)]
        [TestCase(23, true)]
        public void DuskFromSixInTheEveningUntilSixInTheMorning(int hour, bool dusk) =>
            Assert.AreEqual(dusk, MeadowTimeOfDay.IsDuskAt(hour));

        [Test]
        public void EveryPieceHasADuskLookOfTheSameShape()
        {
            EditorSceneManager.OpenScene("Assets/RoomOrchestra/Scenes/RoomOrchestra.unity", OpenSceneMode.Single);
            var clock = Object.FindFirstObjectByType<MeadowTimeOfDay>();
            Assert.IsNotNull(clock, "the meadow has a time of day");
            var filters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(f => f.sharedMesh != null && f.sharedMesh.name.StartsWith("Meadow_") && f.name != "TitleSign").ToList();
            Assert.Greater(filters.Count, 50, "meadow pieces found");
            var day = filters.ToDictionary(f => f, f => f.sharedMesh);
            try
            {
                clock.Apply(true);
                Assert.IsTrue(clock.IsDusk);
                foreach (var f in filters)
                {
                    Assert.AreNotSame(day[f], f.sharedMesh, $"{f.name} changes at dusk");
                    if (f.name == "Sun" || f.name == "Stars") continue; // the sun becomes the moon, and the stars come out: other shapes
                    // the same shape (the vertex count can differ: Unity splits vertices where face colours differ)
                    Assert.Less(Vector3.Distance(day[f].bounds.size, f.sharedMesh.bounds.size), 0.01f, $"{f.name} keeps its shape");
                    Assert.AreEqual(day[f].triangles.Length, f.sharedMesh.triangles.Length, $"{f.name} keeps its faces");
                    Assert.Less(Vector3.Distance(day[f].bounds.center, f.sharedMesh.bounds.center), 0.01f, $"{f.name} stays in place");
                }
                var cloud = filters.First(f => f.name.StartsWith("Cloud")).GetComponent<Renderer>();
                Assert.AreEqual("MeadowCloudDusk", cloud.sharedMaterial.name);
            }
            finally
            {
                clock.Apply(false);
            }
            foreach (var f in filters) Assert.AreSame(day[f], f.sharedMesh, $"{f.name} back to golden hour");
        }
    }
}
