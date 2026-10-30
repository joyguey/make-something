using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>The real room as bounce walls: only steep surfaces are kept; the scene has the scanner in world space.</summary>
    public class RealWallsTests
    {
        [Test]
        public void OnlySteepSurfacesBecomeWalls()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var mesh = cube.GetComponent<MeshFilter>().sharedMesh;
                var walls = RealWalls.SteepOnly(mesh, Quaternion.identity);
                Assert.AreEqual(8, walls.triangles.Length / 3, "a box keeps its four sides (two triangles each); top and bottom go");
                var tipped = RealWalls.SteepOnly(mesh, Quaternion.Euler(90f, 0f, 0f));
                Assert.AreEqual(8, tipped.triangles.Length / 3, "judged in the room's frame: the scan's own rotation counts");
                var floor = new Mesh { vertices = new[] { Vector3.zero, Vector3.forward, Vector3.right }, triangles = new[] { 0, 1, 2 } };
                Assert.AreEqual(0, RealWalls.SteepOnly(floor, Quaternion.identity).triangles.Length, "a floor is not a wall");
                var wall = new Mesh { vertices = new[] { Vector3.zero, Vector3.up, Vector3.right }, triangles = new[] { 0, 1, 2 } };
                Assert.AreEqual(3, RealWalls.SteepOnly(wall, Quaternion.identity).triangles.Length);
            }
            finally
            {
                Object.DestroyImmediate(cube);
            }
        }

        [Test]
        public void TheSceneScansTheRoomInTheHeadsetsOwnSpace()
        {
            EditorSceneManager.OpenScene("Assets/RoomOrchestra/Scenes/RoomOrchestra.unity", OpenSceneMode.Single);
            var walls = Object.FindFirstObjectByType<RealWalls>();
            Assert.IsNotNull(walls);
            Assert.IsNull(walls.transform.parent, "top level: recentring the stage never moves the real walls");
            Assert.AreEqual(Vector3.zero, walls.transform.position);
            Assert.AreEqual(Quaternion.identity, walls.transform.rotation);
            Assert.IsNull(walls.GetComponent<Renderer>(), "nothing of the scan is drawn");
        }
    }
}
