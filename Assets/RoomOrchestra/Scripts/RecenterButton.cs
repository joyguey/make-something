using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>Tappable Recenter: brings the stage in front of wherever the player sits and faces.</summary>
    [RequireComponent(typeof(Collider))]
    public class RecenterButton : MonoBehaviour
    {
        public void Press()
        {
            if (StageAnchor.Instance != null) StageAnchor.Instance.RecenterToHead();
        }
    }
}
