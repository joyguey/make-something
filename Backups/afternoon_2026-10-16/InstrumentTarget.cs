using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// A musical object: plays the next note of its instrument and flashes when activated.
    /// Taps, keys, balls, and the <see cref="LoopStation"/> all trigger notes through this component.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(ColorFlash))]
    public class InstrumentTarget : MonoBehaviour
    {
        public enum NoteOrder
        {
            PingPong,  // up the scale, then back down
            Ascending, // up the scale, then wrap to the first note
            Random     // any note except the one just played
        }

        public enum HitSource
        {
            Tap,
            Key,
            Ball,
            Loop
        }

        static readonly List<InstrumentTarget> s_Active = new();

        [SerializeField] InstrumentType instrument;
        [Tooltip("One clip per scale degree, lowest first (D, E, F#, A, B of D major pentatonic).")]
        [SerializeField] AudioClip[] notes;
        [SerializeField] NoteOrder order = NoteOrder.PingPong;
        [Tooltip("Template voice; extra voices copy its settings so notes can overlap and be scheduled.")]
        [SerializeField] AudioSource audioSource;
        [SerializeField, Range(0f, 1f)] float volume = 0.7f;
        [SerializeField, Min(1)] int voiceCount = 8;
        [Tooltip("Where aim assist lands thrown balls (e.g. table top, chair seat, lamp shade).")]
        [SerializeField] Transform aimPoint;

        ColorFlash m_Flash;
        AudioSource[] m_Voices;
        int m_NextVoice;
        int m_Step;
        readonly List<(double time, AudioSource voice, Color? color)> m_Pending = new();

        /// <summary>Scheduled notes whose voice was confirmed playing / not playing just after their start time.</summary>
        public static int ScheduledStarted, ScheduledNotStarted;

        /// <summary>All enabled instrument targets in the scene.</summary>
        public static IReadOnlyList<InstrumentTarget> Active => s_Active;

        /// <summary>Raised for every note from any instrument: target, note index, strength (0-1), source.</summary>
        public static event Action<InstrumentTarget, int, float, HitSource> AnyPlayed;

        public InstrumentType Instrument => instrument;
        public IReadOnlyList<AudioClip> Notes => notes;
        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position;
        public int LastNoteIndex { get; private set; } = -1;
        public int PlayCount { get; private set; }

        public bool IsSounding
        {
            get
            {
                if (m_Voices == null) return false;
                foreach (var v in m_Voices)
                    if (v.isPlaying) return true;
                return false;
            }
        }

        void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            m_Flash = GetComponent<ColorFlash>();

            m_Voices = new AudioSource[voiceCount];
            m_Voices[0] = audioSource;
            for (int i = 1; i < voiceCount; i++)
            {
                var v = gameObject.AddComponent<AudioSource>();
                v.playOnAwake = false;
                v.spatialBlend = audioSource.spatialBlend;
                v.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
                v.priority = audioSource.priority;
                m_Voices[i] = v;
            }
        }

        void OnEnable() => s_Active.Add(this);
        void OnDisable() => s_Active.Remove(this);

        void Update()
        {
            if (m_Pending.Count == 0) return;
            double now = AudioSettings.dspTime;
            for (int i = m_Pending.Count - 1; i >= 0; i--)
            {
                var (time, voice, color) = m_Pending[i];
                if (time > now) continue;
                if (color.HasValue) m_Flash.Flash(color.Value);
                else m_Flash.Flash();
                // Verify the scheduled voice really started (a reused voice would report the newer clip).
                if (voice.isPlaying) ScheduledStarted++;
                else ScheduledNotStarted++;
                m_Pending.RemoveAt(i);
            }
        }

        /// <summary>Plays the next note according to <see cref="order"/>, as a tap.</summary>
        public void PlayInstrument() => Play(HitSource.Tap);

        /// <summary>Plays the next note now and returns its index, or -1 if nothing played.</summary>
        public int Play(HitSource source, float strength = 1f)
        {
            if (notes == null || notes.Length == 0)
            {
                Debug.LogWarning($"[RoomOrchestra] {name} has no notes assigned.", this);
                return -1;
            }
            int index = NextIndex();
            PlayNoteAt(index, AudioSettings.dspTime, strength, source);
            return index;
        }

        /// <summary>Plays a specific scale degree (0 = lowest) now.</summary>
        public void PlayNote(int index) => PlayNoteAt(index, AudioSettings.dspTime, 1f, HitSource.Tap);

        /// <summary>
        /// Plays a scale degree at an audio-clock time; future times are sample-accurate.
        /// <paramref name="flashColor"/> overrides the flash (loop passes use their own color).
        /// </summary>
        public void PlayNoteAt(int index, double dspTime, float strength, HitSource source, Color? flashColor = null)
        {
            if (notes == null || index < 0 || index >= notes.Length || notes[index] == null || m_Voices == null)
                return;

            var voice = m_Voices[m_NextVoice];
            m_NextVoice = (m_NextVoice + 1) % m_Voices.Length; // oldest voice is reused when all are busy
            voice.clip = notes[index];
            voice.volume = volume * Mathf.Clamp01(strength);

            if (dspTime <= AudioSettings.dspTime + 0.005)
            {
                voice.Play();
                if (flashColor.HasValue) m_Flash.Flash(flashColor.Value);
                else m_Flash.Flash();
            }
            else
            {
                voice.PlayScheduled(dspTime);
                m_Pending.Add((dspTime, voice, flashColor));
            }

            LastNoteIndex = index;
            PlayCount++;
            if (source != HitSource.Loop)
                Debug.Log($"[RoomOrchestra] {source} hit {instrument} note={notes[index].name} strength={strength:F2}");
            AnyPlayed?.Invoke(this, index, strength, source);
        }

        int NextIndex()
        {
            int n = notes.Length;
            if (n == 1) return 0;
            switch (order)
            {
                case NoteOrder.Ascending:
                    return m_Step++ % n;
                case NoteOrder.Random:
                    int pick = UnityEngine.Random.Range(0, n - 1);
                    return pick >= LastNoteIndex && LastNoteIndex >= 0 ? pick + 1 : pick;
                default: // PingPong over 0..n-1..1
                    int period = 2 * (n - 1);
                    int s = m_Step++ % period;
                    return s < n ? s : period - s;
            }
        }

        public static InstrumentTarget Find(InstrumentType type)
        {
            foreach (var target in s_Active)
                if (target.instrument == type)
                    return target;
            return null;
        }
    }
}
