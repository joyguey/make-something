using System.Linq;
using NUnit.Framework;

namespace RoomOrchestra.Tests
{
    /// <summary>Story mode's Level 1 rules: waves, beat timing, Harmony, the Grumble, stars and sounds.</summary>
    public class StoryLevelTests
    {
        [Test]
        public void LevelOneMeetsEachDiscordAloneThenAllTogether()
        {
            var level = StoryLevel.Level1;
            Assert.AreEqual(4, level.Waves.Count);
            CollectionAssert.AreEqual(new[] { DiscordKind.Squeak }, level.Waves[0].Kinds);
            CollectionAssert.AreEqual(new[] { DiscordKind.Wobbler }, level.Waves[1].Kinds);
            CollectionAssert.AreEqual(new[] { DiscordKind.Clang }, level.Waves[2].Kinds);
            Assert.AreEqual(3, level.Waves[3].Kinds.Count);
            Assert.IsTrue(level.Boss, "the Grumble at the end");
            Assert.AreEqual(MusicClock.DefaultBpm, level.Bpm);
        }

        [Test]
        public void AMixedWaveDealsEveryKindInEachSetOfThree()
        {
            var dealt = StoryLevel.Deal(StoryLevel.Level1.Waves[3], 11);
            Assert.AreEqual(9, dealt.Count);
            for (int i = 0; i < 9; i += 3)
                CollectionAssert.AreEquivalent(new[] { DiscordKind.Squeak, DiscordKind.Wobbler, DiscordKind.Clang }, dealt.Skip(i).Take(3));
        }

        [Test]
        public void OnTheBeatMeansCloseToABeat()
        {
            double beat = 60.0 / 88.0;
            Assert.IsTrue(BeatJudge.OnBeat(10.0 + 4 * beat + 0.05, 10.0, 88));
            Assert.IsFalse(BeatJudge.OnBeat(10.0 + 4.5 * beat, 10.0, 88), "half way between");
            Assert.AreEqual(10.0 + 5 * beat, BeatJudge.NextBeat(10.0 + 4.2 * beat, 10.0, 88), 1e-9);
            Assert.AreEqual(10.0, BeatJudge.NextBeat(9.0, 10.0, 88), 1e-9, "before the start: the first beat");
        }

        [Test]
        public void HarmonyFillsWithMatchesFasterOnTheBeatAndPaysForAChorus()
        {
            var h = new HarmonyMeter();
            Assert.IsFalse(h.Spend());
            int onBeat = 0; while (!h.Full) { h.Matched(true); onBeat++; }
            var slow = new HarmonyMeter(); int offBeat = 0; while (!slow.Full) { slow.Matched(false); offBeat++; }
            Assert.Less(onBeat, offBeat, "on the beat fills it sooner");
            Assert.IsTrue(h.Spend());
            Assert.AreEqual(0f, h.Level);
            h.Matched(false); for (int i = 0; i < 10; i++) h.Wrong();
            Assert.AreEqual(0f, h.Level, 1e-5f, "never below empty");
            Assert.AreEqual(0, h.Streak, "a wrong friend ends the streak");
            var miss = new HarmonyMeter(); miss.Matched(false); miss.Matched(false); float kept = miss.Level; miss.Missed();
            Assert.AreEqual(0, miss.Streak, "a friend that hits nothing ends the streak");
            Assert.AreEqual(kept, miss.Level, "but keeps the Harmony");
            Assert.AreEqual(0f, StoryLevel.Level1.MissCost, "Level 1 is gentle about misses");
            miss.Matched(false); miss.Matched(false); float before = miss.Level; miss.Missed(0.1f);
            Assert.AreEqual(before - 0.1f, miss.Level, 1e-5f, "harder levels take Harmony for a miss");
            var streak = new HarmonyMeter(); streak.Matched(false); streak.Matched(false); streak.Matched(false);
            Assert.AreEqual(3, streak.Streak);
            Assert.Greater(streak.Level, 3 * HarmonyMeter.PerMatch + 1e-4f, "a streak fills it faster");
        }

        [Test]
        public void TheGrumblesShellsNeedTheRightFriendTwiceEach()
        {
            var g = new BossDiscord();
            Assert.AreEqual(DiscordKind.Squeak, g.Shell);
            Assert.AreEqual(BossHit.Wrong, g.Hit(OrbType.Boom));
            for (int i = 1; i < g.HitsPerShell; i++) Assert.AreEqual(BossHit.Damaged, g.Hit(OrbType.Flutter));
            Assert.AreEqual(BossHit.ShellBroken, g.Hit(OrbType.Flutter));
            Assert.GreaterOrEqual(g.HitsPerShell, 4, "the boss takes a while (headset 2026-11-25)");
            Assert.AreEqual(DiscordKind.Wobbler, g.Shell);
            Assert.AreEqual(BossHit.ShellBroken, g.Chorus(), "a Chorus breaks a shell");
            Assert.AreEqual(DiscordKind.Clang, g.Shell);
            for (int i = 1; i < g.HitsPerShell; i++) g.Hit(OrbType.Boom);
            Assert.AreEqual(BossHit.Defeated, g.Hit(OrbType.Boom));
            Assert.IsTrue(g.Defeated);
        }

        [Test]
        public void StarsRewardACleanRoom()
        {
            Assert.AreEqual(3, StoryScore.Stars(true, 0.1f, 2));
            Assert.AreEqual(2, StoryScore.Stars(true, 0.4f, 2));
            Assert.AreEqual(1, StoryScore.Stars(true, 0.7f, 12));
            Assert.AreEqual(0, StoryScore.Stars(false, 0f, 0));
        }

        [Test]
        public void EveryStorySoundIsShortSoftAndEndsInSilence()
        {
            foreach (StorySound s in System.Enum.GetValues(typeof(StorySound)))
            {
                var d = StorySoundSynth.Render(s, 44100);
                Assert.Greater(d.Length, 100, s.ToString());
                Assert.Less(d.Length, 44100 * 3, $"{s} is short");
                Assert.LessOrEqual(d.Max(v => System.Math.Abs(v)), 0.91f, $"{s} below full scale");
                Assert.AreEqual(0f, d[d.Length - 1], 1e-3f, $"{s} ends in silence");
                Assert.LessOrEqual(StorySoundSynth.Level(s), 0.5f);
            }
        }
        [Test]
        public void LevelOneRestoresTheUduWithOneRealFactPerWave()
        {
            // owner, 2026-11-25: each level fixes one traditional instrument; real facts, gently told; a bonus, never a lock
            var lore = StoryLore.Levels[0];
            Assert.AreEqual(InstrumentType.Udu, lore.Instrument);
            Assert.AreEqual(StoryLevel.Level1.Waves.Count, lore.Facts.Count, "one fact between each wave");
            foreach (var f in lore.Facts) Assert.IsFalse(string.IsNullOrWhiteSpace(f));
            StringAssert.Contains("Igbo", lore.People);
            Assert.AreEqual("LittleEchoes.Story.Restored.Udu", StoryLore.RestoredKey(InstrumentType.Udu));
        }
        [Test]
        public void LevelTwoRestoresTheErhuAndIsHarder()
        {
            var one = StoryLevel.Level1; var two = StoryLevel.Level2;
            Assert.AreEqual(2, two.Number);
            Assert.AreEqual(InstrumentType.Erhu, StoryLore.Levels[1].Instrument, "owner, 2026-11-26: Level 2 is the erhu");
            Assert.AreEqual(MusicRole.Tune, StoryLore.Levels[1].Role, "its mending is heard in the tune");
            Assert.AreEqual(two.Waves.Count, StoryLore.Levels[1].Facts.Count);
            Assert.Greater(two.Total, one.Total, "more Discords");
            Assert.Greater(two.Speed, one.Speed, "faster");
            Assert.Greater(two.MissCost, 0f, "a miss costs Harmony");
            Assert.Greater(two.Waves[0].Kinds.Count, 1, "mixed from the first wave");
            var boss = two.NewBoss();
            Assert.Greater(boss.Shells.Count * boss.HitsPerShell, one.NewBoss().Shells.Count * one.NewBoss().HitsPerShell, "a tougher Grumble");
            Assert.AreEqual(StoryLevel.All.Length, StoryLore.Levels.Length, "each level restores one instrument");
        }
        [Test]
        public void ThreeLevelsTheLastIsTheHandpanAndTheHardest()
        {
            // owner, 2026-11-26: "I only want 3 levels", the handpan instead of the mbira
            Assert.AreEqual(3, StoryLevel.All.Length);
            Assert.AreEqual(3, StoryLore.Levels.Length);
            CollectionAssert.AreEqual(new[] { InstrumentType.Udu, InstrumentType.Erhu, InstrumentType.Handpan }, StoryLore.Levels.Select(l => l.Instrument));
            CollectionAssert.AreEquivalent(Instruments.Original, StoryLore.Levels.Select(l => l.Instrument), "the story mends the sandbox's own three");
            Assert.AreEqual(MusicRole.Chords, StoryLore.Handpan.Role);
            var two = StoryLevel.Level2; var three = StoryLevel.Level3;
            Assert.Greater(three.Total, two.Total);
            Assert.Greater(three.Speed, two.Speed);
            Assert.Greater(three.MissCost, two.MissCost);
            Assert.Greater(three.NewBoss().Shells.Count, two.NewBoss().Shells.Count, "the Grumble's last stand");
            Assert.AreEqual(three.Waves.Count, StoryLore.Handpan.Facts.Count);
            StringAssert.Contains("Hang", StoryLore.Handpan.Facts[0], "its makers' name for it");
        }
    }
}
