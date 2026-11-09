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
            Assert.AreEqual(InstrumentType.Mbira, InstrumentPacks.Next(InstrumentType.Udu));
            Assert.AreEqual(InstrumentType.Erhu, InstrumentPacks.Next(InstrumentType.Mbira));
            Assert.AreEqual(InstrumentType.Udu, InstrumentPacks.Next(InstrumentType.Erhu));
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
            CollectionAssert.AreEqual(new[] { InstrumentType.Udu, InstrumentType.Mbira, InstrumentType.Erhu }, packs.Furniture.Select(f => f.Instrument), "table, chair, lamp");
            var changes = Object.FindObjectsByType<ChangeInstrumentButton>(FindObjectsSortMode.None);
            CollectionAssert.AreEquivalent(packs.Furniture, changes.Select(c => c.Target), "one Change button per furniture");
            Assert.IsNotNull(Object.FindFirstObjectByType<SoundPackButton>(), "a Sounds button on the leaf");
        }
    }
}
