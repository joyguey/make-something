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
    /// With the controllers put down, the hands point instead: PICO's hand aim ray, and a pinch selects (the owner
    /// pinched the ball on the headset, 2026-11-08).
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
#if UNITY_ANDROID && !UNITY_EDITOR && ENABLE_PICO_XR_SDK
            if (ByteDance.PICO.XR.PXR_HandTracking.GetActiveInputDevice() == ByteDance.PICO.XR.ActiveInputDevice.HandTrackingActive)
                return ReadHand(hand);
#endif
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

#if UNITY_ANDROID && !UNITY_EDITOR && ENABLE_PICO_XR_SDK
        static PointerFrame ReadHand(int hand)
        {
            var aim = new ByteDance.PICO.XR.HandAimState();
            if (!ByteDance.PICO.XR.PXR_HandTracking.GetAimState(hand == 0 ? ByteDance.PICO.XR.HandType.HandLeft : ByteDance.PICO.XR.HandType.HandRight, ref aim))
                return default;
            var status = aim.aimStatus;
            return new PointerFrame
            {
                Tracked = (status & ByteDance.PICO.XR.HandAimStatus.AimRayValid) != 0,
                Position = aim.aimRayPose.Position.ToVector3(),
                Rotation = aim.aimRayPose.Orientation.ToQuat(),
                Select = (status & (ByteDance.PICO.XR.HandAimStatus.AimRayTouched | ByteDance.PICO.XR.HandAimStatus.AimIndexPinching)) != 0,
            };
        }
#endif
    }
}
