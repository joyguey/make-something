using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Tappable undo: reverts the last echo edit (plant, remove, nudge, clear). While the player is choosing furniture
    /// (and until the first note after it), it takes back the last furniture chosen instead (owner, headset 2026-11-08).
    /// </summary>
    // Its tap collider sits on its visible slab, where PICO's hover highlight can show.
    public class UndoButton : MonoBehaviour
    {
        public void Press()
        {
            if (RealFurniture.Instance != null && RealFurniture.Instance.UndoChoice()) return;
            EchoConductor.Instance?.Undo();
        }
    }
}
