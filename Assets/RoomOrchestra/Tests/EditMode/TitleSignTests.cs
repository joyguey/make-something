using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>The title card hangs on two rings and rocks (gusts, a push), and its Start becomes a handprint for hands.</summary>
    public class TitleSignTests
    {
        TitleScreen Title()
        {
            EditorSceneManager.OpenScene("Assets/RoomOrchestra/Scenes/RoomOrchestra.unity", OpenSceneMode.Single);
            var title = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(title);
            return title;
        }

        [Test]
        public void TheCardHangsFromTheRailOnTwoSteelRings()
        {
            var card = Title().Card;
            var hanging = card.Find("Hanging");
            Assert.IsNotNull(hanging, "the card's parts hang from a pivot");
            Assert.IsNotNull(hanging.GetComponent<TitleSwing>());
            Assert.IsNotNull(hanging.Find("RingLeft"));
            Assert.IsNotNull(hanging.Find("RingRight"));
            Assert.IsNotNull(hanging.Find("TitlePanel"));
            Assert.IsNotNull(hanging.GetComponentInChildren<TitleStartButton>(true), "Start swings with the card");
            Assert.IsNull(card.Find("TitleSign")?.GetComponentInParent<TitleSwing>(), "the wooden frame stays put");
            Assert.IsNull(card.Find("TitleFriends")?.GetComponentInParent<TitleSwing>(), "the friends stay on the rail");
            // the rings go round the rail: their middle on the rail's middle, wide enough to clear it
            var ring = hanging.Find("RingLeft").GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.Greater(ring.size.y, 0.12f);
            Assert.Less(Mathf.Abs(hanging.Find("RingLeft").localPosition.y), 1e-4f);
        }

        [Test]
        public void AGustRocksItAndItSettles()
        {
            var swing = new GameObject("Swing").AddComponent<TitleSwing>();
            try
            {
                TitleSwing.Still = true;
                swing.Nudge(TitleSwing.GustDegrees);
                float most = 0f;
                for (int i = 0; i < 90; i++) { swing.Step(1f / 90f); most = Mathf.Max(most, Mathf.Abs(swing.AngleDegrees)); }
                Assert.That(most, Is.InRange(1.5f, 4f), "a gust rocks it a few degrees");
                for (int i = 0; i < 90 * 8; i++) swing.Step(1f / 90f);
                Assert.Less(Mathf.Abs(swing.AngleDegrees), 0.2f, "and it settles");
            }
            finally
            {
                TitleSwing.Still = false;
                Object.DestroyImmediate(swing.gameObject);
            }
        }

        [Test]
        public void APushSwingsItAwayAndItNeverSwingsTooFar()
        {
            var swing = new GameObject("Swing").AddComponent<TitleSwing>();
            try
            {
                swing.Configure(new Vector2(0.475f, 0.295f), 0.3595f, 0f);
                // a hand on the card's face, low down, pushing away from the player (+Z) at 1 m/s
                var palm = swing.transform.TransformPoint(new Vector3(0f, -0.55f, 0f));
                Assert.IsTrue(swing.Push(palm, new Vector3(0f, 0f, 1f)));
                swing.Step(1f / 90f);
                Assert.Less(swing.AngleDegrees, 0f, "the bottom swings away from the player");
                Assert.IsFalse(swing.Push(swing.transform.TransformPoint(new Vector3(0.8f, -0.4f, 0f)), Vector3.forward), "beside the card: no push");
                Assert.IsFalse(swing.Push(swing.transform.TransformPoint(new Vector3(0f, -0.4f, -0.3f)), Vector3.forward), "30 cm in front: no push");
                for (int i = 0; i < 20; i++) swing.Push(swing.transform.TransformPoint(new Vector3(0f, -0.4f, 0f)), new Vector3(0f, 0f, 20f));
                for (int i = 0; i < 90; i++) { swing.Step(1f / 90f); Assert.LessOrEqual(Mathf.Abs(swing.AngleDegrees), TitleSwing.MaxAngle + 1e-3f); }
            }
            finally { Object.DestroyImmediate(swing.gameObject); }
        }

        [Test]
        public void StartIsAHandprintForHandsAndAButtonForControllers()
        {
            var start = Title().Card.GetComponentInChildren<HandprintStart>(true);
            Assert.IsNotNull(start);
            var button = start.transform.Find("ButtonLook").gameObject;
            var print = start.transform.Find("HandprintLook").gameObject;
            start.Apply(false);
            Assert.IsTrue(button.activeSelf);
            Assert.IsFalse(print.activeSelf);
            start.Apply(true);
            Assert.IsFalse(button.activeSelf);
            Assert.IsTrue(print.activeSelf);
            Assert.IsNotNull(print.GetComponentInChildren<Collider>(), "the print can be tapped (look and pinch)");
            Assert.IsNotNull(print.GetComponentInChildren<TMPro.TMP_Text>(), "and says Start");
            Assert.IsTrue(start.Touches(print.transform.position), "a palm flat on the print presses Start");
            Assert.IsFalse(start.Touches(print.transform.position - print.transform.forward * 0.2f), "a hand 20 cm away does not");
            start.Apply(false);
        }

        [TestCase(1.15f, TitleReach.SeatedScale)]
        [TestCase(1.65f, TitleReach.StandingScale)]
        public void WithHandsTheSignStandsWithinReachOnTheGround(float eyeHeight, float size)
        {
            var eyes = new Vector3(0.3f, eyeHeight, 0.4f);
            var home = new Vector3(0f, 1.4f, 1.8f);
            var (position, rotation, scale) = TitleReach.NearPose(eyes, home, 0f, 1.4f, Vector3.forward);
            Assert.AreEqual(size, scale, 1e-4f);
            var flat = position - eyes;
            flat.y = 0f;
            Assert.AreEqual(TitleReach.Distance, flat.magnitude, 1e-4f, "at arm's reach");
            Assert.AreEqual(1.4f * scale, position.y, 1e-4f, "its feet stay on the ground");
            Assert.Greater(Vector3.Dot(rotation * Vector3.forward, flat.normalized), 0.999f, "facing the player");
            Assert.Less(Mathf.Abs(position.y - eyeHeight), 0.4f, "and near eye height");
        }

        [Test]
        public void TheTitleCardComesToHandsOnly()
        {
            var card = Title().Card;
            Assert.IsNotNull(card.GetComponent<TitleReach>());
            Assert.AreEqual(new Vector3(0f, 1.4f, 1.8f), card.localPosition, "it starts where the controllers' rays reach it");
        }
    }
}
