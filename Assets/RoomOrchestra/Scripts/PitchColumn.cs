using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Five pitch tiles stacked low to high beside a target (height = pitch, as the design asks).
    /// The tile for the currently selected pitch is shown larger on the focused target's column,
    /// so controller users can see what A/X will place.
    /// </summary>
    public class PitchColumn : MonoBehaviour
    {
        [SerializeField] InstrumentTarget target;
        [Tooltip("Tiles lowest first.")]
        [SerializeField] PitchTile[] tiles;
        [Tooltip("Unselected tile size (the scene may author the start pitch's tile enlarged).")]
        [SerializeField] Vector3 tileScale = new Vector3(0.14f, 0.11f, 0.03f);
        [SerializeField, Min(1f)] float selectedScale = 1.2f;

        public InstrumentTarget Target => target;
        public PitchTile[] Tiles => tiles;

        public Vector3 TileScale => tileScale;

        public void Configure(InstrumentTarget owner, PitchTile[] stack, Vector3 unselectedScale)
        {
            target = owner;
            tiles = stack;
            tileScale = unselectedScale;
        }

        /// <summary>Highlights <paramref name="petal"/>, or nothing when <paramref name="petal"/> is -1.</summary>
        public void Show(int petal)
        {
            if (tiles == null) return;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null) continue;
                var scale = tileScale * (i == petal ? selectedScale : 1f);
                if (tiles[i].transform.localScale != scale) tiles[i].transform.localScale = scale;
            }
        }
    }
}
