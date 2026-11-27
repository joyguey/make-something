using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace RoomOrchestra.Tests
{
    /// <summary>Story mode's rules (owner, 2026-11-22; docs/STORY_MODE.md). Written 2026-11-22, not yet run.</summary>
    public class StoryRulesTests
    {
        [Test]
        public void BeatenByIsTheOtherWayRound()
        {
            foreach (OrbType f in new[] { OrbType.Flutter, OrbType.Bop, OrbType.Boom })
                Assert.IsTrue(StoryRules.Matches(f, StoryRules.BeatenBy(f)), $"{f} beats the Discord the Grumble spits out for it");
        }

        [Test]
        public void EachDiscordIsBeatenByExactlyOneFriend()
        {
            Assert.AreEqual(OrbType.Flutter, StoryRules.Beats(DiscordKind.Squeak));
            Assert.AreEqual(OrbType.Bop, StoryRules.Beats(DiscordKind.Wobbler));
            Assert.AreEqual(OrbType.Boom, StoryRules.Beats(DiscordKind.Clang));
            Assert.IsFalse(StoryRules.Matches(OrbType.Boom, DiscordKind.Squeak));
        }

        [Test]
        public void TheQueueDealsShuffledSetsSoNoFriendIsLongAway()
        {
            var q = new FriendQueue(5);
            var dealt = new List<OrbType> { q.Current };
            for (int i = 0; i < 299; i++) { q.Flick(); dealt.Add(q.Current); }
            for (int i = 0; i + 3 <= dealt.Count; i += 3)
                CollectionAssert.AreEquivalent(new[] { OrbType.Flutter, OrbType.Bop, OrbType.Boom }, dealt.Skip(i).Take(3), "every set of three has all three");
            Assert.AreEqual(FriendQueue.Preview, q.Next.Count, "the wrist shows the next two");
        }

        [Test]
        public void HoldSwapsWithTheHeldFriendOncePerFlick()
        {
            var q = new FriendQueue(9);
            var first = q.Current; var next = q.Next[0];
            Assert.IsTrue(q.Hold());
            Assert.AreEqual(first, q.Held, "tucked away");
            Assert.AreEqual(next, q.Current, "the next one comes onto the palm");
            Assert.IsFalse(q.Hold(), "once per flick");
            q.Flick();
            Assert.IsTrue(q.Hold());
            Assert.AreEqual(first, q.Current, "the held friend comes back");
        }

        [Test]
        public void LooseDiscordsGreyTheRoomUntilTheLevelIsLost()
        {
            var m = new DiscordMeter();
            m.Tick(1f, 0);
            Assert.AreEqual(0f, m.Level, "a clean room stays clean");
            m.Tick(5f, 2);
            Assert.AreEqual(0.4f, m.Level, 1e-4f);
            m.Beaten();
            Assert.AreEqual(0.34f, m.Level, 1e-4f, "beating one in the room clears some");
            m.Tick(100f, 3);
            Assert.IsTrue(m.Lost);
            Assert.AreEqual(1f, m.Level);
            var easy = new DiscordMeter(2f);
            easy.Tick(5f, 2);
            Assert.AreEqual(0.2f, easy.Level, 1e-4f, "assist slows it");
        }
    }
}
