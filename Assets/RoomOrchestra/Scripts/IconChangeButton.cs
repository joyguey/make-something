using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The instrument's picture over a real piece of furniture is also its Change button (owner, headset 2026-11-12: "I
    /// also don't know how to switch out an instrument, where can I do this?"): a tap gives the furniture the next
    /// instrument (all eight, the V4 library included), as the Change tiles did in the lesson's room.
    /// </summary>
    public class IconChangeButton : MonoBehaviour, IPressable
    {
        [SerializeField] InstrumentTarget target;

        public InstrumentTarget Target => target;
        public void Configure(InstrumentTarget piece) => target = piece;

        public void Press()
        {
            var packs = InstrumentPacks.Instance;
            if (packs != null && target != null) packs.ChangeInstrument(target);
        }
    }
}
