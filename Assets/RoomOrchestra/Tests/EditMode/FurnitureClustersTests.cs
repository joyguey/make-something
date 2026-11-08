using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>The labelled room scan becomes separate real pieces of furniture with their tops (pretend room).</summary>
    public class FurnitureClustersTests
    {
        // A box's surface points, 5 cm apart, like a scan of a piece of furniture.
        static void Box(FurnitureClusters c, FurnitureKind kind, Vector3 min, Vector3 max)
        {
            const float step = 0.05f;
            for (float x = min.x; x <= max.x + 1e-4f; x += step)
            for (float z = min.z; z <= max.z + 1e-4f; z += step)
            {
                c.Add(kind, new Vector3(x, max.y, z)); // top
                for (float y = min.y; y < max.y; y += step)
                    if (x <= min.x + 1e-4f || x >= max.x - 1e-4f || z <= min.z + 1e-4f || z >= max.z - 1e-4f)
                        c.Add(kind, new Vector3(x, y, z)); // sides
            }
        }

        [Test]
        public void TwoTablesAreTwoPiecesWithTheirTops()
        {
            var c = new FurnitureClusters();
            Box(c, FurnitureKind.Table, new Vector3(0f, 0f, 1f), new Vector3(1f, 0.75f, 1.6f));
            Box(c, FurnitureKind.Table, new Vector3(-2.5f, 0f, 1f), new Vector3(-1.9f, 0.45f, 1.4f));
            var tables = c.Pieces().Where(p => p.Kind == FurnitureKind.Table).ToList();
            Assert.AreEqual(2, tables.Count);
            var big = tables[0];
            Assert.AreEqual(0.75f, big.TopY, 0.06f);
            Assert.AreEqual(0.5f, big.Top.x, 0.1f);
            Assert.AreEqual(1.3f, big.Top.z, 0.1f);
            Assert.AreEqual(0.45f, tables[1].TopY, 0.06f);
        }

        [Test]
        public void KindsStaySeparateAndSpecksAreIgnored()
        {
            var c = new FurnitureClusters();
            Box(c, FurnitureKind.Chair, new Vector3(1.5f, 0f, 0f), new Vector3(1.95f, 0.9f, 0.45f));
            Box(c, FurnitureKind.Sofa, new Vector3(2f, 0f, 0f), new Vector3(3.8f, 0.8f, 0.9f)); // right next to the chair
            c.Add(FurnitureKind.Lamp, new Vector3(-3f, 1.5f, -3f)); // one stray labelled point
            c.Add(FurnitureKind.None, new Vector3(0f, 0f, 0f));
            var pieces = c.Pieces();
            Assert.AreEqual(1, pieces.Count(p => p.Kind == FurnitureKind.Chair));
            Assert.AreEqual(1, pieces.Count(p => p.Kind == FurnitureKind.Sofa));
            Assert.AreEqual(0, pieces.Count(p => p.Kind == FurnitureKind.Lamp));
            Assert.AreEqual(FurnitureKind.Sofa, pieces[0].Kind, "biggest first");
        }

        [Test]
        public void ScanLabelsMapToFurniture()
        {
            Assert.AreEqual(FurnitureKind.Table, RoomScan.KindOf(7));
            Assert.AreEqual(FurnitureKind.Sofa, RoomScan.KindOf(8));
            Assert.AreEqual(FurnitureKind.Chair, RoomScan.KindOf(9));
            Assert.AreEqual(FurnitureKind.Lamp, RoomScan.KindOf(22));
            Assert.AreEqual(FurnitureKind.None, RoomScan.KindOf(3), "walls aren't furniture");
            Assert.AreEqual(FurnitureKind.None, RoomScan.KindOf(1), "nor is the floor");
        }
    }
}
