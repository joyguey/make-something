using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using NUnit.Framework;

namespace RoomOrchestra.Tests
{
    /// <summary>The Echo Battle (easter egg): scoring, the turn order, the phrase format, and two links talking over loopback.</summary>
    public class EchoBattleTests
    {
        static PhraseNote N(InstrumentType t, int note, int step) => new PhraseNote(t, note, step);

        [Test]
        public void ExactEchoesScoreTwoNearOnesOneWrongOnesNothing()
        {
            var call = new List<PhraseNote> { N(InstrumentType.Mbira, 2, 0), N(InstrumentType.Erhu, 4, 4), N(InstrumentType.Udu, 1, 8) };
            Assert.AreEqual(6, EchoBattleRules.Score(call, call), "a perfect echo");
            Assert.AreEqual(EchoBattleRules.MaxScore(3), EchoBattleRules.Score(call, call));
            var near = new List<PhraseNote> { N(InstrumentType.Mbira, 2, 1), N(InstrumentType.Erhu, 4, 3), N(InstrumentType.Udu, 1, 8) };
            Assert.AreEqual(1 + 1 + 2, EchoBattleRules.Score(call, near), "one 16th off still counts, half");
            var wrong = new List<PhraseNote> { N(InstrumentType.Mbira, 3, 0), N(InstrumentType.Udu, 4, 4), N(InstrumentType.Udu, 1, 11) };
            Assert.AreEqual(0, EchoBattleRules.Score(call, wrong), "wrong note, wrong instrument, too late");
            Assert.AreEqual(2, EchoBattleRules.Score(new List<PhraseNote> { N(InstrumentType.Mbira, 2, 15) }, new List<PhraseNote> { N(InstrumentType.Mbira, 2, 15), N(InstrumentType.Mbira, 2, 0) }), "each call note counts once");
            Assert.AreEqual(1, EchoBattleRules.StepsApart(15, 0), "around the bar");
        }

        [Test]
        public void PhrasesTravelAsShortTextAndBadPacketsAreRejected()
        {
            var phrase = new List<PhraseNote> { N(InstrumentType.Udu, 2, 0), N(InstrumentType.Mbira, 1, 4), N(InstrumentType.Erhu, 4, 15) };
            string text = EchoBattleRules.Encode(phrase);
            Assert.AreEqual("U2@0,M1@4,E4@15", text);
            Assert.IsTrue(EchoBattleRules.TryDecode(text, out var back));
            CollectionAssert.AreEqual(phrase, back);
            Assert.IsTrue(EchoBattleRules.TryDecode("", out var none) && none.Count == 0, "a silent call is allowed");
            foreach (var bad in new[] { "X2@0", "M9@0", "M1@16", "M1", "M1@0,M1@1,M1@2,M1@3,M1@4", "M@1" })
                Assert.IsFalse(EchoBattleRules.TryDecode(bad, out _), bad);
        }

        [Test]
        public void TheHostCallsFirstAndTurnsAlternateForThreeRounds()
        {
            var host = new EchoBattleMatch(isHost: true);
            var guest = new EchoBattleMatch(isHost: false);
            var phrase = new List<PhraseNote> { N(InstrumentType.Mbira, 2, 0), N(InstrumentType.Mbira, 1, 4) };
            int turns = 0;
            while (!host.Over && turns < 20)
            {
                var caller = host.Turn == BattleTurn.Calling ? host : guest;
                var echoer = caller == host ? guest : host;
                Assert.AreEqual(BattleTurn.Calling, caller.Turn);
                Assert.AreEqual(BattleTurn.Waiting, echoer.Turn);
                caller.Called(phrase);
                echoer.Heard(phrase);
                Assert.AreEqual(BattleTurn.Watching, echoer.Turn);
                echoer.StartEcho();
                int points = echoer.Echoed(phrase.Take(1).ToList()); // echoes the first note only
                caller.TheyScored(points);
                turns++;
            }
            Assert.AreEqual(2 * EchoBattleRules.Rounds, turns, "each player calls once per round");
            Assert.IsTrue(host.Over && guest.Over);
            Assert.AreEqual(host.MyScore, guest.TheirScore, "both headsets agree on the score");
            Assert.AreEqual(guest.MyScore, host.TheirScore);
            Assert.AreEqual(EchoBattleRules.Rounds * 2, host.MyScore);
            Assert.AreEqual(0, host.Result, "equal echoes: a draw");
        }

        [Test]
        public void ScoresFromTheOtherHeadsetAreCappedAtThePhrasesMaximum()
        {
            var host = new EchoBattleMatch(isHost: true);
            host.Called(new List<PhraseNote> { N(InstrumentType.Mbira, 2, 0) });
            host.TheyScored(99);
            Assert.AreEqual(2, host.TheirScore, "a bad or tampered score can't exceed the phrase");
        }

        [Test]
        public void TwoLinksFindEachOtherAndDeliverEachMessageOnce()
        {
            using var host = new EchoBattleLink(EchoBattleLink.Role.Host, 47791, 47792, IPAddress.Loopback);
            using var guest = new EchoBattleLink(EchoBattleLink.Role.Guest, 47792, 47791, IPAddress.Loopback);
            host.Open();
            guest.Open();
            var hostGot = new List<EchoBattleLink.Message>();
            var guestGot = new List<EchoBattleLink.Message>();
            float t = 0f;
            void Pump(int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    t += 0.05f;
                    hostGot.AddRange(host.Poll(t));
                    guestGot.AddRange(guest.Poll(t));
                    Thread.Sleep(10);
                }
            }
            Pump(60);
            Assert.IsTrue(host.Connected && guest.Connected, $"paired (host: {host.Status}; guest: {guest.Status})");
            Assert.AreEqual("JOIN", hostGot.Single().Type, "the host hears the guest join once");

            host.Send("CALL", "1|M2@0,M1@4");
            Pump(40);
            Assert.AreEqual(1, guestGot.Count(m => m.Type == "CALL"), "delivered once, despite resends until acknowledged");
            Assert.AreEqual("1|M2@0,M1@4", guestGot.Single(m => m.Type == "CALL").Payload);

            guest.Send("SCORE", "1|3");
            Pump(40);
            Assert.AreEqual("1|3", hostGot.Single(m => m.Type == "SCORE").Payload);
        }
    }
}
