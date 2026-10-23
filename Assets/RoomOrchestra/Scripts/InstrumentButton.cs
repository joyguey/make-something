using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>In-world 3D button that plays a linked <see cref="InstrumentTarget"/>.</summary>
    [RequireComponent(typeof(Collider))]
    public class InstrumentButton : MonoBehaviour
    {
        [SerializeField] InstrumentTarget target;

        ColorFlash m_Flash;

        public InstrumentTarget Target => target;

        void Awake() => m_Flash = GetComponent<ColorFlash>();

        public void Press()
        {
            if (m_Flash != null)
                m_Flash.Flash();
            if (target != null)
                target.PlayInstrument();
        }
    }
}
