using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Controller vibration (owner-approved Fable polish, 2026-11-14): a light tick when a press lands, a pulse as a
    /// ball is picked up and another as it is thrown, and a soft thump when the player's note sounds. Controllers only:
    /// bare hands have nothing to vibrate, and nothing in the game depends on feeling it (every pulse comes with the
    /// sound and the movement it belongs to). Short and gentle on purpose.
    /// </summary>
    public static class Haptics
    {
        /// <summary>Off for players who don't want vibration.</summary>
        public static bool Enabled { get; set; } = true;

        // strength (0..1) and length (ms) of each pulse
        public const float PressStrength = 0.25f, GrabStrength = 0.35f, ThrowStrength = 0.5f;
        public const int PressMs = 20, GrabMs = 30, ThrowMs = 45;

        /// <summary>The controller that last pressed, grabbed or threw (0 left, 1 right): where a hit is felt.</summary>
        public static int LastHand { get; private set; } = 1;
        /// <summary>The last pulse asked for, and how many (tests).</summary>
        public static (int hand, float strength, int ms) Last { get; private set; }
        public static int Count { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Enabled = true;
            LastHand = 1;
            Last = default;
            Count = 0;
        }

        /// <summary>The player's note sounded: felt in the hand that played it, by the ball's weight (Flutter a light
        /// tap, Bop a knock, Boom a thump), so weight is something you feel as well as hear.</summary>
        public static void Hit(OrbType orb)
        {
            var (strength, ms) = HitFor(orb);
            Pulse(LastHand, strength, ms, remember: false);
        }

        public static (float strength, int ms) HitFor(OrbType orb) => orb switch
        {
            OrbType.Flutter => (0.3f, 25),
            OrbType.Boom => (0.65f, 60),
            _ => (0.45f, 40),
        };

        /// <summary>Vibrates the controller in <paramref name="hand"/> (0 left, 1 right).</summary>
        public static void Pulse(int hand, float strength, int ms, bool remember = true)
        {
            if (remember) LastHand = hand;
            if (!Enabled) return;
            Last = (hand, strength, ms);
            Count++;
#if UNITY_ANDROID && !UNITY_EDITOR && ENABLE_PICO_XR_SDK
            // hands in use: no controller to vibrate
            if (ByteDance.PICO.XR.PXR_HandTracking.GetActiveInputDevice() == ByteDance.PICO.XR.ActiveInputDevice.HandTrackingActive) return;
            ByteDance.PICO.XR.PXR_Input.SendHapticImpulse(
                hand == 0 ? ByteDance.PICO.XR.PXR_Input.VibrateType.LeftController : ByteDance.PICO.XR.PXR_Input.VibrateType.RightController,
                Mathf.Clamp01(strength), Mathf.Clamp(ms, 0, 200));
#endif
        }
    }
}
