using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>The title card: only Start acts; Start tucks the card into the cloud and begins the opening.</summary>
    public class TitleScreenPlayModeTests
    {
        readonly List<GameObject> m_Spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in m_Spawned) if (go != null) Object.Destroy(go);
            m_Spawned.Clear();
        }

        Transform Make(string name, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            if (parent != null) t.SetParent(parent, false); else m_Spawned.Add(t.gameObject);
            return t;
        }

        [UnityTest]
        public IEnumerator StartTucksTheCardIntoTheCloudThenTheOpeningPlays()
        {
            var root = Make("Root");
            var pad = Make("Pad", root);
            var leaf = Make("LeafSwoop", pad);
            var tray = Make("Tray", leaf);
            tray.localPosition = new Vector3(-0.3f, 0.5f, 1.28f);
            var character = Make("Character", tray);
            var card = Make("TitleCard", root);
            card.position = new Vector3(0f, 1.2f, 1.5f);
            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.transform.SetParent(card, false);
            button.AddComponent<TitleStartButton>();
            var other = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_Spawned.Add(other);
            var park = Make("Park", root);
            park.position = new Vector3(0.62f, 0.915f, 1.15f);

            var intro = root.gameObject.AddComponent<IntroSequence>();
            var title = root.gameObject.AddComponent<TitleScreen>();
            title.Configure(card, park, intro);
            intro.Configure(leaf, new[] { character }, new[] { new Vector3(-0.3f, 0.06f, 1f) }, null, title);
            yield return null;

            Assert.IsTrue(title.Showing);
            Assert.IsFalse(InstrumentInputRouter.Activate(other), "nothing but Start acts on the title");
            while (Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds + IntroSequence.StartAfterSeconds + 0.5f) yield return null;
            Assert.IsFalse(intro.Playing, "the opening waits for Start");

            Assert.IsTrue(InstrumentInputRouter.Activate(button), "Start");
            Assert.IsFalse(title.Showing);
            yield return new WaitForSeconds(TitleScreen.LeaveSeconds + 0.2f);
            Assert.IsTrue(title.Parked);
            Assert.Less(Vector3.Distance(card.position, park.position), 1e-4f, "the card is in the cloud");
            Assert.AreEqual(TitleScreen.ParkedScale, card.localScale.x, 1e-4f, "small enough to fit inside it");
            Assert.IsFalse(button.GetComponent<Collider>().enabled, "its button no longer catches taps");
            Assert.IsTrue(intro.Playing || intro.Done, "the opening began");
        }
    }
}
