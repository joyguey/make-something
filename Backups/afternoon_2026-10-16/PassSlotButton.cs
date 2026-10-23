using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>Tap target on the <see cref="LooperPanel"/>: a pass tile (mute) or its remove block.</summary>
    [RequireComponent(typeof(Collider))]
    public class PassSlotButton : MonoBehaviour
    {
        [SerializeField] LooperPanel panel;
        [SerializeField, Min(0)] int slot;
        [Tooltip("Off: mute/unmute the pass. On: remove the pass.")]
        [SerializeField] bool remove;

        public void Press()
        {
            if (panel != null) panel.PressSlot(slot, remove);
        }
    }
}
