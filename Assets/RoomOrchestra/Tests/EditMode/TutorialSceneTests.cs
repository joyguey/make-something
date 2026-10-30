using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// The guided song as the scene is saved (what PICO Spatial exports): the lamp and table stations parked
    /// inside the ball stand's cloud, safely sized, with nothing to hit or tap; the chair at home; Flutter's
    /// bubble and the glow waiting in the cloud. Skipping brings every station home.
    /// </summary>
    public class TutorialSceneTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";

        [SetUp]
        public void OpenScene() => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        static Vector3 Park => GameObject.Find("RoomOrchestra").transform.Find("BallDispenser/ParkPoint").position;

        static StationReveal[] Stations => Object.FindObjectsByType<StationReveal>(FindObjectsSortMode.None);

        [Test]
        public void TheLampAndTableAreSavedInsideTheCloudAndTheChairAtHome()
        {
            CollectionAssert.AreEquivalent(new[] { InstrumentType.Udu, InstrumentType.Erhu }, Stations.Select(s => s.Instrument));
            var core = new Bounds(Park, new Vector3(0.12f, 0.16f, 0.12f)); // the cloud's solid core
            foreach (var station in Stations)
            {
                Assert.IsTrue(station.Hidden, station.name);
                foreach (var part in station.Parts)
                    foreach (var r in part.GetComponentsInChildren<Renderer>())
                    {
                        Assert.IsTrue(core.Contains(r.bounds.min) && core.Contains(r.bounds.max), $"{r.name} of {part.name} pokes out of the cloud: {r.bounds}");
                        Assert.IsFalse(r.GetComponent<Collider>() != null && r.GetComponent<Collider>().enabled, $"{r.name} is tappable while hidden");
                    }
            }
            var chair = Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None).First(t => t.Instrument == InstrumentType.Mbira);
            Assert.Greater(Vector3.Distance(chair.AimPosition, Park), 0.5f, "the chair is at home");
        }

        [Test]
        public void NothingIsSavedAtNearZeroScale()
        {
            // As SceneLayoutTests.NothingAuthoredAtNearZeroScale, but for the scene exactly as saved (stations hidden).
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                var s = t.lossyScale;
                float min = Mathf.Min(Mathf.Abs(s.x), Mathf.Min(Mathf.Abs(s.y), Mathf.Abs(s.z)));
                if (t.GetComponent<Renderer>() != null)
                    Assert.Greater(min, 0.001f, $"{t.name} is saved at near-zero scale {s}");
            }
        }

        [Test]
        public void BubbleAndGlowWaitInTheCloud()
        {
            var tutorial = Object.FindFirstObjectByType<TutorialDirector>();
            Assert.Less(Vector3.Distance(tutorial.Bubble.position, Park), 1e-3f);
            Assert.Less(Vector3.Distance(tutorial.Glow.position, Park), 1e-3f);
            Assert.IsTrue(tutorial.Bubble.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "the parked Skip leaf catches no taps");
            Assert.IsNotNull(tutorial.Bubble.GetComponentInChildren<TutorialSkipButton>(), "the bubble carries the Skip leaf");
        }

        [Test]
        public void SkippingTheOpeningBringsEveryStationHome()
        {
            Object.FindFirstObjectByType<IntroSequence>().Finish();
            foreach (var station in Stations)
            {
                Assert.IsFalse(station.Hidden, station.name);
                Assert.Greater(Vector3.Distance(station.Parts[0].position, Park), 0.5f, $"{station.Parts[0].name} is home");
                Assert.IsTrue(station.Parts[0].GetComponentsInChildren<Collider>().Any(c => c.enabled), $"{station.Parts[0].name} can be hit");
            }
            var table = Stations.First(s => s.Instrument == InstrumentType.Udu).Parts[0];
            Assert.AreEqual(-1.1f, table.localPosition.x, 1e-3f, "the table back where the scene setup put it");
        }
    }
}
