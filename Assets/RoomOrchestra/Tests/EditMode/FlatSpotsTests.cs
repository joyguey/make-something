using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// Flowers grow only on flat, unobstructed real surfaces (George, 2026-11-03), found in a pretend room scan:
    /// a 3 x 3 m floor, a table, a low shelf over part of the floor, a wall, and the player's seat.
    /// </summary>
    public class FlatSpotsTests
    {
        // Level surface from (x0, z0) to (x1, z1) at height y, in 10 cm quads facing up, like a scan.
        static void Level(FlatSpots spots, float x0, float z0, float x1, float z1, float y)
        {
            const float step = 0.1f;
            for (float x = x0; x < x1 - 1e-4f; x += step)
                for (float z = z0; z < z1 - 1e-4f; z += step)
                {
                    var a = new Vector3(x, y, z);
                    var b = new Vector3(x, y, z + step);
                    var c = new Vector3(x + step, y, z + step);
                    var d = new Vector3(x + step, y, z);
                    spots.Add(a, b, c);
                    spots.Add(a, c, d);
                }
        }

        // An upright surface along x at depth z, from height 0 to h.
        static void Wall(FlatSpots spots, float x0, float x1, float z, float h)
        {
            for (float x = x0; x < x1 - 1e-4f; x += 0.1f)
                for (float y = 0f; y < h - 1e-4f; y += 0.1f)
                {
                    spots.Add(new Vector3(x, y, z), new Vector3(x + 0.1f, y, z), new Vector3(x + 0.1f, y + 0.1f, z));
                    spots.Add(new Vector3(x, y, z), new Vector3(x + 0.1f, y + 0.1f, z), new Vector3(x, y + 0.1f, z));
                }
        }

        static FlatSpots Room()
        {
            var spots = new FlatSpots();
            Level(spots, -1.5f, -0.5f, 1.5f, 2.5f, 0f);       // the floor
            Level(spots, 0.6f, 1.2f, 1.2f, 1.8f, 0.72f);      // a table top
            Level(spots, -1.2f, 1.2f, -0.6f, 1.6f, 0.2f);     // a low shelf: no headroom on the floor under it
            Wall(spots, -1.5f, 1.5f, 2.5f, 1.2f);             // the far wall
            spots.KeepClearOf(Vector3.zero, 0.7f);            // the seat
            return spots;
        }

        [Test]
        public void SpotsAreOnFlatClearSurfacesAwayFromTheSeat()
        {
            var spots = Room().Spots(Vector3.zero);
            Assert.Greater(spots.Count, 20, "plenty of open floor");
            foreach (var s in spots)
            {
                bool onFloor = Mathf.Abs(s.y) < 0.01f, onTable = Mathf.Abs(s.y - 0.72f) < 0.01f, onShelf = Mathf.Abs(s.y - 0.2f) < 0.01f;
                Assert.IsTrue(onFloor || onTable || onShelf, $"{s} sits on a real surface");
                Assert.GreaterOrEqual(new Vector2(s.x, s.z).magnitude, 0.7f, $"{s} keeps clear of the seat");
                bool underShelf = s.x > -1.2f && s.x < -0.6f && s.z > 1.2f && s.z < 1.6f;
                Assert.IsFalse(onFloor && underShelf, $"{s}: no flower in the floor space under the shelf");
                bool underTable = s.x > 0.6f && s.x < 1.2f && s.z > 1.2f && s.z < 1.8f;
                Assert.IsFalse(onFloor && underTable, $"{s}: the table top, not the floor under it");
                Assert.Less(s.z, 2.4f, $"{s}: not in the square at the wall's foot");
            }
            Assert.IsTrue(spots.Any(s => Mathf.Abs(s.y - 0.72f) < 0.01f), "flowers can grow on the table top");
        }

        [Test]
        public void TakenSpotsKeepTheirDistanceAndCanBeFreed()
        {
            var room = Room();
            var taken = new System.Collections.Generic.List<Vector3>();
            while (taken.Count < 12 && room.TryTake(new Vector3(0f, 0f, 1.2f), out var spot)) taken.Add(spot);
            Assert.AreEqual(12, taken.Count);
            for (int i = 0; i < taken.Count; i++)
                for (int j = i + 1; j < taken.Count; j++)
                    Assert.GreaterOrEqual(Vector2.Distance(new Vector2(taken[i].x, taken[i].z), new Vector2(taken[j].x, taken[j].z)), FlatSpots.MinSpacing - 1e-4f);
            room.ReleaseAll();
            Assert.IsTrue(room.TryTake(new Vector3(0f, 0f, 1.2f), out var again));
            Assert.AreEqual(taken[0], again, "after Clear the first place is free again");
        }

        [Test]
        public void NoScanNoSpots()
        {
            var empty = new FlatSpots();
            Assert.IsFalse(empty.TryTake(Vector3.zero, out _), "without a room scan flowers keep their own places");
            var steepOnly = new FlatSpots();
            Wall(steepOnly, 0f, 1f, 1f, 1f);
            Assert.IsEmpty(steepOnly.Spots(Vector3.zero), "a wall is no place for a flower");
        }
    }
}
