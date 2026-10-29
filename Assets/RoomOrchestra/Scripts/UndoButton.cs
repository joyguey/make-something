using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>Tappable undo: reverts the last echo edit (plant, remove, nudge, clear).</summary>
    // Its tap collider sits on its visible slab, where PICO's hover highlight can show.
    public class UndoButton : MonoBehaviour
    {
        public void Press() => EchoConductor.Instance?.Undo();
    }
}
