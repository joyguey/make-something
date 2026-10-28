using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Tappable intensity preset (quiet / medium / strong). Shows the tier as 1-3 raised bars,
    /// so dynamics are chosen without any physical strength and read without color.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TierButton : MonoBehaviour
    {
        [Tooltip("Bars from quiet to strong; bars up to the current tier are raised.")]
        [SerializeField] Transform[] bars;
        [SerializeField] Vector3 raisedScale = new Vector3(0.03f, 0.05f, 0.03f);
        [SerializeField] Vector3 loweredScale = new Vector3(0.03f, 0.008f, 0.03f);

        public void Show(IntensityTier tier)
        {
            if (bars == null) return;
            for (int i = 0; i < bars.Length; i++)
            {
                if (bars[i] == null) continue;
                Vector3 target = i <= (int)tier ? raisedScale : loweredScale;
                if (bars[i].localScale != target)
                    bars[i].localScale = target;
            }
        }
    }
}
