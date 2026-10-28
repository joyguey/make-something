using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>The leaf pad glides after a walking player with the ball stand; thrown balls stay put.</summary>
    public class RoamingTrayPlayModeTests
    {
        GameObject m_Stage;
        RoamingTray m_Pad;
        Transform m_Frame, m_Stand, m_Pool, m_ThrownBall, m_Button;

        [SetUp]
        public void SetUp()
        {
            m_Stage = new GameObject("Stage");
            m_Stage.AddComponent<StageAnchor>();
            m_Frame = new GameObject("LittleEchoes").transform;
            m_Frame.SetParent(m_Stage.transform, false);
            var controls = new GameObject("Controls").transform;
            controls.SetParent(m_Frame, false);
            controls.localPosition = new Vector3(0f, 0.3f, 0f); // Standing lift
            var padGo = new GameObject("LeafPad");
            padGo.transform.SetParent(controls, false);
            m_Button = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            m_Button.SetParent(padGo.transform, false);
            m_Button.localPosition = new Vector3(0.2f, 0.5f, 1f);
            m_Stand = new GameObject("BallDispenser").transform;
            m_Stand.SetParent(m_Stage.transform, false);
            m_Stand.position = new Vector3(0.62f, 0f, 1.15f);
            m_Pool = new GameObject("BallPool").transform;
            m_Pool.SetParent(m_Stand, false);
            m_ThrownBall = new GameObject("ThrownBall").transform;
            m_ThrownBall.SetParent(m_Pool, false);
            m_ThrownBall.position = new Vector3(0f, 0.8f, 2f); // resting on the chair
            m_Pad = padGo.AddComponent<RoamingTray>();
            m_Pad.Configure(m_Frame, new[] { m_Stand }, m_Pool, null);
        }

        [TearDown]
        public void TearDown() => Object.Destroy(m_Stage);

        void Glide(StagePose head, float seconds)
        {
            for (float t = 0f; t < seconds; t += 1f / 72f) m_Pad.Step(head, 1f / 72f);
        }

        [UnityTest]
        public IEnumerator WalkingAwayBringsTheControlsAndStandAlongButNotTheThrownBalls()
        {
            yield return null;
            var standToButton = m_Button.position - m_Stand.position;
            var head = new StagePose(2f, 1f, 90f); // walked to the right, facing +X
            Glide(head, 3f);
            Assert.AreEqual(TrayMode.Following, m_Pad.Mode);
            Assert.IsTrue(m_Pad.AtTarget, $"settled: pad at {m_Pad.transform.localPosition:F4} yaw {m_Pad.transform.localEulerAngles.y:F2}");
            // The button keeps its seat layout relative to the player: 1 m ahead (+X now), 0.2 m to the right (-Z).
            Assert.AreEqual(new Vector3(3f, 0.8f, 0.8f).ToString("F2"), m_Button.position.ToString("F2"), "in front of the player, lift kept");
            Assert.Less(Vector3.Distance(Quaternion.Euler(0f, 90f, 0f) * standToButton, m_Button.position - m_Stand.position), 1e-4f, "the stand travels with the pad");
            Assert.AreEqual(0f, m_Stand.position.y, 1e-5f, "the stand stays on the floor");
            Assert.Less(Vector3.Distance(new Vector3(0f, 0.8f, 2f), m_ThrownBall.position), 1e-4f, "a thrown ball stays on the chair");
        }

        float SecondsToSettle(StagePose head)
        {
            float t = 0f;
            do
            {
                m_Pad.Step(head, 1f / 72f);
                t += 1f / 72f;
            } while (!m_Pad.AtTarget && t < 10f);
            return t;
        }

        [UnityTest]
        public IEnumerator ItGlidesRatherThanJumpsAndACallIsQuicker()
        {
            yield return null;
            var away = new StagePose(1.5f, 0f, 0f);
            m_Pad.Step(away, 1f / 72f);
            Assert.Less(m_Pad.transform.localPosition.x, 0.1f, "no jump on the first frame");
            float drift = SecondsToSettle(away);

            var seat = new StagePose(0f, 0f, 0f);
            Assert.IsTrue(m_Pad.CallFrom(seat), "called from the seat: home");
            float call = SecondsToSettle(seat);
            Assert.Less(call, drift * 0.6f, $"the call ({call:F2} s) comes much quicker than the drift ({drift:F2} s)");
            Assert.Greater(call, 0.2f, "but still a glide, not a teleport");
            Assert.AreEqual(TrayMode.Home, m_Pad.Mode);
            Assert.AreEqual(Vector3.zero, m_Pad.transform.localPosition);
            Assert.Less(Vector3.Distance(new Vector3(0.62f, 0f, 1.15f), m_Stand.position), 1e-4f, "the stand is home too");
        }

        [UnityTest]
        public IEnumerator RecenterSendsItHomeAtOnce()
        {
            yield return null;
            Glide(new StagePose(2f, 0f, 0f), 3f);
            Assert.AreEqual(TrayMode.Following, m_Pad.Mode);
            StageAnchor.Instance.Recenter(new Vector3(2f, 1.2f, 0f), Quaternion.identity);
            Assert.AreEqual(TrayMode.Home, m_Pad.Mode);
            Assert.AreEqual(Vector3.zero, m_Pad.transform.localPosition, "the seat (and the pad with it) is under the player again");
        }
    }
}
