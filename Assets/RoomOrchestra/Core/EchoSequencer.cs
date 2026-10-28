using System;
using System.Collections.Generic;

namespace RoomOrchestra
{
    /// <summary>One repeating musical event on a target's orbit: the hit, frozen at a 16th-note step.</summary>
    public sealed class EchoEvent
    {
        public long StableId { get; }
        public InstrumentType Target { get; }
        public OrbType Orb { get; }
        /// <summary>Scale degree or udu articulation (0-4), from the original hit.</summary>
        public int NoteIndex { get; }
        public IntensityTier Tier { get; }
        public int Step { get; internal set; }
        public double CreatedAtDsp { get; }
        /// <summary>Next bar whose occurrence has not been handed to the audio clock yet.</summary>
        internal long NextBar;

        internal EchoEvent(long stableId, InstrumentType target, OrbType orb, int noteIndex, IntensityTier tier, int step, double createdAtDsp)
        {
            StableId = stableId;
            Target = target;
            Orb = orb;
            NoteIndex = noteIndex;
            Tier = tier;
            Step = step;
            CreatedAtDsp = createdAtDsp;
        }

        public override string ToString() => $"echo #{StableId} {Orb}->{Target} note {NoteIndex} step {Step}";
    }

    public enum EchoAddResult
    {
        Added,
        /// <summary>Six echoes already orbit this target; nothing was overwritten.</summary>
        Full
    }

    /// <summary>One echo occurrence to hand to the audio clock.</summary>
    public readonly struct ScheduledEcho
    {
        public EchoEvent Echo { get; }
        public double DspTime { get; }
        public long Bar { get; }

        public ScheduledEcho(EchoEvent echo, double dspTime, long bar)
        {
            Echo = echo;
            DspTime = dspTime;
            Bar = bar;
        }
    }

    /// <summary>
    /// Echo orbits for all targets on one <see cref="MusicClock"/>. A hit becomes an echo at the
    /// nearest 16th step; each echo then repeats once per bar at that step. More echoes make the bar
    /// denser; the tempo never changes. Six per target, collisions move to the nearest free step,
    /// and every edit (add, remove, nudge, clear) can be undone.
    /// </summary>
    public sealed class EchoSequencer
    {
        public const int MaxPerTarget = 6;

        readonly List<EchoEvent> m_Echoes = new();
        readonly Stack<Action<double>> m_Undo = new();
        readonly List<ScheduledEcho> m_Buffer = new();
        long m_NextId = 1;

        public MusicClock Clock { get; }
        /// <summary>How far ahead (s) occurrences are handed to the audio clock. Generous because the
        /// emulator's audio clock advances in coarse jumps; scheduling itself stays sample-accurate.</summary>
        public double Lookahead { get; set; } = 0.3;
        /// <summary>Occurrences found up to this late (s) still play at once; older ones are skipped, never burst.</summary>
        public double LateTolerance { get; set; } = 0.05;

        public IReadOnlyList<EchoEvent> Echoes => m_Echoes;
        public bool CanUndo => m_Undo.Count > 0;

        public EchoSequencer(MusicClock clock) => Clock = clock;

        public int CountOn(InstrumentType target)
        {
            int n = 0;
            foreach (var e in m_Echoes) if (e.Target == target) n++;
            return n;
        }

        public EchoEvent Find(long stableId) => m_Echoes.Find(e => e.StableId == stableId);

        /// <summary>
        /// Plants an echo for a hit that already sounded live at hit.CreatedAtDspTime. Its first
        /// repeat is the next occurrence of its step after the live note, so the hit is never doubled.
        /// </summary>
        public EchoAddResult TryAdd(MusicalHitEvent hit, out EchoEvent echo)
        {
            echo = null;
            if (hit == null) return EchoAddResult.Full;
            if (CountOn(hit.Target) >= MaxPerTarget) return EchoAddResult.Full;

            Clock.Nearest(hit.CreatedAtDspTime, out _, out int nearest);
            int step = NearestFreeStep(hit.Target, nearest, null);
            echo = new EchoEvent(m_NextId++, hit.Target, hit.Orb, hit.Note.Index, hit.Tier, step, hit.CreatedAtDspTime);
            echo.NextBar = Clock.NextBarAfter(step, hit.CreatedAtDspTime + Clock.StepDuration * 0.5);
            m_Echoes.Add(echo);

            var added = echo;
            m_Undo.Push(_ => m_Echoes.Remove(added));
            return EchoAddResult.Added;
        }

        /// <summary>Returns occurrences due by now + <see cref="Lookahead"/> (none while paused).</summary>
        public IReadOnlyList<ScheduledEcho> Collect(double now)
        {
            m_Buffer.Clear();
            if (Clock.IsPaused) return m_Buffer;
            double horizon = now + Lookahead;
            foreach (var echo in m_Echoes)
            {
                double t = Clock.TimeOf(echo.NextBar, echo.Step);
                if (t < now - LateTolerance)
                {
                    // Far behind (app paused by the system, long hitch): skip to the next occurrence.
                    echo.NextBar = Clock.NextBarAfter(echo.Step, now - LateTolerance);
                    t = Clock.TimeOf(echo.NextBar, echo.Step);
                }
                if (t <= horizon)
                {
                    m_Buffer.Add(new ScheduledEcho(echo, Math.Max(t, now), echo.NextBar));
                    echo.NextBar++;
                }
            }
            return m_Buffer;
        }

        /// <summary>Removes an echo. Callers cancel any of its occurrences already handed to audio.</summary>
        public bool Remove(long stableId)
        {
            var echo = Find(stableId);
            if (echo == null) return false;
            int index = m_Echoes.IndexOf(echo);
            m_Echoes.RemoveAt(index);
            m_Undo.Push(now => Reinsert(echo, Math.Min(index, m_Echoes.Count), now));
            return true;
        }

        /// <summary>Moves an echo one step (direction +1 / -1), skipping occupied steps; wraps around the bar.</summary>
        public bool Nudge(long stableId, int direction, double now)
        {
            var echo = Find(stableId);
            if (echo == null || direction == 0) return false;
            int from = echo.Step;
            int to = NextFreeStep(echo.Target, from, Math.Sign(direction), echo);
            if (to == from) return false;
            SetStep(echo, to, now);
            m_Undo.Push(n => SetStep(echo, from, n));
            return true;
        }

        public void Clear()
        {
            if (m_Echoes.Count == 0) return;
            var snapshot = new List<EchoEvent>(m_Echoes);
            m_Echoes.Clear();
            m_Undo.Push(now =>
            {
                m_Echoes.Clear();
                foreach (var e in snapshot) Reinsert(e, m_Echoes.Count, now);
            });
        }

        /// <summary>Reverts the most recent edit. Returns false when there is nothing to undo.</summary>
        public bool Undo(double now)
        {
            if (m_Undo.Count == 0) return false;
            m_Undo.Pop()(now);
            return true;
        }

        /// <summary>
        /// Re-queues every echo's next repeat after <paramref name="now"/>. Call after cancelling the
        /// voices already handed to audio (edit, undo, resume), so nothing is doubled or lost.
        /// </summary>
        public void Rewind(double now)
        {
            foreach (var echo in m_Echoes) echo.NextBar = Clock.NextBarAfter(echo.Step, now);
        }

        /// <summary>Clears echoes and undo history (a fresh start).</summary>
        public void Reset()
        {
            m_Echoes.Clear();
            m_Undo.Clear();
        }

        void Reinsert(EchoEvent echo, int index, double now)
        {
            echo.NextBar = Clock.NextBarAfter(echo.Step, now);
            m_Echoes.Insert(Math.Min(index, m_Echoes.Count), echo);
        }

        void SetStep(EchoEvent echo, int step, double now)
        {
            echo.Step = step;
            echo.NextBar = Clock.NextBarAfter(step, now);
        }

        bool Occupied(InstrumentType target, int step, EchoEvent except)
        {
            foreach (var e in m_Echoes)
                if (e != except && e.Target == target && e.Step == step) return true;
            return false;
        }

        // 0, +1, -1, +2, -2 ... around the bar.
        int NearestFreeStep(InstrumentType target, int step, EchoEvent except)
        {
            for (int k = 0; k <= MusicClock.StepsPerBar / 2; k++)
            {
                int up = Wrap(step + k);
                if (!Occupied(target, up, except)) return up;
                int down = Wrap(step - k);
                if (!Occupied(target, down, except)) return down;
            }
            return step;
        }

        int NextFreeStep(InstrumentType target, int step, int direction, EchoEvent except)
        {
            for (int k = 1; k < MusicClock.StepsPerBar; k++)
            {
                int candidate = Wrap(step + direction * k);
                if (!Occupied(target, candidate, except)) return candidate;
            }
            return step;
        }

        static int Wrap(int step) => ((step % MusicClock.StepsPerBar) + MusicClock.StepsPerBar) % MusicClock.StepsPerBar;
    }
}
