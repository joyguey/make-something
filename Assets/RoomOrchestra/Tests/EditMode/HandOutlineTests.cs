using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>The player's hands are drawn while the meadow hides the real ones.</summary>
    public class HandOutlineTests
    {
        [Test]
        public void AHandSeenThisFrameIsDrawnAndAnUnseenOneIsNot()
        {
            EditorSceneManager.OpenScene("Assets/RoomOrchestra/Scenes/RoomOrchestra.unity", OpenSceneMode.Single);
            var outline = Object.FindFirstObjectByType<HandOutline>();
            Assert.IsNotNull(outline, "the scene draws the player's hands");
            var lines = outline.GetComponentsInChildren<LineRenderer>(true);
            Assert.AreEqual(2 * HandOutline.LinesPerHand, lines.Length);
            foreach (var l in lines) Assert.IsFalse(l.enabled, "nothing drawn until a hand is seen");
            Assert.IsTrue(HandOutline.Wanted, "the meadow stands at launch");

            // a right hand 40 cm in front of the eyes: joint i at 1 cm steps upward
            HandPose.Report(1, i => new Vector3(0.1f, 1.1f + 0.01f * i, 0.4f));
            outline.Refresh();
            for (int k = 0; k < HandOutline.LinesPerHand; k++)
            {
                Assert.IsFalse(lines[k].enabled, "the left hand was not seen");
                var right = lines[HandOutline.LinesPerHand + k];
                Assert.IsTrue(right.enabled, "the right hand is drawn");
                Assert.AreEqual(HandOutline.JointsIn(k), right.positionCount);
            }
            // the index finger's line ends at its tip (joint 10)
            var index = lines[HandOutline.LinesPerHand + 1];
            Assert.Less(Vector3.Distance(index.GetPosition(index.positionCount - 1), new Vector3(0.1f, 1.2f, 0.4f)), 1e-4f);
            Assert.Greater(HandOutline.WidthOf(5), HandOutline.WidthOf(1) * 3f, "the palm is drawn wide, the fingers thin");
            foreach (var l in lines) l.enabled = false;
        }
    }
}
