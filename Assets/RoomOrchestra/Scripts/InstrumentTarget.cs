using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// A musical object (table/udu, chair/mbira, lamp/erhu). Plays exactly the note a
    /// <see cref="MusicalHitEvent"/> asks for: note from height/petal, timbre from the orb, level from
    /// the intensity tier. Voices per orb are rendered once at Start by <see cref="VoiceDesign"/>.
    /// SOUND PROTOTYPE: built from placeholder samples of unverified provenance (docs/AUDIO_ASSET_LICENSES.md).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(ColorFlash))]
    public class InstrumentTarget : MonoBehaviour
    {
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
        [Tooltip("Template voice; extra voices copy its settings so notes can overlap and be scheduled.")]
        [SerializeField] AudioSource audioSource;
        [SerializeField, Range(0f, 1f)] float volume = 0.7f;
        [SerializeField, Min(1)] int voiceCount = 8;
        [Tooltip("Where aim assist lands thrown balls (e.g. table top, chair seat, lamp shade).")]
        [SerializeField] Transform aimPoint;

        ColorFlash m_Flash;
        AudioSource[] m_Voices;
        double[] m_VoiceStart; // dsp time each voice starts (future = scheduled, not yet sounding)
        long[] m_VoiceTag;     // echo id that scheduled the voice (0 = live hit)
        float[] m_VoiceStrength; // so a mix change can re-level voices already playing
        AudioClip[,] m_Bank; // [orb, note index] rendered voices
        readonly List<double> m_PendingFlashes = new();

        /// <summary>All enabled instrument targets in the scene.</summary>
        public static IReadOnlyList<InstrumentTarget> Active => s_Active;

        /// <summary>Raised for every note from any instrument: target, note index, strength (0-1), source.</summary>
        public static event Action<InstrumentTarget, int, float, HitSource> AnyPlayed;

        public InstrumentType Instrument => instrument;
        public IReadOnlyList<AudioClip> Notes => notes;
        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position;
        public int LastNoteIndex { get; private set; } = -1;
        public int PlayCount { get; private set; }
        /// <summary>Player's mix level for this instrument (1 = full, 0 = muted).</summary>
        public float MixGain { get; private set; } = 1f;

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
            m_VoiceStart = new double[voiceCount];
            m_VoiceTag = new long[voiceCount];
            m_VoiceStrength = new float[voiceCount];
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

        /// <summary>Wires an instrument and its notes when built from code (tests).</summary>
        public void Configure(InstrumentType type, AudioClip[] clips)
        {
            instrument = type;
            notes = clips;
        }

        /// <summary>Plays another instrument from now on (an instrument's place changed): its sound follows (SetSound).</summary>
        public void SetInstrument(InstrumentType type)
        {
            if (instrument == type) return;
            StopAll();
            instrument = type;
        }

        /// <summary>A sound pack's notes and volume for this instrument; the per-character voices are rendered again.</summary>
        public void SetSound(AudioClip[] clips, float packVolume)
        {
            if (clips == null || clips.Length < NoteMapping.Count) return;
            bool same = notes != null && notes.Length == clips.Length && System.Linq.Enumerable.SequenceEqual(notes, clips)
                        && Mathf.Approximately(volume, packVolume) && m_Bank != null && m_BankFor == instrument;
            if (same) return;
            StopAll();
            notes = clips;
            volume = packVolume;
            BuildVoiceBank();
        }

        InstrumentType m_BankFor;

        /// <summary>True once per-orb voices are rendered; until then hits fall back to the plain samples.</summary>
        public bool HasVoiceBank => m_Bank != null;

        // Rendered in Start, not Awake, and only audio data: nothing visible changes at startup.
        void Start() => BuildVoiceBank();

        public void BuildVoiceBank()
        {
            if (notes == null || notes.Length < NoteMapping.Count) return;
            var bank = new AudioClip[OrbCatalog.Count, NoteMapping.Count];
            for (int index = 0; index < NoteMapping.Count; index++)
            {
                bool udu = instrument == InstrumentType.Udu;
                var articulation = (UduArticulation)index;
                int sourceIndex = udu ? VoiceDesign.UduSourceIndex(articulation) : index;
                var source = notes[sourceIndex];
                float fundamental = NoteMapping.FrequencyHz(instrument, sourceIndex);
                bool bowed = Instruments.Sustained(instrument); // the erhu's bow, the ocarina's breath
                float[] data = ReadMono(source);
                if (data == null) continue;
                var basis = udu ? VoiceDesign.ForUdu(articulation) : VoiceSpec.Plain;
                foreach (var profile in OrbCatalog.All)
                {
                    float[] rendered = VoiceDesign.Render(data, source.frequency, VoiceDesign.ForOrb(profile.Type, basis, fundamental, bowed, ringingMetal: instrument == InstrumentType.Handpan));
                    if (rendered.Length == 0) continue; // falls back to the plain sample in VoiceFor
                    var clip = AudioClip.Create($"{instrument}_{index}_{profile.Type}", rendered.Length, 1, source.frequency, false);
                    clip.SetData(rendered, 0);
                    bank[(int)profile.Type, index] = clip;
                }
            }
            m_Bank = bank;
            m_BankFor = instrument;
            int built = 0;
            foreach (var clip in bank) if (clip != null) built++;
            Debug.Log($"[RoomOrchestra] {instrument} voice bank: {built} voices (sound prototype)");
        }

        static float[] ReadMono(AudioClip clip)
        {
            if (clip == null) return null;
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            var raw = new float[clip.samples * clip.channels];
            if (!clip.GetData(raw, 0)) return null;
            if (clip.channels == 1) return raw;
            var mono = new float[clip.samples];
            for (int i = 0; i < mono.Length; i++)
            {
                float sum = 0f;
                for (int c = 0; c < clip.channels; c++) sum += raw[i * clip.channels + c];
                mono[i] = sum / clip.channels;
            }
            return mono;
        }

        /// <summary>The voice for an orb and note index (rendered if available, else the plain sample).</summary>
        public AudioClip VoiceFor(OrbType orb, int index)
        {
            index = Mathf.Clamp(index, 0, NoteMapping.Count - 1);
            if (m_Bank != null && m_Bank[(int)orb, index] != null) return m_Bank[(int)orb, index];
            if (notes == null || notes.Length == 0) return null;
            int source = instrument == InstrumentType.Udu ? VoiceDesign.UduSourceIndex((UduArticulation)index) : index;
            return notes[Mathf.Min(source, notes.Length - 1)];
        }

        void OnEnable() => s_Active.Add(this);
        void OnDisable() => s_Active.Remove(this);

        void Update()
        {
            if (m_PendingFlashes.Count == 0) return;
            double now = AudioSettings.dspTime;
            for (int i = m_PendingFlashes.Count - 1; i >= 0; i--)
            {
                if (m_PendingFlashes[i] <= now)
                {
                    m_Flash.Flash();
                    m_PendingFlashes.RemoveAt(i);
                }
            }
        }

        /// <summary>Plays the middle note (no selected orb), as a tap.</summary>
        public void PlayInstrument() => Play(HitSource.Tap);

        /// <summary>Sounds a musical hit now: its note, its orb's timbre, its tier's level.</summary>
        public int PlayHit(MusicalHitEvent hit)
        {
            int index = hit.Note.Index;
            PlayClipAt(VoiceFor(hit.Orb, index), index, AudioSettings.dspTime, IntensityMapping.Strength(hit.Tier),
                hit.Path == InputPath.Toss ? HitSource.Ball : HitSource.Tap);
            return index;
        }

        /// <summary>Schedules one echo repeat on the audio clock (sample-accurate, independent of frame rate).</summary>
        public void PlayEcho(EchoEvent echo, double dspTime) =>
            PlayClipAt(VoiceFor(echo.Orb, echo.NoteIndex), echo.NoteIndex, dspTime, IntensityMapping.Strength(echo.Tier),
                HitSource.Loop, echo.StableId);

        /// <summary>
        /// Stops voices scheduled for the future: those of one echo (<paramref name="tag"/> &gt; 0) or all of
        /// them (tag 0). Used by remove, nudge, undo and pause so no stale repeat sounds afterwards.
        /// </summary>
        public int CancelScheduled(long tag = 0)
        {
            if (m_Voices == null) return 0;
            double now = AudioSettings.dspTime;
            int cancelled = 0;
            for (int i = 0; i < m_Voices.Length; i++)
            {
                if (m_VoiceStart[i] <= now || (tag != 0 && m_VoiceTag[i] != tag)) continue;
                m_Voices[i].Stop();
                m_PendingFlashes.Remove(m_VoiceStart[i]);
                m_VoiceStart[i] = 0;
                m_VoiceTag[i] = 0;
                cancelled++;
            }
            return cancelled;
        }

        /// <summary>Number of voices waiting on the audio clock (scheduled, not yet sounding).</summary>
        public int ScheduledCount
        {
            get
            {
                if (m_Voices == null) return 0;
                double now = AudioSettings.dspTime;
                int n = 0;
                foreach (double start in m_VoiceStart) if (start > now) n++;
                return n;
            }
        }

        /// <summary>Sets this instrument's mix level; voices already sounding or scheduled follow at once.</summary>
        public void SetMixGain(float gain)
        {
            MixGain = Mathf.Clamp01(gain);
            if (m_Voices == null) return;
            for (int i = 0; i < m_Voices.Length; i++)
                m_Voices[i].volume = volume * m_VoiceStrength[i] * MixGain;
        }

        /// <summary>Stops every voice at once (mute / reset).</summary>
        public void StopAll()
        {
            if (m_Voices == null) return;
            for (int i = 0; i < m_Voices.Length; i++)
            {
                m_Voices[i].Stop();
                m_VoiceStart[i] = 0;
                m_VoiceTag[i] = 0;
            }
            m_PendingFlashes.Clear();
        }

        /// <summary>
        /// The real object this instrument lives on after the lesson (or a box around its own furniture when the player
        /// kept it there), or null: then the note comes from the height on its furniture, as in the lesson.
        /// </summary>
        public RealHome Home { get; set; }

        /// <summary>The instrument a collider belongs to: its furniture, or the real object it lives on.</summary>
        public static InstrumentTarget Of(Component c)
        {
            if (c == null) return null;
            var own = c.GetComponentInParent<InstrumentTarget>();
            if (own != null) return own;
            var home = RealHome.Of(c);
            return home != null ? home.Target : null;
        }

        /// <summary>The note at a point on it: along the real object's longest side, else the height on its furniture.</summary>
        public PitchInput PitchAt(Vector3 worldPoint) => Home != null ? Home.PitchAt(worldPoint) : PitchAtHeight(worldPoint.y);

        /// <summary>Normalized height (0 = bottom, 1 = top) of a world-space Y within this object's colliders.</summary>
        public PitchInput PitchAtHeight(float worldY)
        {
            var colliders = GetComponentsInChildren<Collider>();
            if (colliders.Length == 0) return PitchInput.FromHeight(0.5f);
            var bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++) bounds.Encapsulate(colliders[i].bounds);
            float height = bounds.size.y;
            return PitchInput.FromHeight(height > 1e-4f ? (worldY - bounds.min.y) / height : 0.5f);
        }

        /// <summary>Plays the next note now and returns its index, or -1 if nothing played.</summary>
        public int Play(HitSource source, float strength = 1f)
        {
            if (notes == null || notes.Length == 0)
            {
                Debug.LogWarning($"[RoomOrchestra] {name} has no notes assigned.", this);
                return -1;
            }
            const int middle = 2;
            PlayNoteAt(middle, AudioSettings.dspTime, strength, source);
            return middle;
        }

        /// <summary>Plays a specific scale degree (0 = lowest) now.</summary>
        public void PlayNote(int index) => PlayNoteAt(index, AudioSettings.dspTime, 1f, HitSource.Tap);

        /// <summary>Plays a scale degree at an audio-clock time; future times are sample-accurate.</summary>
        /// <summary>
        /// Plays a note that came from somewhere else (the other player's call in an Echo Battle) on this furniture at
        /// <paramref name="dspTime"/>, with the furniture's flash and its note band lit, so it is seen as well as heard.
        /// <paramref name="clips"/>: another instrument's notes when this room doesn't have that instrument (null: this
        /// instrument's own).
        /// </summary>
        public void PlayHeardAt(int index, double dspTime, float strength, AudioClip[] clips = null)
        {
            var from = clips != null && clips.Length > 0 ? clips : notes;
            if (from == null || from.Length == 0 || index < 0) return;
            PlayClipAt(from[Mathf.Min(index, from.Length - 1)], Mathf.Min(index, NoteMapping.Count - 1), dspTime, strength, HitSource.Tap);
        }

        public void PlayNoteAt(int index, double dspTime, float strength, HitSource source)
        {
            if (notes == null || index < 0 || index >= notes.Length) return;
            PlayClipAt(notes[index], index, dspTime, strength, source);
        }

        void PlayClipAt(AudioClip clip, int index, double dspTime, float strength, HitSource source, long tag = 0)
        {
            if (clip == null || m_Voices == null) return;

            int slot = PickVoice();
            var voice = m_Voices[slot];
            voice.clip = clip;
            m_VoiceStrength[slot] = Mathf.Clamp01(strength);
            voice.volume = volume * m_VoiceStrength[slot] * MixGain;
            bool now = dspTime <= AudioSettings.dspTime + 0.005;
            m_VoiceStart[slot] = now ? AudioSettings.dspTime : dspTime;
            m_VoiceTag[slot] = tag;
            // Echo repeats don't flash the whole object: the orbiting mini is their visual cue, which keeps
            // flashing calm when many echoes play (photosensitivity).
            bool flash = source != HitSource.Loop;

            if (now)
            {
                voice.Play();
                if (flash) m_Flash.Flash();
                if (flash && Home != null) Home.Pulse(index); // its band and note name light up
            }
            else
            {
                voice.PlayScheduled(dspTime);
                if (flash) m_PendingFlashes.Add(dspTime);
            }

            LastNoteIndex = index;
            PlayCount++;
            if (source != HitSource.Loop)
                Debug.Log($"[RoomOrchestra] {source} hit {instrument} voice={clip.name} strength={strength:F2}");
            AnyPlayed?.Invoke(this, index, strength, source);
        }

        // A free voice if any; otherwise the one that started longest ago. Never a voice still waiting
        // on the audio clock, so a scheduled repeat is not silently stolen.
        int PickVoice()
        {
            double now = AudioSettings.dspTime;
            int oldest = -1;
            for (int i = 0; i < m_Voices.Length; i++)
            {
                if (m_VoiceStart[i] > now) continue;
                if (!m_Voices[i].isPlaying) return i;
                if (oldest < 0 || m_VoiceStart[i] < m_VoiceStart[oldest]) oldest = i;
            }
            if (oldest >= 0) return oldest;
            int earliest = 0; // all pending: drop the furthest-future one last, reuse the soonest
            for (int i = 1; i < m_Voices.Length; i++) if (m_VoiceStart[i] < m_VoiceStart[earliest]) earliest = i;
            return earliest;
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
