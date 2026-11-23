using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>The meadow as laid out: nothing grows into anything else (owner, 2026-11-19: "make sure nothing is
    /// overlapping that wouldn't be found in nature"), and only a few outcrops stand on the horizon.</summary>
    public class MeadowLayoutTests
    {
        static Transform[] Pieces(string prefix) =>
            Object.FindFirstObjectByType<VirtualRoom>().Panels.Where(p => p.name.StartsWith(prefix)).ToArray();

        static float Flat(Transform a, Transform b)
        {
            var d = a.position - b.position;
            d.y = 0f;
            return d.magnitude;
        }

        [SetUp]
        public void OpenScene() => EditorSceneManager.OpenScene("Assets/RoomOrchestra/Scenes/RoomOrchestra.unity", OpenSceneMode.Single);

        [Test]
        public void TreesStandApartAndNothingGrowsIntoATree()
        {
            var trees = Pieces("Tree");
            Assert.Greater(trees.Length, 8, "the meadow has its trees");
            for (int i = 0; i < trees.Length; i++)
                for (int j = i + 1; j < trees.Length; j++)
                    Assert.GreaterOrEqual(Flat(trees[i], trees[j]), 3.4f - 1e-3f, $"{trees[i].name} and {trees[j].name}: their canopies meet");
            foreach (var kind in new[] { "Flowers", "Bush", "Rock" })
                foreach (var piece in Pieces(kind))
                    foreach (var tree in trees)
                        Assert.Greater(Flat(piece, tree), 1.3f, $"{piece.name} grows into {tree.name}");
        }

        [Test]
        public void FlowersBushesAndRocksKeepTheirOwnGround()
        {
            var things = Pieces("Flowers").Concat(Pieces("Bush")).Concat(Pieces("Rock")).ToArray();
            for (int i = 0; i < things.Length; i++)
                for (int j = i + 1; j < things.Length; j++)
                    Assert.Greater(Flat(things[i], things[j]), 0.7f, $"{things[i].name} and {things[j].name} overlap");
        }

        [Test]
        public void ThreeOutcropsAndNothingInsideThem()
        {
            var hills = Pieces("Hill");
            Assert.AreEqual(3, hills.Length, "about three when looking around (owner)");
            foreach (var hill in hills)
            {
                var size = hill.GetComponent<MeshFilter>().sharedMesh.bounds.extents;
                float reach = Mathf.Max(size.x, size.z) * hill.lossyScale.x;
                foreach (var kind in new[] { "Tree", "Flowers", "Bush", "Rock" })
                    foreach (var piece in Pieces(kind))
                        Assert.Greater(Flat(piece, hill), reach, $"{piece.name} stands inside {hill.name}");
            }
        }
    }
}
