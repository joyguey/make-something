using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Plays the interface sounds (<see cref="UiSoundSynth"/>) where they happen: a soft tick when the pointer comes
    /// onto something tappable, a pop on a press, two notes up or down as the menu opens or closes, a bright arpeggio
    /// for a confirmed choice, a falling pair for cancel, and a sparkle when an instrument moves onto real furniture.
    /// Made once per run (no scene object needed); a few voices, placed at the control, so a sound comes from it.
    /// </summary>
    public class UiSounds : MonoBehaviour
    {
        public const int Voices = 4;
        /// <summary>Hover ticks no closer together than this (s): sweeping across the leaf is a soft patter, not a buzz.</summary>
        public const float HoverGapSeconds = 0.06f;

        static UiSounds s_Instance;
        AudioClip[] m_Clips;
        AudioSource[] m_Voices;
        int m_Next;
        float m_LastHover = -1f;

        /// <summary>The last sound played (tests).</summary>
        public static UiSound? Last { get; private set; }
        public static int Played { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Instance = null;
            Last = null;
            Played = 0;
        }

        static UiSounds Get()
        {
            if (s_Instance != null) return s_Instance;
            var go = new GameObject("UiSounds");
            DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<UiSounds>();
            return s_Instance;
        }

        void Awake()
        {
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            var kinds = (UiSound[])System.Enum.GetValues(typeof(UiSound));
            m_Clips = new AudioClip[kinds.Length];
            foreach (var k in kinds)
            {
                var data = UiSoundSynth.Render(k, rate);
                var clip = AudioClip.Create($"Ui_{k}", data.Length, 1, rate, false);
                clip.SetData(data, 0);
                m_Clips[(int)k] = clip;
            }
            m_Voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var v = new GameObject($"UiVoice{i}").AddComponent<AudioSource>();
                v.transform.SetParent(transform, false);
                v.playOnAwake = false;
                v.spatialBlend = 0.6f; // from the control, but never lost behind the head
                v.minDistance = 0.5f;
                v.maxDistance = 6f;
                v.rolloffMode = AudioRolloffMode.Linear;
                v.dopplerLevel = 0f;
                m_Voices[i] = v;
            }
        }

        /// <summary>Plays <paramref name="sound"/> from <paramref name="at"/> (scene space).</summary>
        public static void Play(UiSound sound, Vector3 at)
        {
            var self = Get();
            if (sound == UiSound.Hover)
            {
                if (Time.unscaledTime - self.m_LastHover < HoverGapSeconds) return;
                self.m_LastHover = Time.unscaledTime;
            }
            var voice = self.m_Voices[self.m_Next];
            self.m_Next = (self.m_Next + 1) % Voices;
            voice.transform.position = at;
            voice.pitch = sound == UiSound.Press ? Random.Range(0.97f, 1.03f) : 1f; // a little life in repeated taps
            voice.PlayOneShot(self.m_Clips[(int)sound], UiSoundSynth.Level(sound));
            Last = sound;
            Played++;
        }

        public static void Play(UiSound sound, Component from) => Play(sound, from != null ? from.transform.position : Vector3.zero);
    }
}
