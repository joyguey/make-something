using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Menu: Play together (owner, 2026-11-22: multiplayer needs "an easier way to access than the easter egg"). Brings
    /// out the Echo Battle card, which offers HOST and JOIN for two headsets on one Wi-Fi, and SOLO against the game on
    /// this one. During the lesson it ends the lesson first (Lesson on the menu plays it again). The secret still works.
    /// </summary>
    public class TogetherButton : MonoBehaviour, IPressable
    {
        public void Press()
        {
            MenuTray.Instance?.SetOpen(false);
            var lesson = TutorialDirector.Instance;
            if (lesson != null && lesson.Active) lesson.Skip();
            EchoBattleDirector.Instance?.ShowFromMenu();
        }
    }
}
