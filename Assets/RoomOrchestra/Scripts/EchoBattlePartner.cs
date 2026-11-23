using System;
using System.Collections.Generic;
using System.Net;

namespace RoomOrchestra
{
    /// <summary>
    /// SOLO: the game plays the other side of an Echo Battle on this one headset (owner, 2026-11-22: multiplayer must be
    /// "a smooth experience for both users even though I don't have two headsets"; a reviewer with one headset gets a
    /// whole battle too). It is a second headset in all but body: it joins over loopback with the same messages
    /// (JOIN, READY, CALL, SCORE) a real one sends over Wi-Fi, so the battle the player sees is the real battle. It
    /// "has" any instruments (it is told the player's), echoes the player's calls with a human-sized slip now and then,
    /// and calls phrases that grow from 2 notes to 4 over the three rounds. Nothing leaves the headset.
    /// </summary>
    public sealed class EchoBattlePartner : IDisposable
    {
        /// <summary>Chances, per called note, that its echo is exact, one 16th off, or missed.</summary>
        public const double ExactChance = 0.6, NearChance = 0.25;

        readonly EchoBattleLink m_Link;
        readonly Random m_Random;
        EchoBattleMatch m_Match;
        List<InstrumentType> m_Set = new();
        List<PhraseNote> m_Echo = new();
        float m_ScoreAt = -1f, m_CallAt = -1f;

        public EchoBattleMatch Match => m_Match;

        public EchoBattlePartner(int localPort, int hostPort, int seed)
        {
            m_Link = new EchoBattleLink(EchoBattleLink.Role.Guest, localPort, hostPort, IPAddress.Loopback);
            m_Random = new Random(seed);
        }

        public void Open() => m_Link.Open();

        /// <summary>Answers the player's headset; <paramref name="barSeconds"/> paces it like a player on the same beat.</summary>
        public void Tick(float now, float barSeconds)
        {
            foreach (var message in m_Link.Poll(now)) Handle(message, now, barSeconds);
            if (m_Match == null) return;
            if (m_ScoreAt >= 0f && now >= m_ScoreAt)
            {
                m_ScoreAt = -1f;
                int round = m_Match.Round;
                int points = m_Match.Echoed(m_Echo);
                m_Link.Send("SCORE", $"{round}|{points}");
                // its turn to call next: after the result shows, a count-in bar and the bar it plays in, as a player would
                if (!m_Match.Over && m_Match.ICall) m_CallAt = now + EchoBattleDirector.ResultSeconds + 2f * barSeconds;
            }
            if (m_CallAt >= 0f && now >= m_CallAt)
            {
                m_CallAt = -1f;
                var call = Compose(m_Match.Round);
                m_Match.Called(call);
                m_Link.Send("CALL", $"{m_Match.Round}|{EchoBattleRules.Encode(call)}");
            }
        }

        void Handle(EchoBattleLink.Message message, float now, float barSeconds)
        {
            switch (message.Type)
            {
                case "SET":
                    if (!EchoBattleRules.TryDecodeSet(message.Payload, out m_Set)) m_Set = new List<InstrumentType> { InstrumentType.Udu };
                    m_Link.Send("READY");
                    break;
                case "START":
                    m_Match ??= new EchoBattleMatch(isHost: false);
                    break;
                case "CALL":
                    var parts = message.Payload.Split('|');
                    if (m_Match == null || parts.Length < 2 || !EchoBattleRules.TryDecode(parts[1], out var phrase)) return;
                    m_Match.Heard(phrase);
                    m_Match.StartEcho();
                    m_Echo = Imitate(phrase);
                    // it listens in the next bar, is counted in, echoes in the bar after: then its score goes back
                    m_ScoreAt = now + 3f * barSeconds;
                    break;
                case "SCORE":
                    var s = message.Payload.Split('|');
                    if (m_Match != null && s.Length >= 2 && int.TryParse(s[1], out int points)) m_Match.TheyScored(points);
                    break;
            }
        }

        /// <summary>Its echo of a call: mostly right, sometimes a 16th late or early, sometimes a note missed.</summary>
        public List<PhraseNote> Imitate(IReadOnlyList<PhraseNote> call)
        {
            var echo = new List<PhraseNote>();
            foreach (var note in call)
            {
                double roll = m_Random.NextDouble();
                if (roll < ExactChance) echo.Add(note);
                else if (roll < ExactChance + NearChance) echo.Add(new PhraseNote(note.Instrument, note.Note, note.Step + (m_Random.Next(2) == 0 ? -1 : 1)));
            }
            return echo;
        }

        /// <summary>A call for <paramref name="round"/>: 2, 3, then 4 notes on the battle's instruments, on 8th notes.</summary>
        public List<PhraseNote> Compose(int round)
        {
            int count = Math.Max(2, Math.Min(EchoBattleRules.MaxNotes, round + 1));
            var steps = new List<int>();
            while (steps.Count < count)
            {
                int step = m_Random.Next(MusicClock.StepsPerBar / 2) * 2;
                if (!steps.Contains(step)) steps.Add(step);
            }
            steps.Sort();
            var set = m_Set.Count > 0 ? m_Set : new List<InstrumentType> { InstrumentType.Udu };
            var phrase = new List<PhraseNote>();
            foreach (int step in steps)
                phrase.Add(new PhraseNote(set[m_Random.Next(set.Count)], m_Random.Next(NoteMapping.Count), step));
            return phrase;
        }

        public void Dispose() => m_Link.Dispose();
    }
}
