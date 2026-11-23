using System;
using System.Collections.Generic;

namespace RoomOrchestra
{
    /// <summary>The kinds of Discord (story mode's noisy enemies), each beaten by one friend's power.</summary>
    public enum DiscordKind
    {
        Squeak, // tiny, fast, flying in swarms: Flutter's piercing arc
        Wobbler, // dodgy, ducks behind things: Bop's ricochet
        Clang, // armoured, shielded: Boom's shockwave
    }

    /// <summary>
    /// Story mode's rules (owner, 2026-11-22; plan in docs/STORY_MODE.md). Pure logic, no Unity: the friend queue with
    /// Hold, matching a flicked friend to a Discord, and the room's Discord meter. Tested in EditMode (StoryRulesTests).
    /// </summary>
    public static class StoryRules
    {
        /// <summary>The friend whose power beats <paramref name="kind"/>.</summary>
        public static OrbType Beats(DiscordKind kind) => kind switch
        {
            DiscordKind.Squeak => OrbType.Flutter,
            DiscordKind.Wobbler => OrbType.Bop,
            _ => OrbType.Boom,
        };

        public static bool Matches(OrbType friend, DiscordKind kind) => Beats(kind) == friend;
    }

    /// <summary>
    /// The friends dealt to the player's palm: shuffled sets of all three (never a long wait for the one you need), the
    /// current one on the palm, the next two shown on the wrist, and one Hold slot (flip the hand palm-down and up to
    /// swap the current friend with the held one; owner chose Hold, 2026-11-22). Hold can be used once per flick.
    /// </summary>
    public sealed class FriendQueue
    {
        public const int Preview = 2;
        static readonly OrbType[] All = { OrbType.Flutter, OrbType.Bop, OrbType.Boom };

        readonly Random m_Random;
        readonly List<OrbType> m_Upcoming = new();
        OrbType? m_Held;
        bool m_HoldUsed;

        public FriendQueue(int seed)
        {
            m_Random = new Random(seed);
            Refill();
            Current = Take();
        }

        /// <summary>The friend on the palm, ready to flick.</summary>
        public OrbType Current { get; private set; }
        public OrbType? Held => m_Held;
        public bool CanHold => !m_HoldUsed;

        /// <summary>The next friends after the current one (for the wrist).</summary>
        public IReadOnlyList<OrbType> Next
        {
            get
            {
                while (m_Upcoming.Count < Preview) Refill();
                return m_Upcoming.GetRange(0, Preview);
            }
        }

        /// <summary>The current friend was flicked: the next comes onto the palm; Hold is available again.</summary>
        public OrbType Flick()
        {
            var flicked = Current;
            Current = Take();
            m_HoldUsed = false;
            return flicked;
        }

        /// <summary>Swap the current friend with the held one (the first Hold takes the next friend). Once per flick.</summary>
        public bool Hold()
        {
            if (m_HoldUsed) return false;
            var was = Current;
            Current = m_Held ?? Take();
            m_Held = was;
            m_HoldUsed = true;
            return true;
        }

        OrbType Take()
        {
            if (m_Upcoming.Count == 0) Refill();
            var f = m_Upcoming[0];
            m_Upcoming.RemoveAt(0);
            return f;
        }

        void Refill()
        {
            var set = new List<OrbType>(All);
            for (int i = set.Count - 1; i > 0; i--)
            {
                int j = m_Random.Next(i + 1);
                (set[i], set[j]) = (set[j], set[i]);
            }
            m_Upcoming.AddRange(set);
        }
    }

    /// <summary>
    /// How much Discord has got into the player's room (owner: "a threshold to how much discord can be left inside the
    /// room before the level is lost"). Each escaped Discord adds to it every second it drifts in the room; beating one
    /// in the room takes some away. Full = the level is lost. Shown as the room greying, never only as a number.
    /// </summary>
    public sealed class DiscordMeter
    {
        /// <summary>Meter gained per second per Discord loose in the room; lost when one is beaten there.</summary>
        public const float PerSecondEach = 0.04f, BeatenRelief = 0.06f;

        public DiscordMeter(float assist = 1f) { Assist = Math.Max(0.25f, assist); }

        /// <summary>Above 1 slows the meter (assist mode).</summary>
        public float Assist { get; }
        /// <summary>0 = clean room, 1 = lost.</summary>
        public float Level { get; private set; }
        public bool Lost => Level >= 1f;

        public void Tick(float seconds, int looseInRoom)
        {
            if (Lost || looseInRoom <= 0 || seconds <= 0f) return;
            Level = Math.Min(1f, Level + PerSecondEach * looseInRoom * seconds / Assist);
        }

        public void Beaten() => Level = Math.Max(0f, Level - BeatenRelief);
    }
}
