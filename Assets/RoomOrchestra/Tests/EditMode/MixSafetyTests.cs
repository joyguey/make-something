using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// Prompt 07 mix safety, measured offline on the scene's real voices (whichever sound set is active):
    /// the densest loop the game allows (6 echoes per instrument on back-to-back steps, plus a live note,
    /// every voice Strong, all three instruments at once) summed sample by sample at the instruments'
    /// volumes, with no distance attenuation (as if the ear were at every instrument). Measures, does
    /// not listen: the owner judges how it sounds.
    /// </summary>
    public class MixSafetyTests
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";

        [OneTimeSetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // The scene is saved in the opening's first frame; layout checks are about the finished layout.
            UnityEngine.Object.FindFirstObjectByType<IntroSequence>()?.Finish();
        }

        static float[] Voice(InstrumentTarget target, OrbType orb, int index)
        {
            bool udu = target.Instrument == InstrumentType.Udu;
            int sourceIndex = udu ? VoiceDesign.UduSourceIndex((UduArticulation)index) : index;
            var clip = target.Notes[sourceIndex];
            clip.LoadAudioData();
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            var basis = udu ? VoiceDesign.ForUdu((UduArticulation)index) : VoiceSpec.Plain;
            var spec = VoiceDesign.ForOrb(orb, basis, NoteMapping.FrequencyHz(target.Instrument, sourceIndex), target.Instrument == InstrumentType.Erhu);
            return VoiceDesign.Render(data, clip.frequency, spec);
        }

        static float Volume(InstrumentTarget target)
        {
            var so = new UnityEditor.SerializedObject(target);
            return so.FindProperty("volume").floatValue;
        }

        // Worst case for one instrument: the loudest-sustaining voices stacked on back-to-back steps.
        static float[] DensestLoop(InstrumentTarget target, int rate, out float voicePeak)
        {
            double step = 60.0 / 88.0 / MusicClock.StepsPerBeat;
            var mix = new float[rate * 4];
            voicePeak = 0f;
            for (int e = 0; e <= EchoSequencer.MaxPerTarget; e++) // 6 echoes and one live note on the next step
            {
                var orb = OrbCatalog.All[e % OrbCatalog.Count].Type;
                var voice = Voice(target, orb, e % NoteMapping.Count);
                voicePeak = Math.Max(voicePeak, voice.Max(v => Math.Abs(v)));
                int start = (int)(e * step * rate);
                float gain = Volume(target) * IntensityMapping.Strength(IntensityTier.Strong);
                for (int i = 0; i < voice.Length && start + i < mix.Length; i++) mix[start + i] += voice[i] * gain;
            }
            return mix;
        }

        [Test]
        public void TheDensestLoopNeverClipsTheOutput()
        {
            var targets = UnityEngine.Object.FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None);
            Assert.AreEqual(3, targets.Length);
            int rate = targets[0].Notes[0].frequency;
            var total = new float[rate * 4];
            string report = "";
            foreach (var target in targets)
            {
                var loop = DensestLoop(target, rate, out float voicePeak);
                float loopPeak = loop.Max(v => Math.Abs(v));
                report += $"{target.Instrument}: voice peak {Db(voicePeak):F1} dBFS, densest loop {Db(loopPeak):F1} dBFS; ";
                Assert.LessOrEqual(voicePeak, VoiceDesign.MaxPeak + 1e-4f, $"{target.Instrument} single voice headroom");
                for (int i = 0; i < total.Length; i++) total[i] += loop[i];
            }
            float dry = total.Max(v => Math.Abs(v));
            report += $"all three together {Db(dry):F1} dBFS dry";

            // The master limiter (on the AudioListener) is what keeps this from clipping.
            var limiter = new PeakLimiter(rate);
            var limited = (float[])total.Clone();
            limiter.Process(limited, 1);
            float peak = limited.Max(v => Math.Abs(v));
            report += $", {Db(peak):F1} dBFS after the limiter";
            Debug.Log($"[RoomOrchestra] Mix safety: {report}");
            Assert.LessOrEqual(peak, PeakLimiter.DefaultThreshold + 1e-5f, $"the densest possible song would clip: {report}");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<MasterLimiter>(), "limiter on the listener in the scene");
        }

        [Test]
        public void TheLimiterLeavesNormalPlayUntouched()
        {
            var limiter = new PeakLimiter(48000);
            var quiet = Enumerable.Range(0, 48000).Select(i => 0.6f * Mathf.Sin(i * 0.05f)).ToArray();
            var copy = (float[])quiet.Clone();
            limiter.Process(copy, 1);
            CollectionAssert.AreEqual(quiet, copy, "below the threshold: bit-for-bit unchanged");
            Assert.AreEqual(0f, limiter.MaxReductionDb);

            // Interleaved stereo: left and right are the same wave at different levels (frame i / 2).
            var loud = Enumerable.Range(0, 48000 * 2).Select(i => (i % 2 == 0 ? 2f : 1.5f) * Mathf.Sin((i / 2) * 0.01f)).ToArray();
            limiter.Process(loud, 2);
            Assert.LessOrEqual(loud.Max(v => Math.Abs(v)), PeakLimiter.DefaultThreshold + 1e-5f, "never over the threshold");
            Assert.Greater(limiter.MaxReductionDb, 6f, "turned down when it had to");
            float leftOverRight = Math.Abs(loud[400]) / Math.Max(1e-6f, Math.Abs(loud[401]));
            Assert.AreEqual(2f / 1.5f, leftOverRight, 0.01f, "both channels turned down together: the stereo image holds");
        }

        static float Db(float x) => 20f * Mathf.Log10(Mathf.Max(x, 1e-6f));
    }
}
