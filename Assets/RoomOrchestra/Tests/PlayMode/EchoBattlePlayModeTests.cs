using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// The Echo Battle on the REAL scene: the secret unlocks it; then this headset hosts a full turn against a second
    /// headset played by a raw link over loopback (as the other headset would over Wi-Fi). Editor simulation: two real
    /// headsets on one Wi-Fi are for PICO's developers to try.
    /// </summary>
    public class EchoBattlePlayModeTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";
        EchoBattleLink m_Other;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            Object.FindFirstObjectByType<IntroSequence>().Finish(); // free play
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            m_Other?.Dispose();
            m_Other = null;
            var empty = SceneManager.CreateScene($"BattleEmpty{Time.frameCount}");
            SceneManager.SetActiveScene(empty);
            var real = SceneManager.GetSceneByPath(ScenePath);
            if (real.isLoaded) yield return SceneManager.UnloadSceneAsync(real);
            PlayerPrefs.DeleteKey(TutorialDirector.DonePrefKey);
        }

        static IEnumerator Secret()
        {
            var selector = OrbSelector.Instance;
            foreach (var orb in new[] { OrbType.Flutter, OrbType.Bop, OrbType.Boom, OrbType.Flutter, OrbType.Bop, OrbType.Boom })
            {
                if (selector.SelectedOrb == orb) { selector.Select(OrbType.Boom == orb ? OrbType.Flutter : OrbType.Boom); yield return null; }
                selector.Select(orb);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheSecretBringsOutTheCardInViewAndLeavePutsItAway()
        {
            var battle = EchoBattleDirector.Instance;
            Assert.IsFalse(battle.Shown, "hidden until the secret");
            var park = GameObject.Find("RoomOrchestra").transform.Find("BallDispenser/ParkPoint");
            while (Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds + 0.2f) yield return null; // it follows the stand home
            Assert.Less(Vector3.Distance(battle.Card.position, park.position), 1e-3f, "waiting in the cloud");
            yield return Secret();
            Assert.IsTrue(battle.Shown, "Flutter, Bop, Boom, twice: the Echo Battle");
            Assert.Greater(Vector3.Distance(battle.Card.position, park.position), 0.5f, "out beside the leaf");
            var p = battle.Card.position;
            Assert.Less(Vector3.Angle(Vector3.forward, new Vector3(p.x, 0f, p.z)), 30f, "in view from the seat, not off to the side");
            // It never covers an instrument's tiles from the seat (the battle is played on them).
            Physics.SyncTransforms();
            var eye = new Vector3(0f, 1.15f, 0f);
            var cardBounds = battle.Card.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            foreach (var tile in Object.FindObjectsByType<PitchTile>(FindObjectsSortMode.None))
            {
                var ray = new Ray(eye, tile.transform.position - eye);
                Assert.IsFalse(cardBounds.IntersectRay(ray, out float d) && d < Vector3.Distance(eye, tile.transform.position), $"the card covers {tile.Target.Instrument} tile {tile.Petal}");
            }
            StringAssert.StartsWith("Unlocked", battle.StatusText);
            Assert.AreEqual("BattleTitle", battle.ShownTitle.name, "the street tag, in English");
            Localization.Set(UiLanguage.Chinese, remember: false);
            Assert.AreEqual("BattleTitleZh", battle.ShownTitle.name, "回声对决 in Chinese");
            Assert.Greater(Vector3.Distance(battle.Card.Find("BattleTitle").localPosition, battle.ShownTitle.localPosition), 0.05f, "the English tag waits behind the wall");
            Localization.Set(UiLanguage.English, remember: false);
            Assert.IsTrue(battle.Card.GetComponentsInChildren<EchoBattleButton>().All(b => b.GetComponentInChildren<Collider>().enabled), "Host, Join and Leave can be tapped");
            yield return Secret();
            Assert.IsTrue(battle.Shown, "repeating the secret never puts it away");
            Assert.IsTrue(InstrumentInputRouter.Activate(battle.Card.GetComponentsInChildren<EchoBattleButton>().First(b => b.ButtonKind == EchoBattleButton.Kind.Leave).GetComponentInChildren<Collider>().gameObject));
            Assert.IsFalse(battle.Shown, "Leave, outside a battle, puts the card away");
        }

        [UnityTest]
        public IEnumerator HostingPairsCallsAPhraseAndCountsTheOtherHeadsetsEcho()
        {
            var battle = EchoBattleDirector.Instance;
            battle.UseTestNetwork(47801, 47802);
            m_Other = new EchoBattleLink(EchoBattleLink.Role.Guest, 47802, 47801, IPAddress.Loopback);
            m_Other.Open();
            var got = new List<EchoBattleLink.Message>();
            IEnumerator Pump(float seconds, System.Func<bool> until = null)
            {
                float end = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < end && (until == null || !until()))
                {
                    got.AddRange(m_Other.Poll(Time.realtimeSinceStartup));
                    yield return null;
                }
            }

            // The player's own song before the battle: one note, which must survive it.
            OrbSelector.Instance.PlaceOnTile(Object.FindObjectsByType<PitchTile>(FindObjectsSortMode.None).First(t => t.Target.Instrument == InstrumentType.Erhu));
            yield return null;
            var ownSong = EchoConductor.Instance.Echoes.Single();
            yield return Secret();
            Assert.IsTrue(InstrumentInputRouter.Activate(battle.Card.GetComponentsInChildren<EchoBattleButton>().First(b => b.ButtonKind == EchoBattleButton.Kind.Host).GetComponentInChildren<Collider>().gameObject));
            Assert.AreEqual(EchoBattleDirector.Phase.Pairing, battle.State);
            Assert.IsTrue(EchoConductor.Instance.SongMuted, "the player's song is set aside (kept, silent) during the battle");
            // hosting locks this room's instruments in, and the other headset is told which they are
            StringAssert.Contains("udu, mbira, erhu", battle.StatusText);
            var chair = InstrumentTarget.Find(InstrumentType.Mbira);
            Assert.IsFalse(InstrumentPacks.Instance.ChangeInstrument(chair), "the host's instruments are locked in");
            Assert.AreEqual(InstrumentType.Mbira, chair.Instrument);
            yield return Pump(5f, () => got.Any(m => m.Type == "SET"));
            Assert.AreEqual("UME", got.First(m => m.Type == "SET").Payload, "the other headset joined and was told the host's instruments");
            Assert.AreEqual(EchoBattleDirector.Phase.Setup, battle.State, "waiting while they put them on their furniture");
            Assert.IsNull(battle.Match, "no play before their room matches");
            Assert.IsFalse(got.Any(m => m.Type == "START"));
            m_Other.Send("READY");
            yield return Pump(5f, () => got.Any(m => m.Type == "START"));
            Assert.IsTrue(got.Any(m => m.Type == "START"), "their room matches: told to start");
            Assert.IsNotNull(battle.Match);
            Assert.IsTrue(battle.Match.ICall, "the host calls first");

            // Our call: wait for GO, then play two notes on the chair.
            yield return Pump(8f, () => battle.State == EchoBattleDirector.Phase.Recording);
            Assert.AreEqual(EchoBattleDirector.Phase.Recording, battle.State);
            var tiles = Object.FindObjectsByType<PitchTile>(FindObjectsSortMode.None).Where(t => t.Target.Instrument == InstrumentType.Mbira).OrderBy(t => t.Petal).ToArray();
            OrbSelector.Instance.PlaceOnTile(tiles[2]);
            yield return Pump(0.4f);
            OrbSelector.Instance.PlaceOnTile(tiles[4]);
            yield return null;
            Assert.AreEqual(1, EchoConductor.Instance.Echoes.Count, "battle notes are one-shots: only the player's own song is kept");
            yield return Pump(5f, () => got.Any(m => m.Type == "CALL"));
            var call = got.First(m => m.Type == "CALL").Payload.Split('|');
            Assert.AreEqual("1", call[0], "round 1");
            Assert.IsTrue(EchoBattleRules.TryDecode(call[1], out var phrase));
            CollectionAssert.AreEqual(new[] { 2, 4 }, phrase.Select(n => n.Note), "our two notes, in order");
            Assert.IsTrue(phrase.All(n => n.Instrument == InstrumentType.Mbira));
            Assert.AreEqual(EchoBattleDirector.Phase.WaitingForThem, battle.State);

            // The other headset echoes and reports 3 points.
            m_Other.Send("SCORE", "1|3");
            yield return Pump(3f, () => battle.Match.TheirScore > 0);
            Assert.AreEqual(3, battle.Match.TheirScore, "their echo of our call");

            // Their call: we hear it, then echo it.
            yield return Pump(4f, () => battle.State == EchoBattleDirector.Phase.WaitingForThem);
            m_Other.Send("CALL", "1|U2@0");
            yield return Pump(12f, () => battle.State == EchoBattleDirector.Phase.Recording);
            Assert.AreEqual(EchoBattleDirector.Phase.Recording, battle.State, "listened, counted in, now echoing");
            yield return Pump(8f, () => got.Any(m => m.Type == "SCORE"));
            Assert.AreEqual("1|0", got.First(m => m.Type == "SCORE").Payload, "no echo played: no points, and the score is sent");
            Assert.AreEqual(2, battle.Match.Round, "round 2");

            var leave = battle.Card.GetComponentsInChildren<EchoBattleButton>().First(b => b.ButtonKind == EchoBattleButton.Kind.Leave).GetComponentInChildren<Collider>().gameObject;
            Assert.IsTrue(InstrumentInputRouter.Activate(leave));
            Assert.IsTrue(EchoConductor.Instance.SongMuted, "mid-battle, Leave asks \"Sure?\" first (George, 2026-11-03)");
            Assert.IsTrue(InstrumentInputRouter.Activate(leave));
            Assert.IsFalse(EchoConductor.Instance.SongMuted, "leaving the battle: the player's own song plays on");
            Assert.IsFalse(InstrumentPacks.Locked, "and the instruments can be changed again");
            Assert.IsTrue(battle.Shown, "leaving a battle goes back to the card's menu");
            Assert.AreSame(ownSong, EchoConductor.Instance.Echoes.Single(), "exactly as it was");
        }

        // Owner, 2026-11-22: an easier way in than the secret, and a smooth battle with one headset.
        [UnityTest]
        public IEnumerator PlayTogetherOnTheMenuThenSoloPlaysAWholeBattleAgainstTheGame()
        {
            var battle = EchoBattleDirector.Instance;
            OrbSelector.Instance.PlaceOnTile(Object.FindObjectsByType<PitchTile>(FindObjectsSortMode.None).First(t => t.Target.Instrument == InstrumentType.Erhu));
            yield return null;
            var ownSong = EchoConductor.Instance.Echoes.Single();

            MenuTray.Instance.SetOpen(true);
            yield return new WaitForSeconds(MenuTray.OpenSeconds + 0.1f);
            var together = Object.FindFirstObjectByType<TogetherButton>();
            Assert.IsTrue(InstrumentInputRouter.Activate(together.GetComponentInChildren<Collider>().gameObject), "Play together takes a tap");
            Assert.IsTrue(battle.Shown, "the card comes out, no secret needed");
            yield return null; // its buttons come out with it
            Assert.IsFalse(MenuTray.Instance.Open, "the menu folds away so the card is in view");
            StringAssert.StartsWith("Two headsets", battle.StatusText);
            StringAssert.Contains("SOLO", battle.StatusText, "and says what to do with one headset");

            var solo = battle.Card.GetComponentsInChildren<EchoBattleButton>().First(b => b.ButtonKind == EchoBattleButton.Kind.Solo);
            Assert.IsTrue(InstrumentInputRouter.Activate(solo.GetComponentInChildren<Collider>().gameObject));
            Assert.IsTrue(battle.Solo);
            Assert.IsTrue(EchoConductor.Instance.SongMuted, "the player's song is set aside, as in any battle");
            float start = Time.realtimeSinceStartup;
            while (battle.Match == null && Time.realtimeSinceStartup < start + 5f) yield return null;
            Assert.IsNotNull(battle.Match, "the game joins at once: no waiting for a second headset");
            Assert.IsTrue(battle.Match.ICall, "the player calls first");

            // three rounds: the player calls two notes each time; the game echoes, then calls and the player listens
            var tiles = Object.FindObjectsByType<PitchTile>(FindObjectsSortMode.None).Where(t => t.Target.Instrument == InstrumentType.Mbira).OrderBy(t => t.Petal).ToArray();
            int calls = 0, heard = 0;
            var lastState = battle.State;
            while (battle.State != EchoBattleDirector.Phase.Over && Time.realtimeSinceStartup < start + 150f)
            {
                if (battle.State != lastState)
                {
                    if (battle.State == EchoBattleDirector.Phase.Recording && battle.Match.ICall)
                    {
                        calls++;
                        OrbSelector.Instance.PlaceOnTile(tiles[1]);
                        yield return new WaitForSeconds(0.35f);
                        OrbSelector.Instance.PlaceOnTile(tiles[3]);
                    }
                    if (battle.State == EchoBattleDirector.Phase.Listening)
                    {
                        heard++;
                        Assert.AreEqual(Mathf.Min(battle.Match.Round + 1, 4), battle.Match.LastCall.Count, "the game's calls grow, 2 to 4 notes");
                        Assert.Greater(InstrumentTarget.Active.Sum(t => t.ScheduledCount), 0, "its call is played on this room's furniture");
                    }
                    lastState = battle.State;
                }
                yield return null;
            }
            Assert.AreEqual(EchoBattleDirector.Phase.Over, battle.State, $"a whole battle in time (stuck at {battle.State}, round {battle.Match.Round})");
            Assert.AreEqual(3, calls, "the player called in each of 3 rounds");
            Assert.AreEqual(3, heard, "and heard the game's call in each");
            Assert.Greater(battle.Match.TheirScore, 0, "the game echoed the player's calls");
            StringAssert.IsMatch("WIN|DRAW", battle.StatusText.ToUpperInvariant(), "the result is said");
            Assert.IsNull(battle.Partner, "the game has left the battle");
            Assert.IsFalse(EchoConductor.Instance.SongMuted, "the player's own song plays on");
            Assert.IsFalse(InstrumentPacks.Locked);
            Assert.AreSame(ownSong, EchoConductor.Instance.Echoes.Single(), "exactly as it was");
        }

        // Owner, headset 2026-11-22: an x to close Flutter's bubble; the two-player card must never open behind it.
        [UnityTest]
        public IEnumerator FlutterBubbleHasAnXAndGivesWayToTheCard()
        {
            var lesson = TutorialDirector.Instance;
            if (lesson.Active) lesson.Skip();
            yield return new WaitForSeconds(1.5f);
            Assert.IsNull(Object.FindFirstObjectByType<BubbleCloseButton>(), "no x while nothing is said");
            lesson.Announce(UiTerm.PlaceDone, 30f);
            yield return null;
            Assert.IsTrue(lesson.BubbleOut);
            var x = Object.FindFirstObjectByType<BubbleCloseButton>();
            Assert.IsNotNull(x, "the x shows on a line after the lesson");
            Assert.IsTrue(x.GetComponent<Collider>().enabled, "and takes taps");
            Assert.IsTrue(InstrumentInputRouter.Activate(x.gameObject));
            Assert.IsFalse(lesson.BubbleOut, "the x closes it at once");
            yield return new WaitForSeconds(1.5f);
            lesson.Announce(UiTerm.PlaceDone, 30f);
            yield return null;
            Assert.IsTrue(lesson.BubbleOut);
            EchoBattleDirector.Instance.ShowFromMenu();
            Assert.IsFalse(lesson.BubbleOut, "Play together sends the bubble away: the card is never behind it");
        }

        [UnityTest]
        public IEnumerator NobodyToJoinTheCardSaysWhatToCheckAndOffersSolo()
        {
            var battle = EchoBattleDirector.Instance;
            battle.UseTestNetwork(47821, 47822); // nobody on the other end
            battle.ShowFromMenu();
            battle.Press(EchoBattleButton.Kind.Join);
            Assert.AreEqual(EchoBattleDirector.Phase.Pairing, battle.State);
            yield return new WaitForSeconds(EchoBattleDirector.StillLookingSeconds + 0.5f);
            StringAssert.StartsWith("Still looking", battle.StatusText);
            StringAssert.Contains("SOLO", battle.StatusText);
            battle.Press(EchoBattleButton.Kind.Leave);
            Assert.AreEqual(EchoBattleDirector.Phase.Menu, battle.State, "LEAVE goes back to the card's buttons, SOLO among them");
        }

        [UnityTest]
        public IEnumerator JoiningHearsTheOtherHeadsetsCallOnThisRoomsFurniture()
        {
            var battle = EchoBattleDirector.Instance;
            battle.UseTestNetwork(47811, 47812);
            m_Other = new EchoBattleLink(EchoBattleLink.Role.Host, 47812, 47811, IPAddress.Loopback);
            m_Other.Open();
            var got = new List<EchoBattleLink.Message>();
            IEnumerator Pump(float seconds, System.Func<bool> until = null)
            {
                float end = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < end && (until == null || !until()))
                {
                    got.AddRange(m_Other.Poll(Time.realtimeSinceStartup));
                    yield return null;
                }
            }
            yield return Secret();
            Assert.IsTrue(InstrumentInputRouter.Activate(battle.Card.GetComponentsInChildren<EchoBattleButton>().First(b => b.ButtonKind == EchoBattleButton.Kind.Join).GetComponentInChildren<Collider>().gameObject));
            yield return Pump(5f, () => got.Any(m => m.Type == "JOIN"));
            Assert.IsTrue(got.Any(m => m.Type == "JOIN"), "we asked to join");
            // the host plays udu, handpan and erhu: this room (udu, mbira, erhu) gets the handpan by itself (owner,
            // 2026-11-22: "make joining automatic"), on the chair, the one piece not already playing one of the host's
            var chairPiece = InstrumentTarget.Find(InstrumentType.Mbira);
            m_Other.Send("SET", "UHE");
            yield return Pump(4f, () => got.Any(m => m.Type == "READY"));
            Assert.AreEqual(InstrumentType.Handpan, chairPiece.Instrument, "the chair took the host's handpan, no taps needed");
            Assert.IsNotNull(InstrumentTarget.Find(InstrumentType.Udu));
            Assert.IsNotNull(InstrumentTarget.Find(InstrumentType.Erhu), "the pieces already matching kept theirs");
            Assert.IsTrue(got.Any(m => m.Type == "READY"), "the room matches the host's: ready");
            Assert.IsFalse(InstrumentPacks.Instance.ChangeInstrument(chairPiece), "and its instruments are locked in too");
            Assert.AreEqual("Udu,Mbira,Erhu", PlayerPrefs.GetString(InstrumentPacks.PlacesPrefKey, "Udu,Mbira,Erhu"), "the player's own arrangement stays the remembered one");
            m_Other.Send("START");
            yield return Pump(4f, () => battle.State == EchoBattleDirector.Phase.WaitingForThem);
            Assert.AreEqual(EchoBattleDirector.Phase.WaitingForThem, battle.State, "the host calls first");

            // JOIN (or HOST) pressed again mid-battle does nothing: it used to reopen the link, which left the battle and
            // told the other headset goodbye (V4 feedback: "leaving the game by accidentally pressing join twice")
            Assert.IsTrue(battle.Card.GetComponentsInChildren<EchoBattleButton>(true).Where(b => b.ButtonKind != EchoBattleButton.Kind.Leave).All(b => !b.gameObject.activeInHierarchy),
                "HOST and JOIN are put away once a battle is on: LEAVE is the only button");
            battle.Press(EchoBattleButton.Kind.Join);
            battle.Press(EchoBattleButton.Kind.Host);
            yield return Pump(0.5f);
            Assert.AreEqual(EchoBattleDirector.Phase.WaitingForThem, battle.State, "still in the battle");
            Assert.IsNotNull(battle.Match);
            Assert.IsFalse(got.Any(m => m.Type == "BYE"), "and the other headset was not told goodbye");

            // the host's call: two notes on the handpan (now on this room's chair)
            var mbira = InstrumentTarget.Find(InstrumentType.Handpan);
            Assert.IsNotNull(mbira);
            m_Other.Send("CALL", "1|H2@0,H4@4");
            yield return Pump(4f, () => battle.State == EchoBattleDirector.Phase.Listening);
            Assert.AreEqual(EchoBattleDirector.Phase.Listening, battle.State);
            Debug.Log($"[test] scheduled on the mbira right after the call: {mbira.ScheduledCount}");
            Assert.AreEqual(2, mbira.ScheduledCount, "both notes of the call are queued to sound on this room's handpan");
            // and they do sound: a voice is playing during the listening bar
            bool heard = false;
            yield return Pump(6f, () => { heard |= mbira.GetComponentsInChildren<AudioSource>().Any(s => s.isPlaying); return heard || battle.State == EchoBattleDirector.Phase.CountIn; });
            Assert.IsTrue(heard, "the call is heard");

            // our echo is scored, and the host calls again, this time on an instrument this room doesn't have (the two
            // players chose different ones): it is still heard, in the ocarina's sound, on this room's furniture
            yield return Pump(14f, () => got.Any(m => m.Type == "SCORE"));
            Assert.IsTrue(got.Any(m => m.Type == "SCORE"), "our echo was scored and sent");
            yield return Pump(14f, () => got.Any(m => m.Type == "CALL")); // we call second in round 1 (nothing played: an empty call)
            Assert.IsTrue(got.Any(m => m.Type == "CALL"), "our call went out");
            m_Other.Send("SCORE", "1|0"); // their echo of it
            yield return Pump(14f, () => battle.State == EchoBattleDirector.Phase.WaitingForThem && battle.Match.Round == 2);
            Assert.AreEqual(2, battle.Match.Round, "round 2: the host calls again");
            Assert.IsNull(InstrumentTarget.Find(InstrumentType.Ocarina), "no ocarina in this room");
            m_Other.Send("CALL", "2|O1@0,O3@8");
            yield return Pump(4f, () => battle.State == EchoBattleDirector.Phase.Listening);
            Assert.AreEqual(EchoBattleDirector.Phase.Listening, battle.State);
            Assert.AreEqual(2, InstrumentTarget.Active.Sum(target => target.ScheduledCount), "both ocarina notes are queued on this room's furniture");
            StringAssert.Contains("2", battle.StatusText, "the card says how many notes to listen for");

            // leaving puts the player's own instruments back on their furniture
            battle.Press(EchoBattleButton.Kind.Leave);
            Assert.AreEqual(EchoBattleDirector.Phase.Menu, battle.State);
            Assert.AreEqual(InstrumentType.Mbira, chairPiece.Instrument, "the chair plays the mbira again");
            Assert.IsFalse(InstrumentPacks.Locked);
            Assert.IsNull(InstrumentPacks.Wanted);
        }
    }
}
