using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Loop recorder. While recording, every live hit is quantized to the step grid and
    /// replays each cycle on the audio clock, so instruments layer into a song.
    /// </summary>
    public class LoopStation : MonoBehaviour
    {
        const int k_BeatsPerBar = 4;

        class LoopEvent
        {
            public InstrumentTarget target;
            public int note;
            public int step;
            public float strength;
            public long lastScheduledCycle;
        }

        [SerializeField, Range(40f, 200f)] float bpm = 90f;
        [SerializeField, Min(1)] int bars = 2;
        [Tooltip("Grid resolution: 2 = eighth notes, 4 = sixteenth notes.")]
        [SerializeField, Min(1)] int stepsPerBeat = 2;
        [SerializeField, Min(1)] int maxEvents = 64;
        [Tooltip("How far ahead (seconds) notes are handed to the audio clock. The emulator's audio " +
                 "clock advances in coarse jumps, so this must be generous; scheduling stays sample-accurate.")]
        [SerializeField, Min(0.02f)] float lookahead = 0.5f;
        [Tooltip("Notes found up to this late (seconds) still play immediately instead of being skipped.")]
        [SerializeField, Min(0f)] float lateTolerance = 0.3f;
        [Tooltip("Log one line per loop cycle with scheduled/late/skipped counts.")]
        [SerializeField] bool logDiagnostics = true;

        readonly List<LoopEvent> m_Events = new();
        double m_StartDsp;
        bool m_Recording;
        long m_LastBeat = -1;
        long m_DiagCycle = -1;
        int m_DiagScheduled, m_DiagLate, m_DiagSkipped;

        public static LoopStation Instance { get; private set; }

        public bool IsRecording => m_Recording;
        public int EventCount => m_Events.Count;
        public int StepCount => bars * k_BeatsPerBar * stepsPerBeat;
        public double StepDuration => 60.0 / bpm / stepsPerBeat;
        public double LoopDuration => StepDuration * StepCount;

        public event Action<bool> RecordingChanged;
        public event Action Cleared;
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
            Debug.Log($"[RoomOrchestra] Loop recording {(recording ? "ON" : "OFF")} ({m_Events.Count} notes in loop)");
            RecordingChanged?.Invoke(recording);
        }

        public void ToggleRecording() => SetRecording(!m_Recording);

        public void Clear()
        {
            m_Events.Clear();
            Debug.Log("[RoomOrchestra] Loop cleared");
            Cleared?.Invoke();
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

            // One note per instrument per step: re-hitting a slot replaces it.
            var ev = m_Events.Find(e => e.target == target && e.step == step);
            if (ev == null)
            {
                if (m_Events.Count >= maxEvents) return;
                ev = new LoopEvent { target = target, step = step };
                m_Events.Add(ev);
            }
            ev.note = note;
            ev.strength = strength;
            ev.lastScheduledCycle = cycle; // this occurrence was just heard live
            Debug.Log($"[RoomOrchestra] Loop recorded {target.Instrument} note={note} step={step}/{StepCount}");
        }

        void Update()
        {
            double now = AudioSettings.dspTime;
            double horizon = now + lookahead;

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
                        ev.target.PlayNoteAt(ev.note, now, ev.strength, InstrumentTarget.HitSource.Loop);
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
                    ev.target.PlayNoteAt(ev.note, t, ev.strength, InstrumentTarget.HitSource.Loop);
                    ev.lastScheduledCycle = c;
                    m_DiagScheduled++;
                }
            }

            long cycleNow = (long)Math.Floor((now - m_StartDsp) / LoopDuration);
            if (cycleNow != m_DiagCycle)
            {
                if (logDiagnostics && m_DiagCycle >= 0 && m_Events.Count > 0)
                    Debug.Log($"[RoomOrchestra] Loop cycle {m_DiagCycle}: events={m_Events.Count} scheduled={m_DiagScheduled} late={m_DiagLate} skipped={m_DiagSkipped}");
                m_DiagCycle = cycleNow;
                m_DiagScheduled = m_DiagLate = m_DiagSkipped = 0;
            }

            long beat = (long)Math.Floor((now - m_StartDsp) / (StepDuration * stepsPerBeat));
            if (beat != m_LastBeat && beat >= 0)
            {
                m_LastBeat = beat;
                Beat?.Invoke((int)(beat % (bars * k_BeatsPerBar)));
            }
        }

        double TimeOf(long cycle, int step) => m_StartDsp + cycle * LoopDuration + step * StepDuration;
    }
}
