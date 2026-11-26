using System;
using System.Collections.Generic;

namespace RoomOrchestra
{
    /// <summary>What Flutter says between waves (story mode teaches one Discord at a time).</summary>
    public enum StoryCue { None, Welcome, MeetSqueaks, MeetWobblers, MeetClangs, AllTogether, BossComing }

    /// <summary>One wave of Discords: which kinds, how many, how many beats apart they come through the portal.</summary>
    public sealed class StoryWave
    {
        public StoryWave(StoryCue cue, int count, int beatsApart, params DiscordKind[] kinds)
        {
            Cue = cue; Count = count; BeatsApart = Math.Max(1, beatsApart); Kinds = kinds;
        }

        public StoryCue Cue { get; }
        public int Count { get; }
        public int BeatsApart { get; }
        public IReadOnlyList<DiscordKind> Kinds { get; }
    }

    /// <summary>
    /// A story level (owner, 2026-11-25: "a polished lvl 1 version"). Level 1, The Seed: meet each Discord on its own
    /// (Squeaks for Flutter, Wobblers for Bop, Clangs for Boom), then all together, then the Grumble (the boss). The
    /// order of a mixed wave is dealt in shuffled sets of the kinds, so no kind is missing long. Pure logic, tested.
    /// </summary>
    public sealed class StoryLevel
    {
        public StoryLevel(string name, double bpm, bool boss, params StoryWave[] waves) : this(name, bpm, boss, 0f, waves) { }

        public StoryLevel(string name, double bpm, bool boss, float missCost, params StoryWave[] waves) { Name = name; Bpm = bpm; Boss = boss; MissCost = missCost; Waves = waves; }

        /// <summary>Harmony lost when a flicked friend hits nothing (owner, 2026-11-25: "in the later levels since they'll
        /// be harder, missing shots will result in more penalties"). Level 1: none, a miss only ends the streak.</summary>
        public float MissCost { get; }

        public string Name { get; }
        public double Bpm { get; }
        public bool Boss { get; }
        public IReadOnlyList<StoryWave> Waves { get; }

        public int Total { get { int n = 0; foreach (var w in Waves) n += w.Count; return n; } }

        public static StoryLevel Level1 => new StoryLevel("The Seed", MusicClock.DefaultBpm, true,
            new StoryWave(StoryCue.MeetSqueaks, 4, 6, DiscordKind.Squeak),
            new StoryWave(StoryCue.MeetWobblers, 4, 6, DiscordKind.Wobbler),
            new StoryWave(StoryCue.MeetClangs, 4, 6, DiscordKind.Clang),
            new StoryWave(StoryCue.AllTogether, 9, 4, DiscordKind.Squeak, DiscordKind.Wobbler, DiscordKind.Clang));

        /// <summary>The kinds of one wave, in the order they come (shuffled sets of the wave's kinds).</summary>
        public static List<DiscordKind> Deal(StoryWave wave, int seed)
        {
            var random = new Random(seed);
            var result = new List<DiscordKind>();
            while (result.Count < wave.Count)
            {
                var set = new List<DiscordKind>(wave.Kinds);
                for (int i = set.Count - 1; i > 0; i--) { int j = random.Next(i + 1); (set[i], set[j]) = (set[j], set[i]); }
                foreach (var k in set) if (result.Count < wave.Count) result.Add(k);
            }
            return result;
        }
    }

    /// <summary>Timing against the story's beat (from the DSP clock): how far a moment is from the nearest beat.</summary>
    public static class BeatJudge
    {
        /// <summary>Seconds a flick may be off the beat and still count as on it.</summary>
        public const double Window = 0.13;

        public static double Offset(double time, double start, double bpm)
        {
            double beat = 60.0 / bpm;
            double phase = (time - start) / beat;
            double off = phase - Math.Round(phase);
            return Math.Abs(off) * beat;
        }

        public static bool OnBeat(double time, double start, double bpm, double window = Window) => Offset(time, start, bpm) <= window;

        /// <summary>The time of the next beat at or after <paramref name="time"/>.</summary>
        public static double NextBeat(double time, double start, double bpm)
        {
            double beat = 60.0 / bpm;
            double n = Math.Ceiling((time - start) / beat - 1e-9);
            return start + Math.Max(0, n) * beat;
        }
    }

    /// <summary>
    /// Harmony (owner's plan: "on-beat flicks fill Harmony; full Harmony gives a Chorus super"; headset 2026-11-25: "a
    /// bar that charges up with a streak of successful hits"). Matches fill it, on-beat ones more, and a streak of
    /// matches in a row more again; full, the next flick is a Chorus (all three friends at once). A wrong friend costs a
    /// little and ends the streak.
    /// </summary>
    public sealed class HarmonyMeter
    {
        // headset 2026-11-25: "a wrong hit should reset the streak": a wrong friend costs a good piece and ends the
        // streak; a friend that hits nothing ends the streak too
        public const float PerMatch = 0.1f, OnBeatBonus = 0.06f, PerStreak = 0.03f, WrongCost = 0.15f;
        public const int StreakCap = 5;

        public float Level { get; private set; }
        public bool Full => Level >= 1f;
        /// <summary>Matches in a row without a wrong friend.</summary>
        public int Streak { get; private set; }

        public void Matched(bool onBeat)
        {
            Level = Math.Min(1f, Level + PerMatch + (onBeat ? OnBeatBonus : 0f) + PerStreak * Math.Min(Streak, StreakCap));
            Streak++;
        }

        public void Wrong() { Level = Math.Max(0f, Level - WrongCost); Streak = 0; }

        /// <summary>A friend flew and hit nothing: the streak ends, and harder levels take <paramref name="cost"/> Harmony.</summary>
        public void Missed(float cost = 0f) { Streak = 0; Level = Math.Max(0f, Level - Math.Max(0f, cost)); }

        /// <summary>Uses a full meter for a Chorus; false (and nothing spent) if it isn't full.</summary>
        public bool Spend()
        {
            if (!Full) return false;
            Level = 0f;
            return true;
        }
    }

    public enum BossHit { Wrong, Damaged, ShellBroken, Defeated }

    /// <summary>
    /// The Grumble, Level 1's boss: three shells, one after another, each beaten only by one friend (its shape shows
    /// which: a spiky shell for Flutter, a wobbly one for Bop, an armour plate for Boom). Each shell takes
    /// <see cref="HitsPerShell"/> matching hits (headset 2026-11-25: "the boss should take longer to defeat"). A wrong
    /// friend bounces off. A Chorus breaks one shell, never more.
    /// </summary>
    public sealed class BossDiscord
    {
        public const int HitsPerShell = 4;
        public static readonly DiscordKind[] Shells = { DiscordKind.Squeak, DiscordKind.Wobbler, DiscordKind.Clang };

        public int ShellIndex { get; private set; }
        public int HitsLeft { get; private set; } = HitsPerShell;
        public bool Defeated => ShellIndex >= Shells.Length;
        /// <summary>The shell now on (what it is weak to: <see cref="StoryRules.Beats"/>).</summary>
        public DiscordKind Shell => Shells[Math.Min(ShellIndex, Shells.Length - 1)];

        public BossHit Hit(OrbType friend)
        {
            if (Defeated) return BossHit.Defeated;
            if (!StoryRules.Matches(friend, Shell)) return BossHit.Wrong;
            if (--HitsLeft > 0) return BossHit.Damaged;
            ShellIndex++;
            HitsLeft = HitsPerShell;
            return Defeated ? BossHit.Defeated : BossHit.ShellBroken;
        }

        /// <summary>A Chorus breaks the shell that is on, whatever it is.</summary>
        public BossHit Chorus()
        {
            if (Defeated) return BossHit.Defeated;
            ShellIndex++;
            HitsLeft = HitsPerShell;
            return Defeated ? BossHit.Defeated : BossHit.ShellBroken;
        }
    }

    /// <summary>Stars for a won level: three for a clean room, fewer as the Discord meter climbed or friends missed.</summary>
    public static class StoryScore
    {
        public static int Stars(bool won, float meterPeak, int wrong)
        {
            if (!won) return 0;
            int stars = 3;
            if (meterPeak > 0.25f || wrong > 4) stars--;
            if (meterPeak > 0.6f || wrong > 10) stars--;
            return stars;
        }
    }
}
