using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The x on Flutter's speech bubble (owner, headset 2026-11-22: "There should also be an 'x' to exit the dialog box
    /// if it ever appears, because when the user clicks another menu item like the two player, it will display behind
    /// the dialog box"). Shown on Flutter's lines after the lesson; it sends the bubble back into Flutter at once.
    /// During the lesson the bubble has its Skip leaf instead.
    /// </summary>
    public class BubbleCloseButton : MonoBehaviour, IPressable
    {
        public void Press() => TutorialDirector.Instance?.Dismiss();
    }
}
