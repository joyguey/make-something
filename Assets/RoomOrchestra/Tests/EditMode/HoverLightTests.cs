using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>George: "Highlight buttons and other objects when I target them": the thing under the pointer is drawn lighter.</summary>
    public class HoverLightTests
    {
        [Test]
        public void TheButtonUnderThePointerIsDrawnLighterUntilThePointerLeaves()
        {
            EditorSceneManager.OpenScene("Assets/RoomOrchestra/Scenes/RoomOrchestra.unity", OpenSceneMode.Single);
            var start = Object.FindFirstObjectByType<TitleScreen>().Card.GetComponentInChildren<TitleStartButton>(true);
            var tapArea = start.transform.Find("ButtonLook").GetComponentInChildren<Collider>(true);
            var look = HoverLight.RendererOf(tapArea);
            Assert.IsNotNull(look, "the tap area has a visible part to light");
            Assert.IsNull(look.GetComponent<TMPro.TMP_Text>(), "never the label");
            var material = look.sharedMaterial;
            var own = material.GetColor("_BaseColor");
            try
            {
                HoverLight.Set(1, look);
                Assert.IsTrue(look.HasPropertyBlock(), "lit");
                var block = new MaterialPropertyBlock();
                look.GetPropertyBlock(block);
                var lit = block.GetColor("_BaseColor");
                Assert.Greater(lit.grayscale, own.grayscale + 0.05f, "clearly lighter");
                Assert.AreSame(material, look.sharedMaterial, "its material is not copied");
                Assert.AreEqual(own, material.GetColor("_BaseColor"), "nor changed");
                HoverLight.Set(0, look);       // both hands on it
                HoverLight.Set(1, null);       // one leaves
                Assert.IsTrue(look.HasPropertyBlock(), "still lit for the other hand");
            }
            finally
            {
                HoverLight.Set(0, null);
                HoverLight.Set(1, null);
            }
            Assert.IsFalse(look.HasPropertyBlock(), "back to its own look");
        }
    }
}
