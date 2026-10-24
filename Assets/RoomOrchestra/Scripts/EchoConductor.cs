using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Runs the single master clock (4/4, 88 BPM) and the echo orbits. Every committed musical hit
    /// plants an echo; repeats are handed to the audio clock ahead of time with PlayScheduled, never
    /// played from Update at the moment of crossing, so timing does not depend on frame rate. Orbit
    /// visuals follow the same clock. Edits (remove, nudge, undo, clear) and pause cancel any repeat
    /// already scheduled and re-queue the rest, so nothing doubles or goes missing.
    /// </summary>
    public class EchoConductor : MonoBehaviour
    {
        [SerializeField] OrbitView[] orbits;
        [Tooltip("Delay (s) before bar 0 starts, so the first bar is not already under way.")]
        [SerializeField, Min(0f)] float startDelay = 0.2f;
        [Tooltip("Log one line per bar with scheduled counts (diagnostics).")]
        [SerializeField] bool logBars = true;

        MusicClock m_Clock;
        EchoSequencer m_Sequencer;
        long m_LastLoggedBar = long.MinValue;
        int m_ScheduledThisBar;
        double m_VisualDsp, m_LastSeenDsp;
        float m_LastSeenTime;

        public static EchoConductor Instance { get; private set; }

        public MusicClock Clock => m_Clock;
        public EchoSequencer Sequencer => m_Sequencer;
        public bool EditMode { get; private set; }
        public long SelectedEchoId { get; private set; }
        public bool IsPaused => m_Clock != null && m_Clock.IsPaused;
        public IReadOnlyList<OrbitView> Orbits => orbits;

        public event Action<EchoEvent> Planted;
        public event Action<MusicalHitEvent> Refused;
        public event Action<ScheduledEcho> Scheduled;

        void Awake()
        {
            Instance = this;
            m_Clock = new MusicClock(AudioSettings.dspTime + startDelay);
            m_Sequencer = new EchoSequencer(m_Clock);
            m_LastSeenDsp = m_VisualDsp = AudioSettings.dspTime;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable() => MusicalHitPipeline.Committed += OnHit;
        void OnDisable() => MusicalHitPipeline.Committed -= OnHit;

        /// <summary>Wires orbits when built from code (tests); scenes set them in the inspector.</summary>
        public void Configure(OrbitView[] views) => orbits = views;

        void OnHit(MusicalHitEvent hit)
        {
            if (m_Sequencer.TryAdd(hit, out var echo) == EchoAddResult.Added)
            {
                Debug.Log($"[RoomOrchestra] Echo planted {echo} ({m_Sequencer.CountOn(echo.Target)}/{EchoSequencer.MaxPerTarget})");
                Planted?.Invoke(echo);
            }
            else
            {
                Debug.Log($"[RoomOrchestra] {hit.Target} orbit is full ({EchoSequencer.MaxPerTarget}); hit played live only");
                Refused?.Invoke(hit);
            }
        }

        void Update()
        {
            double now = AudioSettings.dspTime;
            foreach (var due in m_Sequencer.Collect(now))
            {
                var target = TargetFor(due.Echo.Target);
                if (target != null) target.PlayEcho(due.Echo, due.DspTime);
                m_ScheduledThisBar++;
                Scheduled?.Invoke(due);
            }
            LogBar(now);
            SyncVisuals(now);
        }

        // The audio clock can advance in coarse jumps (notably on the emulator); between jumps the
        // visuals extrapolate with real time so the minis glide instead of stepping.
        void SyncVisuals(double dspNow)
        {
            if (dspNow != m_LastSeenDsp)
            {
                m_LastSeenDsp = dspNow;
                m_LastSeenTime = Time.unscaledTime;
            }
            double extrapolated = m_LastSeenDsp + Math.Min(0.1, Time.unscaledTime - m_LastSeenTime);
            m_VisualDsp = Math.Max(m_VisualDsp, extrapolated); // never run backwards
            double phase = m_Clock.BarPhase(m_VisualDsp);
            if (orbits == null) return;
            foreach (var orbit in orbits)
                if (orbit != null) orbit.Sync(m_Sequencer.Echoes, phase, EditMode ? SelectedEchoId : 0);
        }

        void LogBar(double now)
        {
            long bar = m_Clock.BarIndex(now);
            if (bar == m_LastLoggedBar) return;
            if (logBars && m_LastLoggedBar != long.MinValue && m_Sequencer.Echoes.Count > 0)
                Debug.Log($"[RoomOrchestra] Bar {m_LastLoggedBar}: {m_Sequencer.Echoes.Count} echoes, {m_ScheduledThisBar} repeats scheduled, {m_Clock.Bpm:F0} BPM");
            m_LastLoggedBar = bar;
            m_ScheduledThisBar = 0;
        }

        InstrumentTarget TargetFor(InstrumentType type)
        {
            if (orbits != null)
                foreach (var orbit in orbits)
                    if (orbit != null && orbit.Target != null && orbit.Target.Instrument == type) return orbit.Target;
            return InstrumentTarget.Find(type);
        }

        // Cancel what was already handed to audio, then re-queue from now: used after every edit.
        void Resync()
        {
            double now = AudioSettings.dspTime;
            foreach (var target in InstrumentTarget.Active) target.CancelScheduled();
            m_Sequencer.Rewind(now);
        }

        // ---- Editing (all discrete, one press each) ----

        public void ToggleEditMode(InstrumentTarget focused)
        {
            EditMode = !EditMode;
            if (EditMode && Find(SelectedEchoId) == null) SelectNext(+1, focused);
            Debug.Log($"[RoomOrchestra] Echo edit mode {(EditMode ? "ON" : "OFF")}");
        }

        public void Select(long echoId)
        {
            if (Find(echoId) == null) return;
            SelectedEchoId = echoId;
            EditMode = true;
        }

        /// <summary>Selects the next/previous echo (by step) on the focused target, or on any target.</summary>
        public void SelectNext(int direction, InstrumentTarget focused = null)
        {
            var list = new List<EchoEvent>();
            foreach (var e in m_Sequencer.Echoes)
                if (focused == null || e.Target == focused.Instrument) list.Add(e);
            if (list.Count == 0 && focused != null) list.AddRange(m_Sequencer.Echoes);
            if (list.Count == 0) { SelectedEchoId = 0; return; }
            list.Sort((a, b) => a.Target != b.Target ? a.Target.CompareTo(b.Target) : a.Step.CompareTo(b.Step));
            int index = list.FindIndex(e => e.StableId == SelectedEchoId);
            index = index < 0 ? 0 : ((index + Math.Sign(direction)) % list.Count + list.Count) % list.Count;
            SelectedEchoId = list[index].StableId;
        }

        public bool RemoveSelected()
        {
            var echo = Find(SelectedEchoId);
            if (echo == null || !m_Sequencer.Remove(echo.StableId)) return false;
            TargetFor(echo.Target)?.CancelScheduled(echo.StableId);
            Debug.Log($"[RoomOrchestra] Removed {echo}");
            SelectNext(+1, TargetFor(echo.Target));
            return true;
        }

        public bool Nudge(int direction)
        {
            if (!m_Sequencer.Nudge(SelectedEchoId, direction, AudioSettings.dspTime)) return false;
            Resync();
            Debug.Log($"[RoomOrchestra] Nudged {Find(SelectedEchoId)}");
            return true;
        }

        public bool Undo()
        {
            if (!m_Sequencer.Undo(AudioSettings.dspTime)) return false;
            Resync();
            if (Find(SelectedEchoId) == null) SelectedEchoId = 0;
            Debug.Log($"[RoomOrchestra] Undo ({m_Sequencer.Echoes.Count} echoes)");
            return true;
        }

        public void Clear()
        {
            m_Sequencer.Clear();
            Resync();
            SelectedEchoId = 0;
            Debug.Log("[RoomOrchestra] Echoes cleared (undo restores them)");
        }

        public void TogglePause()
        {
            double now = AudioSettings.dspTime;
            if (m_Clock.IsPaused)
            {
                m_Clock.Resume(now);
                m_Sequencer.Rewind(now);
            }
            else
            {
                m_Clock.Pause(now);
                foreach (var target in InstrumentTarget.Active) target.StopAll(); // silence at once
            }
            Debug.Log($"[RoomOrchestra] Music {(m_Clock.IsPaused ? "paused" : "resumed")}");
        }

        /// <summary>Controller/keyboard commands while edit mode is on.</summary>
        public void ExecuteEdit(ControllerCommand command)
        {
            var focused = OrbSelector.Instance != null ? OrbSelector.Instance.FocusedTarget : null;
            switch (command)
            {
                case ControllerCommand.NextOrb: Nudge(+1); break;
                case ControllerCommand.PreviousOrb: Nudge(-1); break;
                case ControllerCommand.NextTarget: SelectNext(+1, focused); break;
                case ControllerCommand.PreviousTarget: SelectNext(-1, focused); break;
                case ControllerCommand.Place: RemoveSelected(); break;
                case ControllerCommand.CycleIntensity: Undo(); break;
                case ControllerCommand.CyclePetal: ToggleEditMode(focused); break;
            }
        }

        /// <summary>Selects the echo shown by a tapped mini.</summary>
        public bool SelectMini(EchoMini mini)
        {
            if (mini == null || mini.Orbit == null) return false;
            long id = mini.Orbit.EchoFor(mini.transform);
            if (id == 0) return false;
            Select(id);
            return true;
        }

        EchoEvent Find(long id) => id == 0 ? null : m_Sequencer.Find(id);
    }
}
