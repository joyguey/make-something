using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>Instrument packs and instrument places: the rules, and the scene as saved.</summary>
    public class InstrumentPackTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";

        [Test]
        public void PlacesAreRememberedAsAFullArrangementOrNotAtAll()
        {
            CollectionAssert.AreEqual(new[] { InstrumentType.Erhu, InstrumentType.Udu, InstrumentType.Mbira },
                InstrumentPacks.ParsePlaces("Erhu,Udu,Mbira", 3));
            Assert.AreEqual("Udu,Mbira,Erhu", InstrumentPacks.FormatPlaces(new[] { InstrumentType.Udu, InstrumentType.Mbira, InstrumentType.Erhu }));
            Assert.IsNull(InstrumentPacks.ParsePlaces("", 3));
            Assert.IsNull(InstrumentPacks.ParsePlaces("Udu,Udu,Erhu", 3), "each instrument once");
            Assert.IsNull(InstrumentPacks.ParsePlaces("Udu,Mbira", 3), "every furniture");
            Assert.IsNull(InstrumentPacks.ParsePlaces("Udu,Banjo,Erhu", 3), "known instruments only");
        }

        [Test]
        public void ChangeCyclesThroughEveryInstrument()
        {
            // the first three, then the V4 library (owner, 2026-11-11), and round again
            var order = new[] { InstrumentType.Udu, InstrumentType.Mbira, InstrumentType.Erhu, InstrumentType.Ocarina, InstrumentType.Handpan,
                                InstrumentType.Guzheng, InstrumentType.Balafon, InstrumentType.SlitDrum };
            for (int i = 0; i < order.Length; i++)
                Assert.AreEqual(order[(i + 1) % order.Length], InstrumentPacks.Next(order[i]));
            Assert.AreEqual(8, Instruments.Count);
        }

        // owner, 2026-11-11: a small picture of every instrument, for names people may never have heard
        [Test]
        public void EveryInstrumentHasAnIconAndEveryColumnShowsOne()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var library = Object.FindFirstObjectByType<InstrumentIconLibrary>();
            Assert.IsNotNull(library);
            foreach (InstrumentType type in System.Enum.GetValues(typeof(InstrumentType)))
            {
                var mesh = library.MeshFor(type);
                Assert.IsNotNull(mesh, $"{type} has an icon");
                Assert.Less(mesh.bounds.size.magnitude, 0.25f, $"{type}'s icon is small");
                Assert.AreEqual(mesh.vertexCount, mesh.colors32.Length, $"{type}'s icon carries its colours and baked AO");
            }
            foreach (var column in Object.FindObjectsByType<PitchColumn>(FindObjectsSortMode.None))
                Assert.IsNotNull(column.GetComponentInChildren<InstrumentIcon>(), $"{column.name} shows its instrument's picture");
        }

        [Test]
        public void TheSceneHasTwoFullPacksAndAChangeButtonPerInstrument()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var packs = Object.FindFirstObjectByType<InstrumentPacks>();
            Assert.IsNotNull(packs);
            Assert.AreEqual(2, packs.Packs.Count, "Classic and Earthy");
            CollectionAssert.AreEqual(new[] { UiTerm.PackClassic, UiTerm.PackEarthy }, packs.Packs.Select(p => p.name));
            foreach (var pack in packs.Packs)
                foreach (InstrumentType type in System.Enum.GetValues(typeof(InstrumentType)))
                {
                    var clips = pack.For(type);
                    Assert.AreEqual(NoteMapping.Count, clips.Length, $"{pack.name} {type}");
                    Assert.IsTrue(clips.All(c => c != null), $"{pack.name} {type} has every note");
                    Assert.That(pack.VolumeFor(type), Is.InRange(0.1f, 1f), $"{pack.name} {type} volume");
                }
            Assert.AreEqual(3, packs.Furniture.Count);
            CollectionAssert.AreEqual(Instruments.Original, packs.Furniture.Select(f => f.Instrument), "table, chair, lamp");
            var changes = Object.FindObjectsByType<ChangeInstrumentButton>(FindObjectsSortMode.None);
            CollectionAssert.AreEquivalent(packs.Furniture, changes.Select(c => c.Target), "one Change button per furniture");
            Assert.IsNotNull(Object.FindFirstObjectByType<SoundPackButton>(), "a Sounds button on the leaf");
        }
    }
}
