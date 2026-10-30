using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// Each character's own bounce off a wall, with real physics: Flutter, Bop and Boom each play their own sound;
    /// a soft bump is silent; the floor and an instrument never play it. Editor physics, not headset evidence;
    /// nobody listened to the sound here.
    /// </summary>
    public class BouncePlayModeTests
    {
        readonly List<GameObject> m_Spawned = new();
        AudioClip[] m_Clips;

        [SetUp]
        public void SetUp()
        {
            MusicalHitPipeline.Reset();
            m_Clips = new[] { "Flutter", "Bop", "Boom" }.Select(n => AudioClip.Create($"Bounce_{n}", 441, 1, 44100, false)).ToArray();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in m_Spawned) if (go != null) Object.Destroy(go);
            m_Spawned.Clear();
        }

        GameObject Wall(Vector3 at)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = at;
            wall.transform.localScale = new Vector3(2f, 2f, 0.1f);
            wall.AddComponent<BounceSurface>();
            m_Spawned.Add(wall);
            return wall;
        }

        Ball Throw(OrbType orb, Vector3 from, Vector3 velocity)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.position = from;
            go.transform.localScale = Vector3.one * 0.1f;
            var body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            var ball = go.AddComponent<Ball>();
            ball.ApplyOrb(orb);
            ball.ConfigureBounce(go.AddComponent<AudioSource>(), m_Clips);
            m_Spawned.Add(go);
            ball.Launch(velocity);
            return ball;
        }

        static IEnumerator Physics(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator EachCharacterBouncesOffAWallWithItsOwnSound()
        {
            Wall(new Vector3(0f, 1f, 2f));
            var balls = new[]
            {
                Throw(OrbType.Flutter, new Vector3(-0.5f, 1f, 1f), new Vector3(0f, 0f, 4f)),
                Throw(OrbType.Bop, new Vector3(0f, 1f, 1f), new Vector3(0f, 0f, 4f)),
                Throw(OrbType.Boom, new Vector3(0.5f, 1f, 1f), new Vector3(0f, 0f, 4f)),
            };
            yield return Physics(0.6f);
            for (int i = 0; i < balls.Length; i++)
                Assert.AreSame(m_Clips[i], balls[i].LastBounce, $"{(OrbType)i} plays its own bounce");
        }

        [UnityTest]
        public IEnumerator ASoftBumpIsSilent()
        {
            Wall(new Vector3(0f, 1f, 2f));
            var ball = Throw(OrbType.Bop, new Vector3(0f, 1f, 1.8f), new Vector3(0f, 0f, 0.3f));
            yield return Physics(0.8f);
            Assert.IsNull(ball.LastBounce, "slower than the threshold: no sound");
        }

        [UnityTest]
        public IEnumerator FloorAndInstrumentsNeverPlayIt()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); // no BounceSurface: like the floor
            floor.transform.position = new Vector3(0f, -0.05f, 1f);
            floor.transform.localScale = new Vector3(4f, 0.1f, 4f);
            m_Spawned.Add(floor);
            var drop = Throw(OrbType.Boom, new Vector3(0f, 1f, 1f), new Vector3(0f, -4f, 0f));
            yield return Physics(0.6f);
            Assert.IsNull(drop.LastBounce, "landings stay silent");
        }

        [Test]
        public void LouderAndBrighterTheHarderItHits()
        {
            Assert.AreEqual(0f, BounceSound.Volume(BounceSound.MinSpeed - 0.01f));
            Assert.AreEqual(BounceSound.QuietVolume, BounceSound.Volume(BounceSound.MinSpeed), 1e-4f);
            Assert.Greater(BounceSound.Volume(3f), BounceSound.Volume(1f));
            Assert.AreEqual(BounceSound.LoudVolume, BounceSound.Volume(20f), 1e-4f, "never louder than this");
            Assert.Greater(BounceSound.Pitch(4f), BounceSound.Pitch(1f));
            Assert.Less(BounceSound.Pitch(50f), 1.04f, "within about a semitone");
        }
    }
}
