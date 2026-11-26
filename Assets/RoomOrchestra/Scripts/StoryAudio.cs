using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Story mode's music and sounds. The level's song runs on the DSP clock (88 BPM, 4/4, like the sandbox) and is
    /// played on the game's own instruments where it can (read-only: the voices of the instruments in the room), with
    /// a synthesised pulse otherwise. It grows with the level: the drum alone at first, the pluck from the second wave;
    /// every Discord beaten adds the next note of the melody, on the next half-beat, so beating them plays a tune. As
    /// Discord fills the room the music goes muffled and out of tune. Sandbox sound is untouched (its song is muted
    /// while story plays and comes back after).
    /// </summary>
    public class StoryAudio : MonoBehaviour
    {
        const int MusicVoices = 10, SfxVoices = 8;
        static readonly int[][] Chords = { new[] { 0, 2, 3, 2 }, new[] { 4, 3, 2, 3 }, new[] { 3, 2, 0, 2 }, new[] { 1, 2, 3, 4 } };
        static readonly int[] Melody = { 3, 4, 3, 2, 0, 2, 3, 4, 4, 3, 2, 1, 0, 1, 2, 3 };

        readonly AudioSource[] m_Music = new AudioSource[MusicVoices];
        readonly AudioSource[] m_Sfx = new AudioSource[SfxVoices];
        readonly Dictionary<StorySound, AudioClip> m_Clips = new();
        AudioLowPassFilter[] m_Filters;
        int m_NextMusic, m_NextSfx, m_MelodyStep;
        InstrumentTarget m_Drum, m_Pluck, m_Sing;
        double m_Start = -1, m_NextBeat;
        long m_Beat;
        float m_Sour, m_Repair = 1f;

        /// <summary>How mended the level's instrument is (0 cracked: faint and out of tune; 1 whole).</summary>
        public void SetRepair(float repair) => m_Repair = Mathf.Clamp01(repair);

        public double Bpm { get; private set; } = MusicClock.DefaultBpm;
        /// <summary>DSP time of beat 0 (negative before the song starts).</summary>
        public double Start => m_Start;
        public bool Playing => m_Start >= 0;
        /// <summary>0 drum only, 1 drum and pluck, 2 everything.</summary>
        public int Layers { get; set; }
        public bool Paused { get; set; }
        public static int Played { get; private set; }

        void Awake()
        {
            for (int i = 0; i < MusicVoices; i++) m_Music[i] = Voice($"Music{i}", 0f);
            for (int i = 0; i < SfxVoices; i++) m_Sfx[i] = Voice($"Sfx{i}", 0.7f);
            m_Filters = new AudioLowPassFilter[MusicVoices];
            for (int i = 0; i < MusicVoices; i++) { m_Filters[i] = m_Music[i].gameObject.AddComponent<AudioLowPassFilter>(); m_Filters[i].cutoffFrequency = 22000f; }
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            foreach (StorySound s in System.Enum.GetValues(typeof(StorySound)))
            {
                var data = StorySoundSynth.Render(s, rate);
                var clip = AudioClip.Create($"Story_{s}", data.Length, 1, rate, false);
                clip.SetData(data, 0);
                m_Clips[s] = clip;
            }
            FindInstruments();
        }

        AudioSource Voice(string name, float spatial)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var v = go.AddComponent<AudioSource>();
            v.playOnAwake = false; v.spatialBlend = spatial; v.dopplerLevel = 0f;
            v.minDistance = 0.6f; v.maxDistance = 8f; v.rolloffMode = AudioRolloffMode.Linear;
            return v;
        }

        /// <summary>Borrows the voices of the instruments in the room (only reads them).</summary>
        void FindInstruments()
        {
            foreach (var t in FindObjectsByType<InstrumentTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.VoiceFor(OrbType.Bop, 0) == null) continue;
                var type = t.Instrument;
                if (m_Drum == null && (type == InstrumentType.Udu || type == InstrumentType.SlitDrum)) m_Drum = t;
                else if (m_Pluck == null && (type == InstrumentType.Mbira || type == InstrumentType.Guzheng || type == InstrumentType.Balafon || type == InstrumentType.Handpan)) m_Pluck = t;
                else if (m_Sing == null && Instruments.Sustained(type)) m_Sing = t;
            }
            Debug.Log($"[RoomOrchestra] Story music: drum {(m_Drum != null ? m_Drum.Instrument.ToString() : "synth")}, pluck {(m_Pluck != null ? m_Pluck.Instrument.ToString() : "none")}, melody {(m_Sing != null ? m_Sing.Instrument.ToString() : (m_Pluck != null ? m_Pluck.Instrument.ToString() : "synth"))}");
        }

        public void Begin(double bpm)
        {
            Bpm = bpm;
            m_Start = AudioSettings.dspTime + 0.5;
            m_NextBeat = m_Start; m_Beat = 0; m_MelodyStep = 0; Layers = 0; Paused = false;
        }

        public void Stop() { m_Start = -1; foreach (var v in m_Music) v.Stop(); }

        /// <summary>0 clean, 1 full of Discord: the song detunes and goes muffled.</summary>
        public void SetSour(float amount)
        {
            m_Sour = Mathf.Clamp01(amount);
            float cutoff = Mathf.Lerp(22000f, 900f, m_Sour * m_Sour);
            foreach (var f in m_Filters) f.cutoffFrequency = cutoff;
        }

        void Update()
        {
            if (!Playing) return;
            double now = AudioSettings.dspTime;
            double beat = 60.0 / Bpm;
            if (Paused) { if (m_NextBeat < now + 0.3) m_NextBeat = now + 0.3; return; } // the beat waits
            while (m_NextBeat < now + 0.25)
            {
                ScheduleBeat(m_NextBeat, m_Beat);
                m_NextBeat += beat; m_Beat++;
            }
        }

        void ScheduleBeat(double at, long n)
        {
            int inBar = (int)(n % 4), bar = (int)(n / 4 % Chords.Length);
            // the drum: beats 1 and 3 (a deep note), a lighter one on 4
            // the cracked Udu: its beat starts faint and wobbly and comes back as the meadow is restored
            float drum = Mathf.Lerp(0.25f, 1f, m_Repair);
            float crack = (1f - m_Repair) * 0.06f;
            if (inBar == 0 || inBar == 2) Music(m_Drum != null ? m_Drum.VoiceFor(OrbType.Boom, 0) : m_Clips[StorySound.Beat], at, (m_Drum != null ? 0.55f : StorySoundSynth.Level(StorySound.Beat)) * drum, crack);
            else if (inBar == 3 && m_Drum != null) Music(m_Drum.VoiceFor(OrbType.Bop, 1), at, 0.3f * drum, crack);
            // the pluck: the chord, one note a beat
            if (Layers >= 1 && m_Pluck != null) Music(m_Pluck.VoiceFor(OrbType.Flutter, Chords[bar][inBar]), at, 0.32f);
        }

        void Music(AudioClip clip, double at, float volume, float crack = 0f)
        {
            if (clip == null) return;
            var v = m_Music[m_NextMusic]; m_NextMusic = (m_NextMusic + 1) % MusicVoices;
            v.clip = clip;
            v.volume = volume;
            v.pitch = 1f - m_Sour * 0.04f + Random.Range(-1f, 1f) * (m_Sour * 0.02f + crack); // out of tune as Discord fills the room, or cracked
            v.PlayScheduled(at);
            Played++;
        }

        /// <summary>The next note of the melody, on the next half-beat: a Discord turned back into music. On the
        /// friend's own voice (Flutter high and light, Boom low and heavy).</summary>
        public void MelodyNote(OrbType friend)
        {
            if (!Playing) return;
            double half = 30.0 / Bpm;
            double now = AudioSettings.dspTime + 0.02;
            double at = m_Start + System.Math.Ceiling((now - m_Start) / half) * half;
            int note = Melody[m_MelodyStep++ % Melody.Length];
            var voice = m_Sing != null ? m_Sing : m_Pluck;
            if (voice != null) Music(voice.VoiceFor(friend, note), at, 0.6f);
            else Music(m_Clips[StorySound.Chorus], at, 0.2f);
        }

        /// <summary>A sound effect from a place in the room.</summary>
        public void Play(StorySound s, Vector3 at, float volume = 1f)
        {
            var v = m_Sfx[m_NextSfx]; m_NextSfx = (m_NextSfx + 1) % SfxVoices;
            v.transform.position = at;
            v.pitch = s == StorySound.SqueakCall ? Random.Range(0.9f, 1.15f) : 1f;
            v.PlayOneShot(m_Clips[s], StorySoundSynth.Level(s) * volume);
            Played++;
        }

        /// <summary>DSP time now, and whether it is on the beat.</summary>
        public bool OnBeat() => Playing && BeatJudge.OnBeat(AudioSettings.dspTime, m_Start, Bpm);

        /// <summary>0..1 through the current beat (for pulsing visuals); 0 when not playing.</summary>
        public float BeatPhase()
        {
            if (!Playing) return 0f;
            double beat = 60.0 / Bpm;
            double p = (AudioSettings.dspTime - m_Start) / beat;
            return p < 0 ? 0f : (float)(p - System.Math.Floor(p));
        }
    }
}
