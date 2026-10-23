using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Tappable orb in the tray. Selecting it chooses the orb for placing and for the next throw.
    /// The selected token is shown larger (a size cue, not only a color cue).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class OrbToken : MonoBehaviour
    {
        [SerializeField] OrbType orb;
        [Tooltip("Unselected size; set by the scene setup tool from the orb's proportions.")]
        [SerializeField] Vector3 baseScale = Vector3.one * 0.1f;
        [SerializeField, Min(1f)] float selectedScale = 1.3f;

        public OrbType Orb => orb;
        public bool IsSelected => transform.localScale.sqrMagnitude > baseScale.sqrMagnitude * 1.01f;

        public void SetSelected(bool selected)
        {
            Vector3 target = selected ? baseScale * selectedScale : baseScale;
            if (transform.localScale != target) // only touch transforms on real changes (Spatial renderer)
                transform.localScale = target;
        }
    }
}
