using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// The gameplay restyle as built (owner, 2026-11-19, the Astro Bot direction, option A): toy buttons, a modelled leaf
    /// and stalk, note tiles tinted by instrument, the hit bloom's pool, and the chair's cushion. The characters are not
    /// part of it (owner: "keep those friends the same for now").
    /// </summary>
    public class GameplayLookTests
    {
        [SetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene("Assets/RoomOrchestra/Scenes/RoomOrchestra.unity", OpenSceneMode.Single);
            Object.FindFirstObjectByType<IntroSequence>().Finish();
        }

        static double Luminance(Color c)
        {
            double L(float v) => v <= 0.04045 ? v / 12.92 : System.Math.Pow((v + 0.055) / 1.055, 2.4);
            return 0.2126 * L(c.r) + 0.7152 * L(c.g) + 0.0722 * L(c.b);
        }

        [Test]
        public void EveryInstrumentsTileFaceKeepsWhiteLettersReadable()
        {
            foreach (InstrumentType type in System.Enum.GetValues(typeof(InstrumentType)))
            {
                var face = AssetDatabase.LoadAssetAtPath<Material>($"Assets/RoomOrchestra/Materials/TileFace_{type}.mat");
                Assert.IsNotNull(face, $"a face tint for {type}");
                double contrast = 1.05 / (Luminance(face.color) + 0.05);
                Assert.GreaterOrEqual(contrast, 4.5, $"white on {type}'s tiles: {contrast:F2}:1");
            }
        }

        [Test]
        public void ThePlumCapsKeepWhiteWordsAndGoldIconsReadable()
        {
            var cap = AssetDatabase.LoadAssetAtPath<Material>("Assets/RoomOrchestra/Materials/ToyCap.mat");
            var gold = AssetDatabase.LoadAssetAtPath<Material>("Assets/RoomOrchestra/Materials/IconGold.mat");
            double c = Luminance(cap.color);
            Assert.GreaterOrEqual(1.05 / (c + 0.05), 7.0, "white words on a cap");
            Assert.GreaterOrEqual((Luminance(gold.color) + 0.05) / (c + 0.05), 4.5, "gold icons on a cap");
        }

        [Test]
        public void TilesAreTintedKeycapsThatFollowTheirInstrument()
        {
            var tints = Object.FindObjectsByType<InstrumentTint>(FindObjectsSortMode.None);
            Assert.AreEqual(3, tints.Length, "one per pitch column");
            foreach (var tint in tints)
            {
                var type = tint.Target.Instrument;
                Assert.AreEqual(6, tint.Bodies.Length, "five note tiles and the name tile");
                foreach (var body in tint.Bodies)
                {
                    Assert.AreEqual(tint.BodyFor(type), body.sharedMaterial, $"{body.name} wears {type}'s colour");
                    var face = body.transform.Find("TileFace");
                    Assert.IsNotNull(face, $"{body.name} has a glossy face");
                    Assert.IsNull(face.GetComponent<Collider>(), "the tile itself takes the tap");
                }
                foreach (InstrumentType other in System.Enum.GetValues(typeof(InstrumentType)))
                {
                    Assert.IsNotNull(tint.BodyFor(other), $"{other} has a body tint");
                    Assert.IsNotNull(tint.FaceFor(other), $"{other} has a face tint");
                }
            }
        }

        [Test]
        public void LeafButtonsAreToyButtonsWithACapInARing()
        {
            var buttons = Object.FindObjectsByType<ToyPress>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.GreaterOrEqual(buttons.Length, 15, "the leaf's row and the menu's");
            foreach (var b in buttons)
            {
                Assert.IsNotNull(b.Cap, $"{b.name} has a cap");
                StringAssert.EndsWith("Slab", b.Cap.name);
                var ring = b.transform.Find(b.Cap.name.Replace("Slab", "Ring"));
                Assert.IsNotNull(ring, $"{b.name} has a ring");
                var cap = b.Cap.GetComponent<Renderer>().bounds;
                var rim = ring.GetComponent<Renderer>().bounds;
                Assert.Greater(cap.max.y, rim.max.y + 0.01f, $"{b.name}: the cap stands proud of its ring");
                Assert.Greater(rim.size.x, cap.size.x, $"{b.name}: the ring is wider than the cap");
                Assert.Greater(cap.max.y - ToyPress.Depth, rim.max.y, $"{b.name}: pressed, the cap still shows above the ring");
            }
        }

        [Test]
        public void TheLeafIsModelledAndItsStalkReachesIt()
        {
            var leaf = Object.FindFirstObjectByType<IntroSequence>().Leaf.Find("Leaf");
            var mesh = leaf.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual("Leaf", mesh.name, "the Blender leaf (ArtSource/Leaf/build_leaf.py)");
            Assert.AreEqual(Vector3.one, leaf.localScale);
            Assert.AreEqual(mesh.vertexCount, mesh.colors.Length, "its greens and veins are in its vertex colours");
            Assert.IsNull(leaf.parent.Find("LeafRib"), "the veins are part of the leaf now");
            // the pointed tip is on the right, past where the old leaf ended; the midrib runs between friends and buttons
            var tip = mesh.vertices.OrderByDescending(v => v.x).First();
            Assert.Greater(tip.x, 0.53f, "the tip runs a little past the old leaf");
            Assert.AreEqual(0.02f, tip.z, 0.01f, "the tip is on the midrib, between the friends and the buttons");
            Assert.AreEqual(0.015f, mesh.vertices.Where(v => Mathf.Abs(v.x) < 0.2f && Mathf.Abs(v.z - 0.02f) > 0.04f && Mathf.Abs(v.z - 0.02f) < 0.12f).Max(v => v.y), 0.004f,
                "flat where the buttons stand, at the old leaf's top");
            var stalk = Object.FindFirstObjectByType<LeafStalk>().Stalk;
            Assert.AreEqual("Stalk", stalk.GetComponent<MeshFilter>().sharedMesh.name, "the curved stalk");
            var stalkTop = stalk.GetComponent<Renderer>().bounds.max.y;
            var leafBottom = leaf.GetComponent<Renderer>().bounds.min.y;
            Assert.GreaterOrEqual(stalkTop, leafBottom, "no gap between the stalk and the leaf");
        }

        [Test]
        public void TheChairHasACreamCushionWithoutACollider()
        {
            var seat = GameObject.Find("RoomOrchestra").transform.Find("Furniture/Chair").Cast<Transform>().First(t => t.name == "Seat");
            var cushion = seat.Find("Cushion");
            Assert.IsNotNull(cushion);
            Assert.AreEqual("Furniture_ChairCushion", cushion.GetComponent<MeshFilter>().sharedMesh.name);
            Assert.IsNull(cushion.GetComponent<Collider>(), "balls land on the seat");
            var colour = cushion.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
            Assert.Greater(colour.b, 0.6f, "a pale cream, not the chair's orange");
        }

        [Test]
        public void TheHitBloomWaitsParkedWithItsPool()
        {
            var bloom = Object.FindFirstObjectByType<HitBloom>();
            Assert.IsNotNull(bloom);
            Assert.GreaterOrEqual(bloom.Petals.Length, 20);
            var park = GameObject.Find("RoomOrchestra").transform.Find("BallDispenser/ParkPoint");
            foreach (var p in bloom.Petals)
            {
                Assert.Less(Vector3.Distance(park.position, p.position), 1e-3f, $"{p.name} waits in the cloud");
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, p.GetComponent<Renderer>().shadowCastingMode);
                Assert.IsFalse(p.GetComponent<Renderer>().enabled, "not drawn while parked");
            }
        }
    }
}
