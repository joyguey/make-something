using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Layered loop recorder, like a live looper pedal. Each record session (record on -> off)
    /// becomes a <see cref="Pass"/> with its own color. Hits are quantized to the step grid and
    /// replay each cycle on the audio clock; passes can be muted or removed independently.
    /// </summary>
    public class LoopStation : MonoBehaviour
    {
        const int k_BeatsPerBar = 4;

        /// <summary>One recorded layer.</summary>
        public class Pass
        {
            public int id;
            public Color color;
            public bool muted;
            public int noteCount;
        }

        class LoopEvent
        {
            public InstrumentTarget target;
            public Pass pass;
            public int note;
            public int step;
            public float strength;
            public long lastScheduledCycle;
        }

        [SerializeField, Range(40f, 200f)] float bpm = 90f;
        [SerializeField, Min(1)] int bars = 2;
        [Tooltip("Grid resolution: 2 = eighth notes, 4 = sixteenth notes.")]
        [SerializeField, Min(1)] int stepsPerBeat = 2;
        [Tooltip("Layers kept at once; recording beyond this drops the oldest pass.")]
        [SerializeField, Range(1, 8)] int maxPasses = 8;
        [SerializeField, Min(1)] int maxEvents = 128;
        [SerializeField] Color[] passColors =
        {
            new Color(1f, 0.45f, 0.35f),  // coral
            new Color(1f, 0.75f, 0.2f),   // amber
            new Color(0.55f, 0.9f, 0.3f), // lime
            new Color(0.2f, 0.85f, 0.75f),// teal
            new Color(0.35f, 0.65f, 1f),  // sky
            new Color(0.65f, 0.45f, 1f),  // violet
            new Color(1f, 0.45f, 0.8f),   // pink
            new Color(0.92f, 0.92f, 0.88f)// pearl
        };
        [Tooltip("How far ahead (seconds) notes are handed to the audio clock. Must exceed the audio " +
                 "clock's update jumps and frame gaps; scheduled audio stays sample-accurate.")]
        [SerializeField, Min(0.02f)] float lookahead = 0.5f;
        [Tooltip("Notes found up to this late (seconds) still play immediately instead of being skipped.")]
        [SerializeField, Min(0f)] float lateTolerance = 0.3f;
        [Tooltip("Log one line per loop cycle with frame gaps and scheduled/late/skipped counts.")]
        [SerializeField] bool logDiagnostics = true;

        readonly List<LoopEvent> m_Events = new();
        readonly List<Pass> m_Passes = new();
        readonly List<(double time, Pass pass)> m_PendingPulses = new();
        Pass m_RecordingPass;
        int m_NextPassId = 1;
        int m_ColorCursor;
        double m_StartDsp;
        bool m_Recording;
        long m_LastBeat = -1;

        // Per-cycle diagnostics.
        long m_DiagCycle = -1;
        int m_DiagScheduled, m_DiagLate, m_DiagSkipped, m_DiagFrames;
        float m_DiagMaxGap;

        public static LoopStation Instance { get; private set; }

        public bool IsRecording => m_Recording;
        public int EventCount => m_Events.Count;
        public IReadOnlyList<Pass> Passes => m_Passes;
        public int MaxPasses => maxPasses;
        public int StepCount => bars * k_BeatsPerBar * stepsPerBeat;
        public int StepsPerBeat => stepsPerBeat;
        public double StepDuration => 60.0 / bpm / stepsPerBeat;
        public double LoopDuration => StepDuration * StepCount;

        /// <summary>Step currently under the playhead (0 .. StepCount-1).</summary>
        public int CurrentStep
        {
            get
            {
                double pos = (AudioSettings.dspTime - m_StartDsp) % LoopDuration;
                if (pos < 0) pos += LoopDuration;
                return Mathf.Clamp((int)(pos / StepDuration), 0, StepCount - 1);
            }
        }

        public event Action<bool> RecordingChanged;
        public event Action Cleared;
        /// <summary>Raised when passes are added, removed, muted or unmuted.</summary>
        public event Action PassesChanged;
        /// <summary>Raised when a pass plays a note (on the audio clock), for visual pulses.</summary>
        public event Action<Pass> PassPulsed;
        /// <summary>Raised on each beat with the beat index inside the loop (0-based).</summary>
        public event Action<int> Beat;

        void Awake()
        {
            Instance = this;
            m_StartDsp = AudioSettings.dspTime + 0.1;
        }

        void OnEnable() => InstrumentTarget.AnyPlayed += OnAnyPlayed;
        void OnDisable() => InstrumentTarget.AnyPlayed -= OnAnyPlayed;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetRecording(bool recording)
        {
            if (m_Recording == recording) return;
            m_Recording = recording;
            if (!recording && m_RecordingPass != null)
                Debug.Log($"[RoomOrchestra] Pass {m_RecordingPass.id} recorded: {m_RecordingPass.noteCount} notes");
            m_RecordingPass = null; // the next recorded hit starts a new pass
            Debug.Log($"[RoomOrchestra] Loop recording {(recording ? "ON" : "OFF")} ({m_Passes.Count} passes, {m_Events.Count} notes)");
            RecordingChanged?.Invoke(recording);
        }

        public void ToggleRecording() => SetRecording(!m_Recording);

        public void SetMuted(Pass pass, bool muted)
        {
            if (pass == null || pass.muted == muted) return;
            pass.muted = muted;
            Debug.Log($"[RoomOrchestra] Pass {pass.id} {(muted ? "muted" : "unmuted")}");
            PassesChanged?.Invoke();
        }

        public void ToggleMute(Pass pass)
        {
            if (pass != null) SetMuted(pass, !pass.muted);
        }

        public void RemovePass(Pass pass)
        {
            if (pass == null || !m_Passes.Remove(pass)) return;
            m_Events.RemoveAll(e => e.pass == pass);
            if (m_RecordingPass == pass) m_RecordingPass = null;
            Debug.Log($"[RoomOrchestra] Pass {pass.id} removed ({m_Passes.Count} passes, {m_Events.Count} notes left)");
            PassesChanged?.Invoke();
        }

        public void Clear()
        {
            m_Events.Clear();
            m_Passes.Clear();
            m_PendingPulses.Clear();
            m_RecordingPass = null;
            Debug.Log("[RoomOrchestra] Loop cleared");
            Cleared?.Invoke();
            PassesChanged?.Invoke();
        }

        /// <summary>True if any audible (unmuted) pass has a note on <paramref name="step"/>.</summary>
        public bool StepHasNotes(int step)
        {
            foreach (var e in m_Events)
                if (e.step == step && !e.pass.muted) return true;
            return false;
        }

        void OnAnyPlayed(InstrumentTarget target, int note, float strength, InstrumentTarget.HitSource source)
        {
            if (!m_Recording || source == InstrumentTarget.HitSource.Loop)
                return;

            double sinceStart = AudioSettings.dspTime - m_StartDsp;
            long cycle = (long)Math.Floor(sinceStart / LoopDuration);
            int step = (int)Math.Round((sinceStart - cycle * LoopDuration) / StepDuration);
            if (step >= StepCount)
            {
                step -= StepCount;
                cycle++;
            }

            if (m_RecordingPass == null)
            {
                if (m_Passes.Count >= maxPasses)
                {
                    Debug.Log($"[RoomOrchestra] Pass limit {maxPasses} reached; dropping oldest pass");
                    RemovePass(m_Passes[0]);
                }
                m_RecordingPass = new Pass { id = m_NextPassId++, color = passColors[m_ColorCursor++ % passColors.Length] };
                m_Passes.Add(m_RecordingPass);
                PassesChanged?.Invoke();
            }

            // Within a pass, re-hitting the same instrument on the same step replaces that note.
            // Different passes stack, like overdub layers on a looper.
            var pass = m_RecordingPass;
            var ev = m_Events.Find(e => e.pass == pass && e.target == target && e.step == step);
            if (ev == null)
            {
                if (m_Events.Count >= maxEvents) return;
                ev = new LoopEvent { target = target, pass = pass, step = step };
                m_Events.Add(ev);
                pass.noteCount++;
            }
            ev.note = note;
            ev.strength = strength;
            ev.lastScheduledCycle = cycle; // this occurrence was just heard live
            Debug.Log($"[RoomOrchestra] Loop recorded pass={pass.id} {target.Instrument} note={note} step={step}/{StepCount}");
        }

        void Update()
        {
            double now = AudioSettings.dspTime;
            double horizon = now + lookahead;

            m_DiagFrames++;
            m_DiagMaxGap = Mathf.Max(m_DiagMaxGap, Time.unscaledDeltaTime);

            foreach (var ev in m_Events)
            {
                if (ev.target == null) continue;
                long c = ev.lastScheduledCycle + 1;
                double t = TimeOf(c, ev.step);
                if (t < now)
                {
                    if (now - t <= lateTolerance)
                    {
                        // Slightly late: play now rather than drop the note.
                        Emit(ev, now);
                        ev.lastScheduledCycle = c;
                        m_DiagLate++;
                        continue;
                    }
                    // Far behind (e.g. app was paused): skip to the next occurrence.
                    m_DiagSkipped++;
                    c = (long)Math.Ceiling((now - m_StartDsp - ev.step * StepDuration) / LoopDuration);
                    t = TimeOf(c, ev.step);
                }
                if (t <= horizon)
                {
                    Emit(ev, t); // muted passes advance silently, so unmuting resumes in time
                    ev.lastScheduledCycle = c;
                    m_DiagScheduled++;
                }
            }

            for (int i = m_PendingPulses.Count - 1; i >= 0; i--)
            {
                if (m_PendingPulses[i].time > now) continue;
                var pass = m_PendingPulses[i].pass;
                m_PendingPulses.RemoveAt(i);
                if (m_Passes.Contains(pass) && !pass.muted)
                    PassPulsed?.Invoke(pass);
            }

            long cycleNow = (long)Math.Floor((now - m_StartDsp) / LoopDuration);
            if (cycleNow != m_DiagCycle)
            {
                if (logDiagnostics && m_DiagCycle >= 0 && m_Events.Count > 0)
                    Debug.Log($"[RoomOrchestra] Loop cycle {m_DiagCycle}: passes={m_Passes.Count} events={m_Events.Count} scheduled={m_DiagScheduled} late={m_DiagLate} skipped={m_DiagSkipped} frames={m_DiagFrames} maxFrameGap={m_DiagMaxGap * 1000f:F0}ms voicesStarted={InstrumentTarget.ScheduledStarted} voicesNotStarted={InstrumentTarget.ScheduledNotStarted}");
                m_DiagCycle = cycleNow;
                m_DiagScheduled = m_DiagLate = m_DiagSkipped = m_DiagFrames = 0;
                m_DiagMaxGap = 0f;
            }

            long beat = (long)Math.Floor((now - m_StartDsp) / (StepDuration * stepsPerBeat));
            if (beat != m_LastBeat && beat >= 0)
            {
                m_LastBeat = beat;
                Beat?.Invoke((int)(beat % (bars * k_BeatsPerBar)));
            }
        }

        void Emit(LoopEvent ev, double time)
        {
            if (ev.pass.muted) return;
            ev.target.PlayNoteAt(ev.note, time, ev.strength, InstrumentTarget.HitSource.Loop, ev.pass.color);
            m_PendingPulses.Add((time, ev.pass));
        }

        double TimeOf(long cycle, int step) => m_StartDsp + cycle * LoopDuration + step * StepDuration;
    }
}
