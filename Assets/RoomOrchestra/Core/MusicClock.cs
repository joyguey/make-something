using System;

namespace RoomOrchestra
{
    /// <summary>
    /// The single master clock: 4/4, sixteen steps per bar, driven only by the audio DSP clock.
    /// Every time is computed from (bar, step) indices, never accumulated, so it cannot drift.
    /// Adding echoes never touches tempo; only an explicit tempo change could (not in this phase).
    /// </summary>
    public sealed class MusicClock
    {
        public const int StepsPerBeat = 4;
        public const int BeatsPerBar = 4;
        public const int StepsPerBar = StepsPerBeat * BeatsPerBar;
        public const double DefaultBpm = 88.0;

        double m_PausedAt;

        public double Bpm { get; }
        /// <summary>DSP time of bar 0, step 0 (shifted forward by pauses).</summary>
        public double StartDsp { get; private set; }
        public bool IsPaused { get; private set; }

        public double StepDuration => 60.0 / Bpm / StepsPerBeat;
        public double BeatDuration => 60.0 / Bpm;
        public double BarDuration => StepDuration * StepsPerBar;

        public MusicClock(double startDsp, double bpm = DefaultBpm)
        {
            if (bpm <= 0) throw new ArgumentOutOfRangeException(nameof(bpm));
            StartDsp = startDsp;
            Bpm = bpm;
        }

        /// <summary>Musical seconds since bar 0 (frozen while paused).</summary>
        public double Elapsed(double dsp) => (IsPaused ? m_PausedAt : dsp) - StartDsp;

        public long BarIndex(double dsp) => (long)Math.Floor(Elapsed(dsp) / BarDuration);

        /// <summary>0 at the start of a bar, approaching 1 at its end.</summary>
        public double BarPhase(double dsp)
        {
            double bars = Elapsed(dsp) / BarDuration;
            return bars - Math.Floor(bars);
        }

        /// <summary>DSP time at which (bar, step) sounds.</summary>
        public double TimeOf(long bar, int step) => StartDsp + bar * BarDuration + step * StepDuration;

        /// <summary>The step nearest <paramref name="dsp"/> (the end of a bar rounds to step 0 of the next).</summary>
        public void Nearest(double dsp, out long bar, out int step)
        {
            long steps = (long)Math.Round(Elapsed(dsp) / StepDuration, MidpointRounding.AwayFromZero);
            bar = FloorDiv(steps, StepsPerBar);
            step = (int)(steps - bar * StepsPerBar);
        }

        /// <summary>First bar at which <paramref name="step"/> sounds strictly after <paramref name="dsp"/>.</summary>
        public long NextBarAfter(int step, double dsp)
        {
            // Works in musical time, so it stays correct while paused.
            double elapsed = Elapsed(dsp);
            long bar = (long)Math.Floor((elapsed - step * StepDuration) / BarDuration) + 1;
            // Guard against floating-point edges in either direction.
            while (MusicalTime(bar - 1, step) > elapsed) bar--;
            while (MusicalTime(bar, step) <= elapsed) bar++;
            return bar;
        }

        double MusicalTime(long bar, int step) => bar * BarDuration + step * StepDuration;

        public void Pause(double dsp)
        {
            if (IsPaused) return;
            IsPaused = true;
            m_PausedAt = dsp;
        }

        /// <summary>Continues from exactly the musical position where it paused.</summary>
        public void Resume(double dsp)
        {
            if (!IsPaused) return;
            StartDsp += dsp - m_PausedAt;
            IsPaused = false;
        }

        static long FloorDiv(long a, long b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    }
}
