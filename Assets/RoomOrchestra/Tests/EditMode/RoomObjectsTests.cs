using NUnit.Framework;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// Owner, headset 2026-11-08: the chair the mbira went on "was extra large because of its proximity to me", and the
    /// yellow focus disc floated over the ball on the cloud. A chosen object's box never reaches into the player's space.
    /// </summary>
    public class RoomObjectsTests
    {
        [Test]
        public void ABoxThatReachesThePlayerIsCutBackOnTheirSide()
        {
            var head = new Vector3(0f, 1.2f, 0f);
            // the owner's headset log: a chair box 1.37 x 1.19 m around (0.26, -0.30), pointed at its far side
            var box = new Bounds(new Vector3(0.26f, 0.44f, -0.30f), new Vector3(1.37f, 0.89f, 1.19f));
            var point = new Vector3(0.8f, 0.88f, -0.7f);
            var cut = RoomObjects.ClearOfPlayer(box, point, head);
            Assert.IsTrue(cut.Contains(new Vector3(point.x, cut.center.y, point.z)), "still holds the point chosen");
            var flat = new Vector3(head.x, cut.center.y, head.z);
            Assert.Greater(Mathf.Sqrt(cut.SqrDistance(flat)), RoomObjects.PlayerClear - 0.01f, "clear of the player");
            Assert.AreEqual(box.size.y, cut.size.y, 1e-4f, "as tall as before");
        }

        [Test]
        public void ABoxAwayFromThePlayerIsUnchanged()
        {
            var box = new Bounds(new Vector3(1.5f, 0.4f, 1.2f), new Vector3(0.6f, 0.8f, 0.5f));
            Assert.AreEqual(box, RoomObjects.ClearOfPlayer(box, box.center, new Vector3(0f, 1.2f, 0f)));
        }
    }
}
