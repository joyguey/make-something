using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>A halo ring turns to face a player who walks round the room (camera stands in for the head).</summary>
    public class OrbitFacingPlayModeTests
    {
        GameObject m_Stage, m_Head;

        [UnitySetUp]
        public IEnumerator Wait()
        {
            while (Time.timeSinceLevelLoad < JellyBody.StartDelaySeconds + 0.1f) yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Stage != null) Object.Destroy(m_Stage);
            if (m_Head != null) Object.Destroy(m_Head);
        }

        [UnityTest]
        public IEnumerator TheRingTurnsToFaceAPlayerWhoWalksRoundAndStillReadsClockwise()
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) cam.tag = "Untagged";
            m_Stage = new GameObject("Stage");
            m_Stage.AddComponent<StageAnchor>();
            m_Head = new GameObject("Head");
            m_Head.AddComponent<Camera>();
            m_Head.tag = "MainCamera";
            m_Head.transform.position = new Vector3(0f, 1.15f, 0f);

            var ringPosition = new Vector3(0f, 0.8f, 2f);
            var orbitGo = new GameObject("Orbit");
            orbitGo.transform.SetParent(m_Stage.transform, false);
            orbitGo.transform.SetPositionAndRotation(ringPosition, OrbitView.Facing(ringPosition, m_Head.transform.position, OrbitView.HaloTilt));
            var view = orbitGo.AddComponent<OrbitView>();
            view.Configure(null, orbitGo.transform, new Transform[0], new Transform[0], new Transform[0], 0.3f);
            var start = orbitGo.transform.rotation;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(start, orbitGo.transform.rotation, "a seated player: the ring stays put");

            // Walk round to the ring's right side.
            m_Head.transform.position = new Vector3(2f, 1.6f, 2f);
            yield return new WaitForSeconds(3f);
            var away = ringPosition - m_Head.transform.position;
            away.y = 0f;
            var up = orbitGo.transform.up;
            Assert.Greater(Vector3.Dot(new Vector3(up.x, 0f, up.z).normalized, away.normalized), 0.99f, "12 o'clock is on the far side from where the player now stands");

            // Clockwise as the player sees it: a step a quarter-bar later sits to the player's right of the marker.
            var head = m_Head.transform.position;
            var toRing = (ringPosition - head).normalized;
            var right = Vector3.Cross(Vector3.up, toRing).normalized;
            var top = view.SlotPosition(0) - ringPosition;
            var quarter = view.SlotPosition(4) - ringPosition;
            Assert.Greater(Vector3.Dot(quarter, right), Vector3.Dot(top, right) + 0.2f, "echoes still travel clockwise from the new viewpoint");
        }
    }
}
