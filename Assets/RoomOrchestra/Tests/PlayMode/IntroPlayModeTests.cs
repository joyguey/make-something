using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>The opening plays by itself after the startup delay, and everyone ends in place.</summary>
    public class IntroPlayModeTests
    {
        readonly List<GameObject> m_Spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in m_Spawned) if (go != null) Object.Destroy(go);
            m_Spawned.Clear();
        }

        [UnityTest]
        public IEnumerator TheLeafSwoopsInThenTheCharactersHopOntoIt()
        {
            var pad = new GameObject("Pad").transform;
            m_Spawned.Add(pad.gameObject);
            var leaf = new GameObject("LeafSwoop").transform;
            leaf.SetParent(pad, false);
            var tray = new GameObject("Tray").transform;
            tray.SetParent(leaf, false);
            tray.localPosition = new Vector3(-0.3f, 0.5f, 1.28f);
            var cast = new Transform[3];
            var floor = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                cast[i] = new GameObject($"Character{i}").transform;
                cast[i].SetParent(tray, false);
                cast[i].localPosition = new Vector3(-0.17f + 0.17f * i, 0.06f, 0f);
                floor[i] = new Vector3(-0.47f + 0.17f * i, 0.06f, 1.0f);
            }
            var intro = pad.gameObject.AddComponent<IntroSequence>();
            intro.Configure(leaf, cast, floor);
            var leafStart = leaf.localPosition;
            Assert.Greater(leafStart.magnitude, 1f, "configured in the first frame: the leaf away");
            Assert.Less(Vector3.Distance(floor[0], cast[0].position), 1e-5f, "a character on its floor spot");

            // Nothing moves before PICO Spatial's renderer connects.
            while (Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds - 0.2f)
            {
                yield return null;
                Assert.AreEqual(leafStart, leaf.localPosition, "still before the renderer connects");
            }
            float deadline = Time.time + JellyBody.StartDelaySeconds + IntroSequence.StartAfterSeconds + IntroPath.TotalSeconds(3) + 2f;
            bool sawLeafMove = false, sawHop = false;
            while (!intro.Done && Time.time < deadline)
            {
                yield return null;
                if (leaf.localPosition != leafStart) sawLeafMove = true;
                if (cast[0].position.y > 0.1f && cast[0].position.y < 0.5f) sawHop = true;
            }
            Assert.IsTrue(intro.Done, "the opening finished by itself");
            Assert.IsTrue(sawLeafMove, "the leaf glided in");
            Assert.IsTrue(sawHop, "Flutter was seen in the air between the floor and the leaf");
            Assert.AreEqual(Vector3.zero, leaf.localPosition);
            for (int i = 0; i < 3; i++)
                Assert.Less(Vector3.Distance(cast[i].localPosition, new Vector3(-0.17f + 0.17f * i, 0.06f, 0f)), 1e-5f, $"character {i} in its place");
        }
    }
}
