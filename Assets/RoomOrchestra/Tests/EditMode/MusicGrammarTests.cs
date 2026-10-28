using System;
using System.Linq;
using NUnit.Framework;

namespace RoomOrchestra.Tests
{
    public class NoteMappingTests
    {
        [TestCase(0f, 0)]
        [TestCase(0.1999f, 0)]
        [TestCase(0.2f, 1)]
        [TestCase(0.3999f, 1)]
        [TestCase(0.4f, 2)]
        [TestCase(0.6f, 3)]
        [TestCase(0.7999f, 3)]
        [TestCase(0.8f, 4)]
        [TestCase(0.9999f, 4)]
        [TestCase(1f, 4)]
        [TestCase(-0.5f, 0)]
        [TestCase(1.5f, 4)]
        public void HeightBandsIncludingExactZeroAndOne(float height, int expected)
        {
            Assert.AreEqual(expected, NoteMapping.IndexFromHeight(height));
        }

        [Test]
        public void EachPetalMatchesTheCentreOfItsHeightBand()
        {
            for (int petal = 0; petal < NoteMapping.Count; petal++)
            {
                float centre = (petal + 0.5f) / NoteMapping.Count;
                Assert.AreEqual(NoteMapping.IndexFor(PitchInput.FromHeight(centre)), NoteMapping.IndexFor(PitchInput.FromPetal(petal)));
            }
        }

        [Test]
        public void MbiraAndErhuScaleStepsAreDMajorPentatonic()
        {
            var mbira = Enumerable.Range(0, 5).Select(i => NoteMapping.Resolve(InstrumentType.Mbira, PitchInput.FromPetal(i)).Label);
            var erhu = Enumerable.Range(0, 5).Select(i => NoteMapping.Resolve(InstrumentType.Erhu, PitchInput.FromPetal(i)).Label);
            CollectionAssert.AreEqual(new[] { "D4", "E4", "F#4", "A4", "B4" }, mbira);
            CollectionAssert.AreEqual(new[] { "D5", "E5", "F#5", "A5", "B5" }, erhu);
        }

        [Test]
        public void UduIsPercussionArticulationsNotKeyboardNotes()
        {
            var labels = Enumerable.Range(0, 5).Select(i => NoteMapping.Resolve(InstrumentType.Udu, PitchInput.FromPetal(i))).ToArray();
            Assert.IsTrue(labels.All(n => n.IsPercussion));
            CollectionAssert.AreEqual(
                new[] { UduArticulation.Open, UduArticulation.Muted, UduArticulation.Slap, UduArticulation.Bend, UduArticulation.Tap },
                labels.Select(n => n.Articulation));
            Assert.IsFalse(labels.Any(n => n.Label.Contains("D2") || n.Label.Contains("#")), "no pitched note names on udu");
        }

        [Test]
        public void SameInputAlwaysGivesTheSameNote()
        {
            for (int repeat = 0; repeat < 50; repeat++)
            {
                var a = new MusicalHitEvent(repeat, repeat, InstrumentType.Erhu, OrbType.Flutter, IntensityTier.Medium,
                    PitchInput.FromHeight(0.63f), InputPath.Toss, repeat);
                Assert.AreEqual("A5", a.Note.Label);
            }
        }

        [Test]
        public void OrbAndTierNeverChangeTheNote()
        {
            foreach (var orb in OrbCatalog.All)
            foreach (IntensityTier tier in Enum.GetValues(typeof(IntensityTier)))
            {
                var hit = new MusicalHitEvent(1, 1, InstrumentType.Mbira, orb.Type, tier, PitchInput.FromPetal(3), InputPath.Place, 0);
                Assert.AreEqual("A4", hit.Note.Label);
            }
        }
    }

    public class VoiceDesignTests
    {
        const int Rate = 44100;

        // A decaying 220 Hz tone with harmonics: used for the pure-math properties.
        static float[] TestTone(float seconds = 1f)
        {
            var data = new float[(int)(seconds * Rate)];
            for (int i = 0; i < data.Length; i++)
            {
                double t = i / (double)Rate;
                double env = Math.Exp(-3 * t);
                data[i] = (float)(env * (0.6 * Math.Sin(2 * Math.PI * 220 * t) + 0.3 * Math.Sin(2 * Math.PI * 660 * t) + 0.1 * Math.Sin(2 * Math.PI * 1320 * t)));
            }
            return data;
        }

        static float[] Voice(OrbType orb, VoiceSpec basis) =>
            VoiceDesign.Render(TestTone(), Rate, VoiceDesign.ForOrb(orb, basis, 220f, bowed: false));

        [Test]
        public void RenderingIsDeterministic()
        {
            CollectionAssert.AreEqual(Voice(OrbType.Boom, VoiceSpec.Plain), Voice(OrbType.Boom, VoiceSpec.Plain));
        }

        [Test]
        public void EveryVoiceHasHeadroomAndEndsSilentWithoutAClick()
        {
            foreach (var orb in OrbCatalog.All)
            {
                var bases = new[] { VoiceSpec.Plain }.Concat(Enum.GetValues(typeof(UduArticulation)).Cast<UduArticulation>().Select(VoiceDesign.ForUdu));
                foreach (var basis in bases)
                {
                    float[] v = Voice(orb.Type, basis);
                    Assert.Greater(v.Length, 0);
                    Assert.LessOrEqual(v.Max(x => Math.Abs(x)), VoiceDesign.MaxPeak + 1e-4f, $"{orb.Type} clips");
                    Assert.Less(Math.Abs(v[v.Length - 1]), 1e-3f, $"{orb.Type} ends with a click");
                }
            }
        }

        [Test]
        public void ShortSamplesStillRenderEveryOrbVoice()
        {
            var shortTone = TestTone(0.2f); // shorter than Flutter's 0.4 s bowed start offset
            foreach (var orb in OrbCatalog.All)
                Assert.Greater(VoiceDesign.Render(shortTone, Rate, VoiceDesign.ForOrb(orb.Type, VoiceSpec.Plain, 220f, bowed: true)).Length, 0, orb.DisplayName);
        }

        [Test]
        public void OvertoneMeasureSeesHarmonics()
        {
            var pure = new float[Rate];
            for (int i = 0; i < pure.Length; i++) pure[i] = (float)Math.Sin(2 * Math.PI * 220 * i / Rate);
            Assert.Less(VoiceDesign.OvertoneFraction(pure, Rate, 220f), 0.01f, "pure tone has no overtones");
            Assert.Greater(VoiceDesign.OvertoneFraction(TestTone(), Rate, 220f), 0.15f, "test tone has overtones");
        }

        [Test]
        public void UduArticulationsAreDistinct()
        {
            float[] open = VoiceDesign.Render(TestTone(), Rate, VoiceDesign.ForUdu(UduArticulation.Open));
            float[] muted = VoiceDesign.Render(TestTone(), Rate, VoiceDesign.ForUdu(UduArticulation.Muted));
            float[] tap = VoiceDesign.Render(TestTone(), Rate, VoiceDesign.ForUdu(UduArticulation.Tap));
            float[] bend = VoiceDesign.Render(TestTone(), Rate, VoiceDesign.ForUdu(UduArticulation.Bend));
            Assert.LessOrEqual(muted.Length, (int)(0.18f * Rate));
            Assert.LessOrEqual(tap.Length, (int)(0.1f * Rate));
            Assert.Greater(open.Length, muted.Length * 3, "open rings, muted is damped");
            Assert.Less(bend.Length, open.Length, "bend starts faster (higher), so it plays through the sample sooner");
            Assert.AreNotEqual(VoiceDesign.UduSourceIndex(UduArticulation.Open), VoiceDesign.UduSourceIndex(UduArticulation.Slap));
        }

        [Test]
        public void PitchedVoicesKeepTheirPitch()
        {
            // Pitch = the waveform's repeat period (autocorrelation peak), which overtones do not change.
            // The 220 Hz test tone repeats every 200.45 samples.
            int Period(float[] d)
            {
                int n = Math.Min(d.Length, Rate / 10), best = 0;
                double bestScore = double.MinValue;
                for (int lag = 100; lag <= 400; lag++)
                {
                    double score = 0;
                    for (int i = 0; i + lag < n; i++) score += d[i] * d[i + lag];
                    if (score > bestScore) { bestScore = score; best = lag; }
                }
                return best;
            }
            int reference = Period(TestTone());
            Assert.AreEqual(200, reference, 1);
            foreach (var orb in OrbCatalog.All)
            {
                var spec = VoiceDesign.ForOrb(orb.Type, VoiceSpec.Plain, 220f, false);
                Assert.AreEqual(1f, spec.RateStart, orb.DisplayName);
                Assert.AreEqual(1f, spec.RateEnd, orb.DisplayName);
                Assert.AreEqual(reference, Period(Voice(orb.Type, VoiceSpec.Plain)), 1, orb.DisplayName);
            }
        }
    }

    /// <summary>
    /// Orb timbre measured on the project's REAL placeholder samples (not a synthetic tone): an earlier
    /// version passed synthetic tests but Flutter and Bop sounded the same on the lamp to the owner.
    /// </summary>
    public class RealSampleTimbreTests
    {
        static float[] Load(string path, out int rate)
        {
            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AudioClip>(path);
            Assert.IsNotNull(clip, path);
            clip.LoadAudioData();
            var data = new float[clip.samples * clip.channels];
            Assert.IsTrue(clip.GetData(data, 0), path);
            rate = clip.frequency;
            return data;
        }

        static readonly string[] s_Files = { "Udu/Udu_0_D2", "Mbira/Mbira_0_D4", "Mbira/Mbira_2_Fs4", "Mbira/Mbira_4_B4", "Erhu/Erhu_0_D5", "Erhu/Erhu_2_Fs5", "Erhu/Erhu_4_B5" };

        static (InstrumentType, int) Parse(string file)
        {
            var instrument = (InstrumentType)Enum.Parse(typeof(InstrumentType), file.Split('/')[0]);
            int index = int.Parse(file.Split('_')[1]);
            return (instrument, index);
        }

        [TestCaseSource(nameof(s_Files))]
        public void FlutterShortBopPlainBoomLongAndDarker(string file)
        {
            var (instrument, index) = Parse(file);
            float[] source = Load($"Assets/RoomOrchestra/Audio/{file}.wav", out int rate);
            float f0 = NoteMapping.FrequencyHz(instrument, index);
            bool bowed = instrument == InstrumentType.Erhu;
            float[] Voice(OrbType orb) => VoiceDesign.Render(source, rate, VoiceDesign.ForOrb(orb, VoiceSpec.Plain, f0, bowed));

            float[] flutter = Voice(OrbType.Flutter), bop = Voice(OrbType.Bop), boom = Voice(OrbType.Boom);
            float flutterS = flutter.Length / (float)rate, bopS = bop.Length / (float)rate, boomS = boom.Length / (float)rate;
            float bopOver = VoiceDesign.OvertoneFraction(bop, rate, f0), boomOver = VoiceDesign.OvertoneFraction(boom, rate, f0);
            float flutterOver = VoiceDesign.OvertoneFraction(flutter, rate, f0);
            TestContext.WriteLine($"{file}: length F {flutterS:F2}s Bop {bopS:F2}s Boom {boomS:F2}s | overtones F {flutterOver:P0} Bop {bopOver:P0} Boom {boomOver:P0}");

            Assert.Less(flutterS, bopS * 0.6f, "Flutter is a short blip");
            Assert.GreaterOrEqual(boomS, bopS + 0.4f, "Boom rings longer (echo tail)");
            if (bopOver > 0.02f) Assert.Less(boomOver, bopOver * 0.6f, "Boom is clearly darker");
            foreach (var v in new[] { flutter, bop, boom })
                Assert.LessOrEqual(v.Max(x => Math.Abs(x)), VoiceDesign.MaxPeak + 1e-4f);
        }

        // Loudness of the first 200 ms (what the ear hears as the note's level), in dB.
        static float OnsetDb(float[] v, int rate)
        {
            int n = Math.Min(v.Length, rate / 5);
            double energy = 0;
            for (int i = 0; i < n; i++) energy += v[i] * v[i];
            return 10f * (float)Math.Log10(Math.Max(energy / n, 1e-12));
        }

        [TestCaseSource(nameof(s_Files))]
        public void WeightChangesTimbreNotLoudness(string file)
        {
            var (instrument, index) = Parse(file);
            float[] source = Load($"Assets/RoomOrchestra/Audio/{file}.wav", out int rate);
            float f0 = NoteMapping.FrequencyHz(instrument, index);
            bool bowed = instrument == InstrumentType.Erhu;
            float Db(OrbType orb) => OnsetDb(VoiceDesign.Render(source, rate, VoiceDesign.ForOrb(orb, VoiceSpec.Plain, f0, bowed)), rate);

            float flutter = Db(OrbType.Flutter), bop = Db(OrbType.Bop), boom = Db(OrbType.Boom);
            TestContext.WriteLine($"{file}: onset loudness F {flutter:F1} dB, Bop {bop:F1} dB, Boom {boom:F1} dB");
            Assert.GreaterOrEqual(flutter, bop - 4f, "Flutter is lighter, not lost in the mix");
            Assert.LessOrEqual(boom, bop + 1f, "Boom is heavier in sound, not just louder");
        }
    }

    public class PetalSelectionTests
    {
        [Test]
        public void PetalCyclesLowToHighAndWraps()
        {
            var m = new SelectionModel(3, petal: 4);
            m.CyclePetal();
            Assert.AreEqual(0, m.Petal);
            m.SetPetal(-1);
            Assert.AreEqual(4, m.Petal);
        }

        [Test]
        public void StickClickCyclesPetalOncePerPress()
        {
            var mapper = new ControllerCommandMapper();
            var idle = new ControllerFrame { Connected = true };
            mapper.Update(default, idle);
            var click = idle;
            click.StickClick = true;
            int count = 0;
            for (int i = 0; i < 30; i++) count += mapper.Update(default, click).Count(c => c == ControllerCommand.CyclePetal);
            Assert.AreEqual(1, count);
        }
    }
}
