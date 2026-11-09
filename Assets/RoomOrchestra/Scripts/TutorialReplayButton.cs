using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>The leaf's Lesson button: plays the guided song again (its tap collider is on its visible slab).</summary>
    public class TutorialReplayButton : MonoBehaviour
    {
        public void Press() => TutorialDirector.Instance?.Replay();
    }
}
