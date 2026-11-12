using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>The painted look: rim light on characters and furniture only, wind sway only on plants, haze far away.</summary>
    public class LookTests
    {
        [Test]
        public void RimOnCharactersAndFurnitureSwayOnPlantsHazeFarAway()
        {
            EditorSceneManager.OpenScene("Assets/RoomOrchestra/Scenes/RoomOrchestra.unity", OpenSceneMode.Single);
            Assert.IsNotNull(Object.FindFirstObjectByType<LookSettings>(), "the look is set as the scene loads");
            Assert.IsTrue(RenderSettings.fog);
            Assert.GreaterOrEqual(RenderSettings.fogStartDistance, 8f, "no haze on anything within reach");

            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            Material On(string name) => renderers.First(r => r.name.StartsWith(name)).sharedMaterial;
            float Rim(Material m) => m.HasProperty("_RimAmount") ? m.GetFloat("_RimAmount") : 0f;
            float Sway(Material m) => m.HasProperty("_SwayAmount") ? m.GetFloat("_SwayAmount") : 0f;

            Assert.AreEqual(0f, Rim(On("GroundS")), "no rim on the ground");
            Assert.AreEqual(0f, Sway(On("GroundS")), "the ground stays still");
            Assert.AreEqual(0f, Sway(On("Rock")), "rocks stay still");
            Assert.Greater(Sway(On("Grass")), 0f, "grass sways");
            Assert.Greater(Sway(On("Flowers")), 0f, "flowers sway");
            Assert.Greater(Sway(On("Tree")), 0f, "trees sway");
            Assert.Less(Sway(On("Tree")), 0.1f, "gently");

            var furniture = GameObject.Find("RoomOrchestra").transform.Find("Furniture").GetComponentsInChildren<MeshRenderer>()
                .Where(r => r.sharedMaterial != null && r.sharedMaterial.shader.name == BakedAOShader).ToList();
            Assert.IsNotEmpty(furniture);
            foreach (var r in furniture) Assert.AreEqual(1f, Rim(r.sharedMaterial), $"{r.name} catches the rim light");
        }

        const string BakedAOShader = "RoomOrchestra/LitBakedAO";
    }
}
