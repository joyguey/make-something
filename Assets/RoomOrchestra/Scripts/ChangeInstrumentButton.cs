using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Under an instrument's name tile: gives this furniture the next instrument (it swaps with whichever furniture
    /// had it; the song follows its instruments). Its tap collider is on its visible slab.
    /// </summary>
    public class ChangeInstrumentButton : MonoBehaviour
    {
        [SerializeField] InstrumentTarget target;

        public InstrumentTarget Target => target;

        public void Configure(InstrumentTarget furniture) => target = furniture;

        public bool Press() => InstrumentPacks.Instance != null && InstrumentPacks.Instance.ChangeInstrument(target);
    }
}
