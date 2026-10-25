using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>Recenter with real transforms and physics (head pose injected; no headset).</summary>
    public class RecenterPlayModeTests
    {
        GameObject m_Stage;

        [TearDown]
        public void TearDown()
        {
            if (m_Stage != null) Object.Destroy(m_Stage);
        }

        [UnityTest]
        public IEnumerator RecenterBringsStageAndBodiesInFrontOfASeatedPlayer()
        {
            m_Stage = new GameObject("Stage");
            var anchor = m_Stage.AddComponent<StageAnchor>();
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.transform.SetParent(m_Stage.transform, false);
            table.transform.localPosition = new Vector3(0f, 0.7f, 1.2f);
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.transform.SetParent(m_Stage.transform, false);
            ball.transform.localPosition = new Vector3(0.5f, 1f, 1.1f);
            var body = ball.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            yield return new WaitForFixedUpdate();

            // Seated at (2, 1.1, 3), turned to face +X, looking a little down at the floor.
            Assert.IsTrue(anchor.Recenter(new Vector3(2f, 1.1f, 3f), Quaternion.Euler(25f, 90f, 0f)));
            Assert.AreEqual(0f, m_Stage.transform.position.y, 1e-5f, "furniture stays on the floor");
            Assert.Less((table.transform.position - new Vector3(3.2f, 0.7f, 3f)).magnitude, 1e-4f, "table 1.2 m in front of the player");
            var expectedBall = new Vector3(3.1f, 1f, 2.5f);
            Assert.Less((body.position - expectedBall).magnitude, 1e-4f, "physics body moved with it");

            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.Less((ball.transform.position - expectedBall).magnitude, 1e-3f, "interpolation does not pull the ball back");
        }
    }
}
