using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>The scan's progress bar stays in clear view: right of the view, above the speech card, the leaf and the cloud.</summary>
    public class ScanBarPlacementTests
    {
        [Test]
        public void TheBarIsOnTheRightAboveEverythingThatCouldStandInFrontOfIt()
        {
            EditorSceneManager.OpenScene("Assets/RoomOrchestra/Scenes/RoomOrchestra.unity", OpenSceneMode.Single);
            Object.FindFirstObjectByType<IntroSequence>().Finish(skipTutorial: false);
            var eyes = Viewer.SeatedEye;
            float bottom = float.MaxValue;
            // whichever way the player turns while scanning
            for (int turn = 0; turn < 360; turn += 30)
            {
                var forward = Quaternion.Euler(0f, turn, 0f) * Vector3.forward;
                var at = ScanProgressBar.PlaceFor(eyes, forward);
                var right = Vector3.Cross(Vector3.up, forward);
                float bearing = Mathf.Atan2(Vector3.Dot(at - eyes, right), Vector3.Dot(at - eyes, forward)) * Mathf.Rad2Deg;
                Assert.That(bearing, Is.InRange(12f, 30f), "on the right of the view, well inside it");
                bottom = Mathf.Min(bottom, at.y - 0.09f); // the bar, its number and its hint reach 9 cm below its middle
            }
            Assert.Greater(bottom, eyes.y - 0.05f, "at eye height or above");
            foreach (var name in new[] { "TutorialBubble", "LeafPad", "BallDispenser" })
            {
                var thing = GameObject.Find(name);
                Assert.IsNotNull(thing, name);
                float top = float.MinValue;
                foreach (var r in thing.GetComponentsInChildren<Renderer>())
                    if (r.enabled && r.bounds.size.sqrMagnitude > 1e-6f && r.bounds.size.y < 1.5f) top = Mathf.Max(top, r.bounds.max.y);
                Assert.Less(top, bottom, $"{name} is wholly below the bar, so it can never stand in front of it");
            }
        }
    }
}
