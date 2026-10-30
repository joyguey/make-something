using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>The leaf on Flutter's speech bubble that skips the guided song (its tap collider is on its visible leaf).</summary>
    public class TutorialSkipButton : MonoBehaviour
    {
        public void Press() => TutorialDirector.Instance?.Skip();
    }
}
