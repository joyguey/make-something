using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>The player's hands (PICO's hand models) are shown while the meadow hides the real ones.</summary>
    public class HandOutlineTests
    {
        [Test]
        public void AHandTrackedThisFrameIsShownAndAnUntrackedOneIsNot()
        {
            EditorSceneManager.OpenScene("Assets/RoomOrchestra/Scenes/RoomOrchestra.unity", OpenSceneMode.Single);
            var outline = Object.FindFirstObjectByType<HandOutline>();
            Assert.IsNotNull(outline, "the scene has the player's hands");
            for (int hand = 0; hand < 2; hand++)
            {
                var model = outline.Model(hand);
                Assert.IsNotNull(model, "a model for each hand");
                Assert.IsFalse(model.activeSelf, "hidden until its hand is tracked");
                Assert.IsNotNull(model.GetComponent("PXR_Hand"), "PICO's hand follows the joints");
                var skin = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert.IsNotNull(skin, "a hand mesh");
                Assert.AreEqual("PlayerHand", skin.sharedMaterial.name, "in the outline look");
                Assert.IsNull(model.transform.Find("RayPose"), "no pinch ray: the game points with the eyes");
                Assert.IsNull(model.transform.Find("DefaultRay"));
            }
            Assert.IsTrue(HandOutline.Wanted, "the meadow stands at launch");
            HandPose.Report(1);
            outline.Refresh();
            Assert.IsFalse(outline.Model(0).activeSelf, "the left hand is not tracked");
            Assert.IsTrue(outline.Model(1).activeSelf, "the right hand is shown");
            outline.Model(1).SetActive(false);
        }
    }
}
