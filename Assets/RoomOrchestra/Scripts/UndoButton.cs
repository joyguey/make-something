using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>Tappable undo: reverts the last echo edit (plant, remove, nudge, clear).</summary>
    [RequireComponent(typeof(Collider))]
    public class UndoButton : MonoBehaviour
    {
        public void Press() => EchoConductor.Instance?.Undo();
    }
}
