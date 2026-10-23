using System;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The one path from any input (toss, tap-to-place, controller button, editor key) to sound.
    /// Deduplicates, stamps a <see cref="MusicalHitEvent"/>, plays it, then announces it.
    /// </summary>
    public static class MusicalHitPipeline
    {
        static readonly MusicalHitFactory s_Factory = new();

        public static IntensityMapping Intensity { get; } = new();

        /// <summary>Raised once per accepted musical event, after its sound was triggered.</summary>
        public static event Action<MusicalHitEvent> Committed;

        public static long EventCount => s_Factory.EventCount;

        /// <summary>Allocates an id for one intended action; all contacts of that action share it.</summary>
        public static long BeginAction() => s_Factory.BeginAction();

        public static bool HasFired(long actionId) => s_Factory.Deduplicator.HasFired(actionId);

        /// <summary>False if <paramref name="key"/> repeated within the debounce window (double-reported taps).</summary>
        public static bool TryAcceptRepeat(string key, double window) =>
            s_Factory.Deduplicator.TryAcceptRepeat(key, Time.unscaledTimeAsDouble, window);

        /// <summary>Plays and announces a hit, or returns null if this action already produced one.</summary>
        public static MusicalHitEvent Submit(long actionId, InstrumentTarget target, OrbType orb,
            IntensityTier tier, PitchInput pitch, InputPath path)
        {
            if (target == null) return null;
            var hit = s_Factory.TryCreate(actionId, target.Instrument, orb, tier, pitch, path, AudioSettings.dspTime);
            if (hit == null) return null;

            target.PlayHit(hit);
            Debug.Log($"[RoomOrchestra] Musical hit {hit}");
            Committed?.Invoke(hit);
            return hit;
        }

        /// <summary>Clears ids and dedup history (play mode start and tests).</summary>
        public static void Reset()
        {
            s_Factory.Reset();
            Intensity.Sensitivity = 1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Reset();
            Committed = null;
        }
    }
}
