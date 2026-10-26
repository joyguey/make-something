using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>A character rolls off a ledge: scared on the way down, then a poof on the floor (real physics).</summary>
    public class FallAndPoofPlayModeTests
    {
        readonly List<GameObject> m_Spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in m_Spawned) if (go != null) Object.Destroy(go);
            m_Spawned.Clear();
        }

        GameObject Spawn(PrimitiveType type, string name, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            m_Spawned.Add(go);
            return go;
        }

        (Ball ball, CharacterFace face, PoofCloud cloud, Transform park) Build()
        {
            Spawn(PrimitiveType.Cube, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(10f, 0.1f, 10f)).AddComponent<GroundSurface>();
            var ballGo = Spawn(PrimitiveType.Sphere, "Ball", new Vector3(0f, 1f, 0f), Vector3.one * 0.1f);
            ballGo.AddComponent<Rigidbody>();
            var ball = ballGo.AddComponent<Ball>();
            Transform Part(string n, Vector3 at)
            {
                var t = new GameObject(n).transform;
                t.SetParent(ballGo.transform, false);
                t.localPosition = at;
                return t;
            }
            var face = ballGo.AddComponent<CharacterFace>();
            face.Configure(Part("EyeL", new Vector3(-0.15f, 0.07f, -0.36f)), Part("EyeR", new Vector3(0.15f, 0.07f, -0.36f)),
                Part("Smile", new Vector3(0f, -0.18f, -0.41f)), Part("Mouth_O", new Vector3(0f, -0.2f, -0.16f)));

            var park = new GameObject("PoofPark").transform;
            park.position = new Vector3(5f, 0.2f, 5f);
            m_Spawned.Add(park.gameObject);
            var cloudGo = new GameObject("PoofCloud");
            m_Spawned.Add(cloudGo);
            cloudGo.transform.position = park.position;
            var puffs = new Transform[7];
            for (int i = 0; i < puffs.Length; i++)
            {
                puffs[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
                Object.Destroy(puffs[i].GetComponent<Collider>());
                puffs[i].SetParent(cloudGo.transform, false);
                puffs[i].localScale = Vector3.one * 0.035f;
            }
            var cloud = cloudGo.AddComponent<PoofCloud>();
            cloud.Configure(puffs, park);
            return (ball, face, cloud, park);
        }

        [UnityTest]
        public IEnumerator RollingOffATableLooksScaredThenPoofsOnTheFloor()
        {
            var (ball, face, cloud, park) = Build();
            Spawn(PrimitiveType.Cube, "Table", new Vector3(0f, 0.7f, 0f), new Vector3(0.6f, 0.05f, 0.6f));
            var expired = false;
            ball.Expired += _ => expired = true;
            yield return new WaitForFixedUpdate();
            ball.Launch(new Vector3(1.2f, 0f, 0f)); // rolls along the table top and off the edge
            ball.transform.position = new Vector3(0.1f, 0.78f, 0f);

            bool wasScared = false, sawFloorPause = false;
            Vector3 landedNear = Vector3.zero;
            for (float end = Time.time + 3f; Time.time < end && !expired;)
            {
                yield return new WaitForFixedUpdate();
                if (face.Current == Expression.Scared && !ball.OnTheFloor) wasScared = true;
                if (ball.OnTheFloor)
                {
                    sawFloorPause = true;
                    Assert.AreEqual(Expression.Scared, face.Current, "startled on the floor before the poof");
                    Assert.IsFalse(cloud.Playing, "the poof waits until the startled moment is over");
                }
                landedNear = ball.transform.position;
            }
            Assert.IsTrue(wasScared, "looked scared on the way down");
            Assert.IsTrue(sawFloorPause, "sat startled on the floor for a moment");
            Assert.IsTrue(expired, "gone once it hit the floor");
            Assert.IsTrue(cloud.Playing, "poof!");
            Assert.Less(Vector3.Distance(cloud.transform.position, new Vector3(landedNear.x, 0f, landedNear.z)), 0.3f, "the poof is where it landed");
            yield return new WaitForSeconds(PoofCloud.Seconds * 0.5f);
            Assert.Greater(cloud.Puffs[1].localPosition.magnitude, 0.03f, "puffs burst outward");
            Assert.Greater(cloud.Puffs[0].localPosition.y, 0.03f, "the middle puff billows up");
            yield return new WaitForSeconds(PoofCloud.Seconds * 0.6f);
            Assert.IsFalse(cloud.Playing);
            Assert.AreEqual(park.position, cloud.transform.position, "cloud back inside the stand");
            Assert.AreEqual(Vector3.zero, cloud.Puffs[0].localPosition);
        }

        [UnityTest]
        public IEnumerator LandingOnFurnitureIsNotAPoof()
        {
            var (ball, face, cloud, _) = Build();
            Spawn(PrimitiveType.Cube, "Chair", new Vector3(0f, 0.4f, 0f), new Vector3(1f, 0.1f, 1f));
            var expired = false;
            ball.Expired += _ => expired = true;
            yield return new WaitForFixedUpdate();
            ball.Launch(Vector3.zero); // drops onto the chair seat
            bool scared = false;
            for (float end = Time.time + 1.2f; Time.time < end;)
            {
                yield return new WaitForFixedUpdate();
                scared |= face.Current == Expression.Scared;
            }
            Assert.IsFalse(scared, "a short drop onto furniture is not scary");
            Assert.IsFalse(expired);
            Assert.IsFalse(cloud.Playing);
        }
    }
}
