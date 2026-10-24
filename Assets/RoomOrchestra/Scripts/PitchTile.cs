using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// One labeled tile in a target's pitch column. Tapping it plays the selected orb on that target
    /// at this tile's note (or udu sound) and plants an echo: one tap, and the label says what you hear.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PitchTile : MonoBehaviour
    {
        [SerializeField] InstrumentTarget target;
        [SerializeField, Range(0, PitchInput.PetalCount - 1)] int petal;

        public InstrumentTarget Target => target;
        public int Petal => petal;

        public void Configure(InstrumentTarget owner, int index)
        {
            target = owner;
            petal = index;
        }
    }
}
