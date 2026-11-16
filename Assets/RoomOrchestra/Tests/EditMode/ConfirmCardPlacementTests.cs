using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// The question card never stands inside another sign or menu (owner, headset 2026-11-15: "make sure no signs or
    /// pop-up menus get into each other's way... the dialog box that came up clipped through the sign").
    /// </summary>
    public class ConfirmCardPlacementTests
    {
        [Test]
        public void OnTheTitleSignTheQuestionStandsInFrontOfTheSign()
        {
            EditorSceneManager.OpenScene("Assets/RoomOrchestra/Scenes/RoomOrchestra.unity", OpenSceneMode.Single);
            var title = Object.FindFirstObjectByType<TitleScreen>();
            var quit = title.Card.GetComponentInChildren<QuitButton>(true);
            var card = Object.FindFirstObjectByType<ConfirmCard>();
            Assert.IsNotNull(quit);
            Assert.IsNotNull(card);
            try
            {
                card.Show(quit);
                Assert.IsTrue(card.StandsClear(quit, Viewer.SeatedEye), "clear of the sign, and Quit still in view for the second tap");
                var plate = title.Card.Find("Hanging/TitlePanel").GetComponent<Renderer>().bounds;
                Assert.Less(card.Box().max.z, plate.min.z, "wholly in front of the sign's plate");
                Assert.Greater(card.Box().min.z, 0.3f, "and not in the player's face");
            }
            finally { card.Hide(); }
        }

        [Test]
        public void OnTheLeafEveryQuestionStandsClearOfTheMenuAndTheLeaf()
        {
            EditorSceneManager.OpenScene("Assets/RoomOrchestra/Scenes/RoomOrchestra.unity", OpenSceneMode.Single);
            Object.FindFirstObjectByType<IntroSequence>().Finish(); // free play: the title is away, the leaf is home
            TuckAway.ShowAllNow();
            var card = Object.FindFirstObjectByType<ConfirmCard>();
            var askers = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(m => (m is QuitButton || m is TutorialReplayButton || m is StartOverButton) && !m.transform.IsChildOf(Object.FindFirstObjectByType<TitleScreen>().Card))
                .ToList();
            Assert.GreaterOrEqual(askers.Count, 3, "Quit, Lesson and Start over on the leaf");
            try
            {
                foreach (var asker in askers)
                {
                    card.Show(asker);
                    Assert.IsTrue(card.StandsClear(asker, Viewer.SeatedEye), $"{asker.name}: the question touches nothing on the leaf and leaves the button in view");
                }
            }
            finally { card.Hide(); }
        }
    }
}
