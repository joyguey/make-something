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

        // A decaying 220 Hz tone with harmonics: a stand-in for a plucked sample.
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

        static float[] Voice(OrbType orb, VoiceSpec basis) => VoiceDesign.Render(TestTone(), Rate, VoiceDesign.ForOrb(orb, basis));

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
        public void FlutterIsBrighterThanBopWhichIsBrighterThanBoom()
        {
            float flutter = VoiceDesign.BrightnessMeasure(Voice(OrbType.Flutter, VoiceSpec.Plain));
            float bop = VoiceDesign.BrightnessMeasure(Voice(OrbType.Bop, VoiceSpec.Plain));
            float boom = VoiceDesign.BrightnessMeasure(Voice(OrbType.Boom, VoiceSpec.Plain));
            Assert.Greater(flutter, bop * 1.2f, "Flutter audibly brighter");
            Assert.Greater(bop, boom * 1.2f, "Boom audibly darker");
        }

        [Test]
        public void FlutterIsShorterThanBoom()
        {
            Assert.Less(Voice(OrbType.Flutter, VoiceSpec.Plain).Length, Voice(OrbType.Boom, VoiceSpec.Plain).Length);
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
            // Pitch = the waveform's repeat period (autocorrelation peak), which brightening the
            // overtones does not change. The 220 Hz test tone repeats every 200.45 samples.
            int Period(float[] d)
            {
                int n = Math.Min(d.Length, Rate / 5), best = 0;
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
                var spec = VoiceDesign.ForOrb(orb.Type, VoiceSpec.Plain);
                Assert.AreEqual(1f, spec.RateStart, orb.DisplayName);
                Assert.AreEqual(1f, spec.RateEnd, orb.DisplayName);
                Assert.AreEqual(reference, Period(Voice(orb.Type, VoiceSpec.Plain)), 1, orb.DisplayName);
            }
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
