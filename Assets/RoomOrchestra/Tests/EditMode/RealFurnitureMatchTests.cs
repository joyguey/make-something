using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>After the lesson, each instrument picks a real piece of furniture (owner, 2026-11-07): pretend rooms.</summary>
    public class RealFurnitureMatchTests
    {
        static FurniturePiece Piece(FurnitureKind kind, Vector3 top, Vector3 size, int cells = 50)
        {
            var box = new Bounds(new Vector3(top.x, top.y - size.y / 2f, top.z), size);
            return new FurniturePiece(kind, box, top.y, cells);
        }

        static readonly (string, Vector3)[] Stations =
        {
            ("Table", new Vector3(-1.1f, 0f, 2.5f)),
            ("Chair", new Vector3(0f, 0f, 2.2f)),
            ("Lamp", new Vector3(1.1f, 0f, 2.5f)),
        };

        [Test]
        public void EachInstrumentFindsItsOwnKind()
        {
            var room = new List<FurniturePiece>
            {
                Piece(FurnitureKind.Lamp, new Vector3(2f, 1.5f, 1.5f), new Vector3(0.4f, 1.5f, 0.4f)),
                Piece(FurnitureKind.Chair, new Vector3(-1f, 0.9f, 1.5f), new Vector3(0.5f, 0.9f, 0.5f)),
                Piece(FurnitureKind.Table, new Vector3(0f, 0.75f, 2f), new Vector3(1.2f, 0.75f, 0.7f)),
            };
            var picks = RealFurniture.Match(Stations, room, Vector3.zero, Vector3.forward);
            Assert.AreEqual(FurnitureKind.Table, picks[0]?.Kind);
            Assert.AreEqual(FurnitureKind.Chair, picks[1]?.Kind);
            Assert.AreEqual(FurnitureKind.Lamp, picks[2]?.Kind);
        }

        [Test]
        public void NoPieceIsSharedAndMissingOnesStayVirtual()
        {
            var room = new List<FurniturePiece> { Piece(FurnitureKind.Sofa, new Vector3(0f, 0.8f, 2.5f), new Vector3(2f, 0.8f, 0.9f)) };
            var picks = RealFurniture.Match(Stations, room, Vector3.zero, Vector3.forward);
            int used = 0;
            foreach (var p in picks) if (p.HasValue) used++;
            Assert.AreEqual(1, used, "one sofa, one instrument");
            Assert.AreEqual(FurnitureKind.Sofa, picks[1]?.Kind, "the chair's mbira prefers the sofa over the table's udu");
            Assert.IsFalse(picks[2].HasValue, "no lamp-like piece: the erhu stays where it is");
        }

        [Test]
        public void OutOfReachOrTooHighIsSkipped()
        {
            var room = new List<FurniturePiece>
            {
                Piece(FurnitureKind.Table, new Vector3(0f, 0.75f, 6f), new Vector3(1f, 0.75f, 1f)),   // across a big room
                Piece(FurnitureKind.Table, new Vector3(0.2f, 0.75f, 0.2f), new Vector3(1f, 0.75f, 1f)), // the player is sitting at it
                Piece(FurnitureKind.Lamp, new Vector3(1f, 2.9f, 2f), new Vector3(0.3f, 0.3f, 0.3f)),  // a ceiling light
            };
            var picks = RealFurniture.Match(Stations, room, Vector3.zero, Vector3.forward);
            Assert.IsFalse(picks[0].HasValue);
            Assert.IsFalse(picks[2].HasValue);
        }

        [Test]
        public void InFrontBeatsBehind()
        {
            var room = new List<FurniturePiece>
            {
                Piece(FurnitureKind.Table, new Vector3(0f, 0.75f, -2f), new Vector3(1f, 0.75f, 1f)),
                Piece(FurnitureKind.Table, new Vector3(-2.5f, 0.75f, 1.5f), new Vector3(1f, 0.75f, 1f)),
            };
            var picks = RealFurniture.Match(Stations, room, Vector3.zero, Vector3.forward);
            Assert.AreEqual(-2.5f, picks[0].Value.Top.x, 0.01f);
        }

        [Test]
        public void ScanLinesHaveBothLanguages()
        {
            foreach (var term in new[] { UiTerm.ScanLook, UiTerm.ScanFound, UiTerm.ScanNone })
            {
                Assert.IsNotEmpty(UiText.Get(term, UiLanguage.English));
                Assert.IsNotEmpty(UiText.Get(term, UiLanguage.Chinese));
            }
        }
    }
}
