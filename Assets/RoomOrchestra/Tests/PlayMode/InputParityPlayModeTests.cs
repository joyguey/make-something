using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// PlayMode checks with real physics and real components (no headset, no PICO runtime):
    /// bounce debounce, toss/place/controller parity, and orb re-skinning.
    /// </summary>
    public class InputParityPlayModeTests
    {
        class CollisionCounter : MonoBehaviour
        {
            public int Count;
            void OnCollisionEnter(Collision c) => Count++;
        }

        class FakeControllerSource : IControllerSource
        {
            public ControllerFrame Left = new ControllerFrame { Connected = true };
            public ControllerFrame Right;
            public ControllerFrame Read(int hand) => hand == 0 ? Left : Right;
        }

        readonly List<GameObject> m_Spawned = new();
        readonly List<MusicalHitEvent> m_Events = new();

        [SetUp]
        public void SetUp()
        {
            MusicalHitPipeline.Reset();
            MusicalHitPipeline.Committed += m_Events.Add;
        }

        [TearDown]
        public void TearDown()
        {
            MusicalHitPipeline.Committed -= m_Events.Add;
            foreach (var go in m_Spawned) if (go != null) Object.Destroy(go);
            m_Spawned.Clear();
            m_Events.Clear();
        }

        T Track<T>(T component) where T : Component
        {
            m_Spawned.Add(component.gameObject);
            return component;
        }

        InstrumentTarget MakeTarget(InstrumentType type, Vector3 position, float bounciness = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Target_{type}";
            go.transform.position = position;
            go.transform.localScale = new Vector3(2f, 0.2f, 2f);
            go.GetComponent<Collider>().sharedMaterial = new PhysicsMaterial
            {
                bounciness = bounciness,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
            go.AddComponent<AudioSource>();
            var target = go.AddComponent<InstrumentTarget>();
            target.Configure(type, new[] { AudioClip.Create("test", 441, 1, 44100, false) });
            return Track(target);
        }

        Ball MakeBall(OrbType orb, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Ball_{orb}";
            go.transform.position = position;
            go.AddComponent<Rigidbody>();
            var ball = go.AddComponent<Ball>();
            ball.ApplyOrb(orb);
            return Track(ball);
        }

        static IEnumerator Physics(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator BouncingBallProducesExactlyOneEvent()
        {
            MakeTarget(InstrumentType.Udu, Vector3.zero, bounciness: 1f);
            var ball = MakeBall(OrbType.Bop, new Vector3(0f, 1f, 0f));
            var counter = ball.gameObject.AddComponent<CollisionCounter>();
            ball.Launch(new Vector3(0f, -3f, 0f));

            yield return Physics(2.5f);

            Assert.Greater(counter.Count, 1, "the ball really bounced several times");
            Assert.AreEqual(1, m_Events.Count, "one throw -> one musical event");
            Assert.AreEqual(InputPath.Toss, m_Events[0].Path);
            Assert.AreEqual(OrbType.Bop, m_Events[0].Orb);
        }

        [UnityTest]
        public IEnumerator EveryOrbWorksWithTheSameGentleThrow(
            [Values(OrbType.Flutter, OrbType.Bop, OrbType.Boom)] OrbType orb)
        {
            MakeTarget(InstrumentType.Mbira, Vector3.zero);
            var ball = MakeBall(orb, new Vector3(0f, 0.6f, 0f));
            ball.Launch(new Vector3(0f, -1f, 0f)); // gentle: no orb needs extra force

            yield return Physics(1f);

            Assert.AreEqual(1, m_Events.Count, $"{orb} produced an event");
            Assert.AreEqual(orb, m_Events[0].Orb);
        }

        [UnityTest]
        public IEnumerator PlaceReproducesTheTossIntentExactly()
        {
            var target = MakeTarget(InstrumentType.Erhu, Vector3.zero);
            var ball = MakeBall(OrbType.Flutter, new Vector3(0f, 1f, 0f));
            ball.Launch(new Vector3(0f, -4f, 0f));
            yield return Physics(1f);
            Assert.AreEqual(1, m_Events.Count);
            var toss = m_Events[0];

            var selector = Track(new GameObject("Selector").AddComponent<OrbSelector>());
            selector.Configure(new[] { target }, null);
            selector.Select(toss.Orb);
            selector.SetTier(toss.Tier);
            var place = selector.PlaceOn(target, toss.Pitch);

            Assert.IsNotNull(place);
            Assert.AreEqual(InputPath.Place, place.Path);
            Assert.IsTrue(toss.IsSameIntent(place), $"toss {toss} vs place {place}");
            Assert.AreEqual(2, m_Events.Count, "both went through the one pipeline");
        }

        [UnityTest]
        public IEnumerator ControllerOnlyOneHandedPlaceWithoutThrowing()
        {
            var table = MakeTarget(InstrumentType.Udu, new Vector3(-2f, 0f, 0f));
            var chair = MakeTarget(InstrumentType.Mbira, new Vector3(0f, 0f, 0f));
            var lamp = MakeTarget(InstrumentType.Erhu, new Vector3(2f, 0f, 0f));
            var selector = Track(new GameObject("Selector").AddComponent<OrbSelector>());
            selector.Configure(new[] { table, chair, lamp }, null);
            var input = selector.gameObject.AddComponent<ControllerButtonInput>();
            var fake = new FakeControllerSource();
            input.Source = fake;
            input.Configure(selector);

            var idle = new ControllerFrame { Connected = true };
            IEnumerator Frame(ControllerFrame f)
            {
                fake.Left = f; // left hand only
                yield return null;
            }

            yield return Frame(idle);
            yield return Frame(new ControllerFrame { Connected = true, StickX = 1f }); // Flutter -> Bop
            yield return Frame(idle);
            yield return Frame(new ControllerFrame { Connected = true, StickY = 1f }); // chair -> lamp
            yield return Frame(idle);
            yield return Frame(new ControllerFrame { Connected = true, Secondary = true }); // Medium -> Strong
            yield return Frame(idle);
            yield return Frame(new ControllerFrame { Connected = true, Primary = true }); // place
            yield return Frame(new ControllerFrame { Connected = true, Primary = true }); // still held
            yield return Frame(idle);

            Assert.AreEqual(1, m_Events.Count, "held button placed once");
            var e = m_Events[0];
            Assert.AreEqual(OrbType.Bop, e.Orb);
            Assert.AreEqual(InstrumentType.Erhu, e.Target);
            Assert.AreEqual(IntensityTier.Strong, e.Tier);
            Assert.AreEqual(InputPath.Place, e.Path);
        }

        [UnityTest]
        public IEnumerator RepeatedPlacesAreSeparateActions()
        {
            var target = MakeTarget(InstrumentType.Erhu, Vector3.zero);
            var selector = Track(new GameObject("Selector").AddComponent<OrbSelector>());
            selector.Configure(new[] { target }, null);
            yield return null;
            var a = selector.PlaceOn(target);
            var b = selector.PlaceOn(target);
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreNotEqual(a.ActionId, b.ActionId);
            Assert.IsTrue(a.IsSameIntent(b), "same choices, same musical answer");
        }

        [UnityTest]
        public IEnumerator ParkedBallsStandUprightAndFitInsideTheDispenserPost()
        {
            var spawn = Track(new GameObject("Spawn").transform);
            spawn.position = new Vector3(0f, 1f, 0f);
            var balls = new[] { MakeBall(OrbType.Boom, Vector3.zero), MakeBall(OrbType.Boom, Vector3.zero) };
            foreach (var b in balls) b.transform.rotation = Quaternion.Euler(70f, 20f, 35f); // landed tilted
            var launcher = Track(new GameObject("Launcher").AddComponent<BallLauncher>());
            launcher.Configure(spawn, balls);
            yield return null; // Start parks the spares

            var parked = balls[0] == launcher.ReadyBall ? balls[1] : balls[0];
            yield return new WaitForFixedUpdate();
            Assert.Less(Quaternion.Angle(Quaternion.identity, parked.transform.rotation), 0.1f, "upright");
            var size = parked.GetComponent<Renderer>().bounds.size;
            const float postWidth = 0.12f;
            Assert.Less(Mathf.Max(size.x, size.z), postWidth - 0.02f, $"fits inside the post with margin ({size})");

            launcher.SetOrb(OrbType.Boom);
            Assert.Greater(launcher.ReadyBall.transform.localScale.x, launcher.ReadyBall.transform.localScale.y, "handed out as a wide Boom again");
        }

        [UnityTest]
        public IEnumerator SelectingAnOrbReskinsTheReadyBall()
        {
            var spawn = Track(new GameObject("Spawn").transform);
            spawn.position = new Vector3(0f, 1f, 0f);
            var balls = new[] { MakeBall(OrbType.Flutter, Vector3.down * 3f), MakeBall(OrbType.Flutter, Vector3.down * 3f) };
            var launcher = Track(new GameObject("Launcher").AddComponent<BallLauncher>());
            launcher.Configure(spawn, balls);
            yield return null; // Start prepares the ready ball

            Assert.IsNotNull(launcher.ReadyBall);
            launcher.SetOrb(OrbType.Boom);
            var ready = launcher.ReadyBall;
            Assert.AreEqual(OrbType.Boom, ready.Orb);
            Assert.Greater(ready.transform.localScale.x, ready.transform.localScale.y, "Boom is wide");
            Assert.AreEqual(OrbCatalog.Get(OrbType.Boom).Mass, ready.GetComponent<Rigidbody>().mass, 1e-4f);
        }
    }
}
