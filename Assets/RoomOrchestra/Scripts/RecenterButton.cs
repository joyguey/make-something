using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>Tappable Recenter: brings the stage in front of wherever the player sits and faces.</summary>
    // Its tap collider sits on its visible slab, where PICO's hover highlight can show.
    public class RecenterButton : MonoBehaviour
    {
        public void Press()
        {
            if (StageAnchor.Instance != null) StageAnchor.Instance.RecenterToHead();
        }
    }
}
