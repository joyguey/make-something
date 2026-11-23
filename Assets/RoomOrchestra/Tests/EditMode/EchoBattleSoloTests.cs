using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// Echo Battle without the secret (owner, 2026-11-22: "an easier way to access than the easter egg", smooth "even
    /// though I don't have two headsets"): Menu > Play together, and SOLO against the game.
    /// </summary>
    public class EchoBattleSoloTests
    {
        [Test]
        public void ThePartnersCallsGrowFromTwoNotesToFourOnEighthNotes()
        {
            using var partner = new EchoBattlePartner(47901, 47902, seed: 7);
            for (int round = 1; round <= EchoBattleRules.Rounds; round++)
                for (int k = 0; k < 50; k++)
                {
                    var call = partner.Compose(round);
                    Assert.AreEqual(round + 1, call.Count, $"round {round}");
                    Assert.IsTrue(call.All(n => n.Step % 2 == 0), "on 8th notes: easy to follow");
                    CollectionAssert.AllItemsAreUnique(call.Select(n => n.Step));
                    CollectionAssert.IsOrdered(call.Select(n => n.Step));
                    Assert.IsTrue(call.All(n => n.Note >= 0 && n.Note < NoteMapping.Count));
                    Assert.IsTrue(EchoBattleRules.TryDecode(EchoBattleRules.Encode(call), out var back) && back.SequenceEqual(call), "it travels like a player's call");
                }
        }

        [Test]
        public void JoiningPutsTheHostsInstrumentsOnTheFurnitureByItself()
        {
            const InstrumentType U = InstrumentType.Udu, M = InstrumentType.Mbira, E = InstrumentType.Erhu, H = InstrumentType.Handpan, O = InstrumentType.Ocarina, G = InstrumentType.Guzheng;
            CollectionAssert.AreEqual(new[] { U, H, E }, EchoBattleRules.MatchPlaces(new[] { U, H, E }, new[] { U, M, E }), "only the odd one out changes");
            CollectionAssert.AreEqual(new[] { H, O, G }, EchoBattleRules.MatchPlaces(new[] { H, O, G }, new[] { U, M, E }), "none matching: the host's, in order");
            CollectionAssert.AreEqual(new[] { E, U, M }, EchoBattleRules.MatchPlaces(new[] { U, M, E }, new[] { E, U, M }), "all there: nothing moves");
            CollectionAssert.AreEqual(new[] { U, M }, EchoBattleRules.MatchPlaces(new[] { U, M, E }, new[] { U, M }), "fewer pieces: as many as fit");
            CollectionAssert.AreEqual(new[] { U, H, E }, EchoBattleRules.MatchPlaces(new[] { U, H, E }, new[] { U, U, E }), "a repeat gives way");
            foreach (var room in new[] { new[] { U, M, E }, new[] { O, H, G }, new[] { M, U, E } })
                Assert.AreEqual(0, EchoBattleRules.Missing(new[] { G, M, O }, EchoBattleRules.MatchPlaces(new[] { G, M, O }, room), 3).Count, "always ready afterwards");
        }

        [Test]
        public void ThePartnerIsBeatableButNoPushover()
        {
            using var partner = new EchoBattlePartner(47903, 47904, seed: 11);
            int got = 0, most = 0;
            for (int k = 0; k < 400; k++)
            {
                var call = partner.Compose(1 + k % 3);
                got += EchoBattleRules.Score(call, partner.Imitate(call));
                most += EchoBattleRules.MaxScore(call.Count);
            }
            float share = got / (float)most;
            Assert.That(share, Is.InRange(0.55f, 0.85f), $"it scores {share:P0} of what a perfect echo would");
        }

        [Test]
        public void PlayTogetherIsOnTheMenuAndTheCardHasSoloBesideHostJoinAndLeave()
        {
            EditorSceneManager.OpenScene("Assets/RoomOrchestra/Scenes/RoomOrchestra.unity", OpenSceneMode.Single);
            var together = Object.FindFirstObjectByType<TogetherButton>(FindObjectsInactive.Include);
            Assert.IsNotNull(together, "Play together on the menu");
            Assert.IsNotNull(together.GetComponentInParent<MenuTray>(true), "on the menu tray, not crowding the leaf");
            Assert.IsNotNull(together.transform.Find("TogetherIcon"), "with its two-people icon");
            Assert.IsNotNull(together.GetComponentInChildren<Collider>(true), "tappable");

            var director = Object.FindFirstObjectByType<EchoBattleDirector>();
            var buttons = director.Card.GetComponentsInChildren<EchoBattleButton>(true);
            CollectionAssert.AreEquivalent(new[] { EchoBattleButton.Kind.Host, EchoBattleButton.Kind.Join, EchoBattleButton.Kind.Solo, EchoBattleButton.Kind.Leave }, buttons.Select(b => b.ButtonKind));
            // all four stickers on the poster, none overlapping (in the card's own space)
            var panel = director.Card.Find("BattlePanel");
            float half = panel.localScale.x / 2f;
            var spans = new List<(float min, float max)>();
            foreach (var b in buttons)
            {
                var edge = b.transform.Find($"Battle{b.ButtonKind}Edge");
                float x = b.transform.localPosition.x, w = edge.localScale.x / 2f + 0.004f; // a little for the tilt
                Assert.Less(Mathf.Abs(x) + w, half, $"{b.ButtonKind} stays on the poster");
                spans.Add((x - w, x + w));
            }
            spans.Sort();
            for (int i = 1; i < spans.Count; i++) Assert.Less(spans[i - 1].max, spans[i].min, "stickers don't overlap");
        }
    }
}
