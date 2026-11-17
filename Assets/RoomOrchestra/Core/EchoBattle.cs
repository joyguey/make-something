using System;
using System.Collections.Generic;
using System.Text;

namespace RoomOrchestra
{
    /// <summary>One note of a battle phrase: which instrument, which note (or udu sound) and which 16th of the bar.</summary>
    public readonly struct PhraseNote : IEquatable<PhraseNote>
    {
        public readonly InstrumentType Instrument;
        public readonly int Note;
        public readonly int Step;

        public PhraseNote(InstrumentType instrument, int note, int step)
        {
            Instrument = instrument;
            Note = note;
            Step = ((step % MusicClock.StepsPerBar) + MusicClock.StepsPerBar) % MusicClock.StepsPerBar;
        }

        public bool Equals(PhraseNote other) => Instrument == other.Instrument && Note == other.Note && Step == other.Step;
        public override bool Equals(object obj) => obj is PhraseNote other && Equals(other);
        public override int GetHashCode() => ((int)Instrument * 31 + Note) * 31 + Step;
        public override string ToString() => $"{Instrument} {Note}@{Step}";
    }

    /// <summary>
    /// The Echo Battle's rules (easter egg, Version 3 nice-to-have; owner 2026-10-30: a turn-based echo battle over local
    /// Wi-Fi as the first multiplayer step). One player calls a phrase of up to <see cref="MaxNotes"/> notes in one bar;
    /// the other hears it and echoes it in the next. Each call note scores when the echo has the same instrument and note
    /// on the same step (2 points) or one 16th away (1 point). Players swap; <see cref="Rounds"/> rounds each. Judging is
    /// local to the echoing headset (it has both phrases), so Wi-Fi delay never decides a point. Pure rules.
    /// </summary>
    public static class EchoBattleRules
    {
        public const int MaxNotes = 4, Rounds = 3;
        public const int ExactPoints = 2, NearPoints = 1, StepTolerance = 1;

        public static int MaxScore(int callNotes) => callNotes * ExactPoints;

        /// <summary>Points for <paramref name="echo"/> against <paramref name="call"/>: each call note matched at most once.</summary>
        /// <param name="inThisRoom">Which instruments the echoing player has on their furniture (null: all). A called
        /// note on an instrument they don't have is matched by the same note on any of theirs: the two rooms need not
        /// hold the same instruments (V4 feedback: with different instruments the echo could never score).</param>
        public static int Score(IReadOnlyList<PhraseNote> call, IReadOnlyList<PhraseNote> echo, Func<InstrumentType, bool> inThisRoom = null)
        {
            if (call == null || echo == null) return 0;
            bool SameSound(PhraseNote a, PhraseNote b) =>
                a.Note == b.Note && (a.Instrument == b.Instrument || (inThisRoom != null && !inThisRoom(a.Instrument)));
            var used = new bool[echo.Count];
            int points = 0;
            // Exact matches first, so a near echo never steals an exact one.
            var matched = new bool[call.Count];
            for (int c = 0; c < call.Count; c++)
                for (int e = 0; e < echo.Count; e++)
                    if (!used[e] && SameSound(call[c], echo[e]) && call[c].Step == echo[e].Step)
                    {
                        used[e] = true;
                        matched[c] = true;
                        points += ExactPoints;
                        break;
                    }
            for (int c = 0; c < call.Count; c++)
            {
                if (matched[c]) continue;
                for (int e = 0; e < echo.Count; e++)
                    if (!used[e] && SameSound(call[c], echo[e]) && StepsApart(call[c].Step, echo[e].Step) <= StepTolerance)
                    {
                        used[e] = true;
                        points += NearPoints;
                        break;
                    }
            }
            return points;
        }

        public static int StepsApart(int a, int b)
        {
            int d = Math.Abs(a - b) % MusicClock.StepsPerBar;
            return Math.Min(d, MusicClock.StepsPerBar - d);
        }

        /// <summary>"U2@0,M1@4" (instrument initial, note, step); empty for no notes.</summary>
        public static string Encode(IReadOnlyList<PhraseNote> phrase)
        {
            var text = new StringBuilder();
            for (int i = 0; i < phrase.Count; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(phrase[i].Instrument.ToString()[0]).Append(phrase[i].Note).Append('@').Append(phrase[i].Step);
            }
            return text.ToString();
        }

        /// <summary>The phrase back from <see cref="Encode"/>; false if anything in it is malformed (a stray packet).</summary>
        public static bool TryDecode(string text, out List<PhraseNote> phrase)
        {
            phrase = new List<PhraseNote>();
            if (string.IsNullOrEmpty(text)) return true;
            foreach (var part in text.Split(','))
            {
                int at = part.IndexOf('@');
                if (part.Length < 4 || at < 2) return false;
                InstrumentType type;
                switch (part[0])
                {
                    case 'U': type = InstrumentType.Udu; break;
                    case 'M': type = InstrumentType.Mbira; break;
                    case 'E': type = InstrumentType.Erhu; break;
                    case 'O': type = InstrumentType.Ocarina; break;
                    case 'H': type = InstrumentType.Handpan; break;
                    case 'G': type = InstrumentType.Guzheng; break;
                    case 'B': type = InstrumentType.Balafon; break;
                    case 'S': type = InstrumentType.SlitDrum; break;
                    default: return false;
                }
                if (!int.TryParse(part.Substring(1, at - 1), out int note) || note < 0 || note >= NoteMapping.Count) return false;
                if (!int.TryParse(part.Substring(at + 1), out int step) || step < 0 || step >= MusicClock.StepsPerBar) return false;
                phrase.Add(new PhraseNote(type, note, step));
            }
            return phrase.Count <= MaxNotes;
        }
    }

    public enum BattleTurn { Waiting, Calling, Watching, Echoing, Done }

    /// <summary>
    /// One headset's view of a match: whose turn it is and the score. The host calls first. Each round: host calls and the
    /// guest echoes, then the guest calls and the host echoes. A turn ends with a phrase (call) or a score (echo), the
    /// moments the two headsets tell each other about.
    /// </summary>
    public sealed class EchoBattleMatch
    {
        public bool IsHost { get; }
        public int Round { get; private set; } = 1;
        /// <summary>Within a round: 0 = the host calls, 1 = the guest calls.</summary>
        public int Half { get; private set; }
        public int MyScore { get; private set; }
        public int TheirScore { get; private set; }
        public BattleTurn Turn { get; private set; }
        public List<PhraseNote> LastCall { get; private set; } = new();

        public EchoBattleMatch(bool isHost)
        {
            IsHost = isHost;
            Turn = ICall ? BattleTurn.Calling : BattleTurn.Waiting;
        }

        /// <summary>Is it this headset's turn to call?</summary>
        public bool ICall => (Half == 0) == IsHost;
        public bool Over => Turn == BattleTurn.Done;

        /// <summary>This player finished a call: it goes to the other headset; we wait for their echo's score.</summary>
        public void Called(IReadOnlyList<PhraseNote> phrase)
        {
            if (Turn != BattleTurn.Calling) return;
            LastCall = new List<PhraseNote>(phrase);
            Turn = BattleTurn.Waiting;
        }

        /// <summary>The other player's call arrived: watch it, then echo it.</summary>
        public void Heard(IReadOnlyList<PhraseNote> phrase)
        {
            if (ICall || Turn == BattleTurn.Done) return;
            LastCall = new List<PhraseNote>(phrase);
            Turn = BattleTurn.Watching;
        }

        public void StartEcho()
        {
            if (Turn == BattleTurn.Watching) Turn = BattleTurn.Echoing;
        }

        /// <summary>This player echoed: score it here and move on. Returns the points (sent to the caller).</summary>
        public int Echoed(IReadOnlyList<PhraseNote> echo, Func<InstrumentType, bool> inThisRoom = null)
        {
            if (Turn != BattleTurn.Echoing) return 0;
            int points = EchoBattleRules.Score(LastCall, echo, inThisRoom);
            MyScore += points;
            Advance();
            return points;
        }

        /// <summary>The other player's echo of our call was scored on their headset.</summary>
        public void TheyScored(int points)
        {
            if (!ICall || Turn != BattleTurn.Waiting) return;
            TheirScore += Math.Max(0, Math.Min(points, EchoBattleRules.MaxScore(LastCall.Count)));
            Advance();
        }

        void Advance()
        {
            if (Half == 0) Half = 1;
            else
            {
                Half = 0;
                Round++;
            }
            if (Round > EchoBattleRules.Rounds)
            {
                Turn = BattleTurn.Done;
                return;
            }
            Turn = ICall ? BattleTurn.Calling : BattleTurn.Waiting;
        }

        /// <summary>1 if this player won, -1 if the other did, 0 for a draw (only once the match is over).</summary>
        public int Result => !Over ? 0 : Math.Sign(MyScore - TheirScore);
    }
}
