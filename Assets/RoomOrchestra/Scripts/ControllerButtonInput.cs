using UnityEngine;
using UnityEngine.XR;
using InputDevice = UnityEngine.XR.InputDevice;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace RoomOrchestra
{
    /// <summary>Source of per-hand controller frames; swapped for a fake in tests and the editor.</summary>
    public interface IControllerSource
    {
        /// <summary>0 = left, 1 = right.</summary>
        ControllerFrame Read(int hand);
    }

    /// <summary>
    /// Reads PICO controllers through UnityEngine.XR InputDevices, which PICO documents for Spatial
    /// mode. The trigger is not used: in Spatial mode it already drives pinch/tap via Spatial Input.
    /// </summary>
    public sealed class XRControllerSource : IControllerSource
    {
        readonly InputDevice[] m_Devices = new InputDevice[2];

        public ControllerFrame Read(int hand)
        {
            if (!m_Devices[hand].isValid)
            {
                m_Devices[hand] = InputDevices.GetDeviceAtXRNode(hand == 0 ? XRNode.LeftHand : XRNode.RightHand);
                if (!m_Devices[hand].isValid) return default;
            }
            var device = m_Devices[hand];
            var frame = new ControllerFrame { Connected = true };
            if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick))
            {
                frame.StickX = stick.x;
                frame.StickY = stick.y;
            }
            device.TryGetFeatureValue(CommonUsages.primaryButton, out frame.Primary);
            device.TryGetFeatureValue(CommonUsages.secondaryButton, out frame.Secondary);
            return frame;
        }
    }

    /// <summary>
    /// One-handed controller path: stick left/right picks the orb, up/down picks the target,
    /// A/X places, B/Y changes intensity. Either controller alone is enough.
    /// </summary>
    public class ControllerButtonInput : MonoBehaviour
    {
        [SerializeField] OrbSelector selector;

        readonly ControllerCommandMapper m_Mapper = new();

        public IControllerSource Source { get; set; } = new XRControllerSource();

        public void Configure(OrbSelector orbSelector) => selector = orbSelector;

        void OnDisable() => m_Mapper.Reset();

        void Update()
        {
            if (selector == null || Source == null) return;
            foreach (var command in m_Mapper.Update(Source.Read(0), Source.Read(1)))
            {
                Debug.Log($"[RoomOrchestra] Controller {command}");
                selector.Execute(command);
            }
        }
    }
}
