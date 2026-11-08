using UnityEngine;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using InputDevice = UnityEngine.XR.InputDevice;

namespace RoomOrchestra
{
    /// <summary>One controller's pointer this frame, in tracking (rig) space.</summary>
    public struct PointerFrame
    {
        public bool Tracked;
        public Vector3 Position;
        public Quaternion Rotation;
        /// <summary>The trigger is held (select: tap, or hold to grab the ball).</summary>
        public bool Select;
    }

    /// <summary>Source of per-hand pointer frames; swapped for a fake in tests.</summary>
    public interface IPointerSource
    {
        /// <summary>0 = left, 1 = right.</summary>
        PointerFrame Read(int hand);
    }

    /// <summary>
    /// PICO controllers in PICO XR mode through UnityEngine.XR InputDevices: the controller's pose and its trigger.
    /// (In PICO Spatial mode the system turned the trigger into pinch touches; in PICO XR mode the app reads it.)
    /// </summary>
    public sealed class XRPointerSource : IPointerSource
    {
        /// <summary>Trigger travel that counts as pressed.</summary>
        public const float Pressed = 0.6f, Released = 0.4f;

        readonly InputDevice[] m_Devices = new InputDevice[2];
        readonly float[] m_NextTry = new float[2];
        readonly bool[] m_Held = new bool[2];

        public XRPointerSource() => InputDevices.deviceConnected += _ => m_NextTry[0] = m_NextTry[1] = 0f;

        public PointerFrame Read(int hand)
        {
            if (!m_Devices[hand].isValid)
            {
                if (Time.unscaledTime < m_NextTry[hand]) return default;
                m_NextTry[hand] = Time.unscaledTime + XRControllerSource.RetrySeconds;
                m_Devices[hand] = InputDevices.GetDeviceAtXRNode(hand == 0 ? XRNode.LeftHand : XRNode.RightHand);
                if (!m_Devices[hand].isValid) return default;
            }
            var device = m_Devices[hand];
            var frame = new PointerFrame();
            device.TryGetFeatureValue(CommonUsages.isTracked, out frame.Tracked);
            if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out frame.Position)) frame.Tracked = false;
            if (!device.TryGetFeatureValue(CommonUsages.deviceRotation, out frame.Rotation)) frame.Tracked = false;
            // hysteresis, so a trigger resting half way does not flicker between tap and release
            if (device.TryGetFeatureValue(CommonUsages.trigger, out float trigger))
                m_Held[hand] = m_Held[hand] ? trigger > Released : trigger > Pressed;
            else if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool button))
                m_Held[hand] = button;
            frame.Select = m_Held[hand];
            return frame;
        }
    }
}
