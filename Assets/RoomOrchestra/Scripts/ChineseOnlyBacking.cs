using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The dark plate behind a Chinese-only label (1=D). In Chinese it sits behind the text so it reads
    /// against a light wall; in English the label is blank, so the plate waits inside the tile below
    /// (PICO Spatial: objects are never hidden, only parked out of sight).
    /// </summary>
    public class ChineseOnlyBacking : MonoBehaviour
    {
        [SerializeField] Transform plate;
        [SerializeField] Vector3 shownLocalPosition, parkedLocalPosition;

        public Transform Plate => plate;
        public bool Shown => plate != null && (plate.localPosition - shownLocalPosition).sqrMagnitude < 1e-8f;

        public void Configure(Transform backing, Vector3 shown, Vector3 parked)
        {
            plate = backing;
            shownLocalPosition = shown;
            parkedLocalPosition = parked;
        }

        void OnEnable()
        {
            Localization.Changed += Apply;
            Apply(Localization.Current);
        }

        void OnDisable() => Localization.Changed -= Apply;

        void Apply(UiLanguage language)
        {
            if (plate == null) return;
            var target = language == UiLanguage.Chinese ? shownLocalPosition : parkedLocalPosition;
            if (plate.localPosition != target) plate.localPosition = target;
        }
    }
}
