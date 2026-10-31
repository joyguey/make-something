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
            StringAssert.Contains("Echo Battle", battle.StatusText);
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
            yield return Pump(5f, () => got.Any(m => m.Type == "START"));
            Assert.IsTrue(got.Any(m => m.Type == "START"), "the other headset joined and was told to start");
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

            Assert.IsTrue(InstrumentInputRouter.Activate(battle.Card.GetComponentsInChildren<EchoBattleButton>().First(b => b.ButtonKind == EchoBattleButton.Kind.Leave).GetComponentInChildren<Collider>().gameObject));
            Assert.IsFalse(EchoConductor.Instance.SongMuted, "leaving the battle: the player's own song plays on");
            Assert.IsTrue(battle.Shown, "leaving a battle goes back to the card's menu");
            Assert.AreSame(ownSong, EchoConductor.Instance.Echoes.Single(), "exactly as it was");
        }
    }
}
