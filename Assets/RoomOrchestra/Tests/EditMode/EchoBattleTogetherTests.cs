using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>Echo Battle without the secret (owner, 2026-11-22: "an easier way to access than the easter egg"): Menu >
    /// Play together; and joining puts the host's instruments on the furniture by itself.</summary>
    public class EchoBattleTogetherTests
    {
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
        public void PlayTogetherIsOnTheMenuAndTheCardKeepsHostJoinAndLeave()
        {
            EditorSceneManager.OpenScene("Assets/RoomOrchestra/Scenes/RoomOrchestra.unity", OpenSceneMode.Single);
            var together = Object.FindFirstObjectByType<TogetherButton>(FindObjectsInactive.Include);
            Assert.IsNotNull(together, "Play together on the menu");
            Assert.IsNotNull(together.GetComponentInParent<MenuTray>(true), "on the menu tray, not crowding the leaf");
            Assert.IsNotNull(together.transform.Find("TogetherIcon"), "with its two-people icon");
            Assert.IsNotNull(together.GetComponentInChildren<Collider>(true), "tappable");

            var director = Object.FindFirstObjectByType<EchoBattleDirector>();
            var buttons = director.Card.GetComponentsInChildren<EchoBattleButton>(true);
            CollectionAssert.AreEquivalent(new[] { EchoBattleButton.Kind.Host, EchoBattleButton.Kind.Join, EchoBattleButton.Kind.Leave }, buttons.Select(b => b.ButtonKind));
            // the stickers on the poster, none overlapping (in the card's own space)
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
