using System.Linq;
using NUnit.Framework;

namespace RoomOrchestra.Tests
{
    public class OrbCatalogTests
    {
        [Test]
        public void ExactlyThreeOrbsInEnumOrder()
        {
            Assert.AreEqual(3, OrbCatalog.Count);
            for (int i = 0; i < OrbCatalog.Count; i++)
                Assert.AreEqual((OrbType)i, OrbCatalog.All[i].Type);
        }

        [Test]
        public void OrbsDifferByShapeAndMotifNotOnlyColor()
        {
            Assert.AreEqual(3, OrbCatalog.All.Select(p => p.Silhouette).Distinct().Count());
            Assert.AreEqual(3, OrbCatalog.All.Select(p => p.Motif).Distinct().Count());
            var flutter = OrbCatalog.Get(OrbType.Flutter);
            var boom = OrbCatalog.Get(OrbType.Boom);
            Assert.Greater(flutter.ScaleY, flutter.ScaleX, "Flutter is tall");
            Assert.Greater(boom.ScaleX, boom.ScaleY, "Boom is wide");
        }

        [Test]
        public void WeightIncreasesFlutterBopBoom()
        {
            Assert.Less(OrbCatalog.Get(OrbType.Flutter).Mass, OrbCatalog.Get(OrbType.Bop).Mass);
            Assert.Less(OrbCatalog.Get(OrbType.Bop).Mass, OrbCatalog.Get(OrbType.Boom).Mass);
        }

        [Test]
        public void EveryOrbHasSafeSpeedClamp()
        {
            foreach (var p in OrbCatalog.All)
                Assert.That(p.MaxLaunchSpeed, Is.InRange(1f, 10f), p.DisplayName);
        }
    }

    public class IntensityMappingTests
    {
        [TestCase(0f, IntensityTier.Quiet)]
        [TestCase(2.49f, IntensityTier.Quiet)]
        [TestCase(2.5f, IntensityTier.Medium)]
        [TestCase(4.99f, IntensityTier.Medium)]
        [TestCase(5f, IntensityTier.Strong)]
        [TestCase(30f, IntensityTier.Strong)]
        public void ThresholdsAreInclusiveLowerBounds(float speed, IntensityTier expected)
        {
            Assert.AreEqual(expected, new IntensityMapping().FromImpactSpeed(speed));
        }

        [Test]
        public void SensitivityLetsGentleThrowsReachStrong()
        {
            var mapping = new IntensityMapping { Sensitivity = 2f };
            Assert.AreEqual(IntensityTier.Strong, mapping.FromImpactSpeed(2.5f));
        }

        [Test]
        public void StrengthRisesWithTierAndQuietIsAudible()
        {
            Assert.Greater(IntensityMapping.Strength(IntensityTier.Quiet), 0.3f);
            Assert.Less(IntensityMapping.Strength(IntensityTier.Quiet), IntensityMapping.Strength(IntensityTier.Medium));
            Assert.Less(IntensityMapping.Strength(IntensityTier.Medium), IntensityMapping.Strength(IntensityTier.Strong));
            Assert.AreEqual(1f, IntensityMapping.Strength(IntensityTier.Strong));
        }
    }

    public class DeduplicationTests
    {
        [Test]
        public void BounceSpamWithinOneActionYieldsOneEvent()
        {
            var factory = new MusicalHitFactory();
            long action = factory.BeginAction();
            int created = 0;
            for (int bounce = 0; bounce < 20; bounce++)
            {
                // Bounces also ricochet across targets; still the same intended action.
                var target = (InstrumentType)(bounce % 3);
                if (factory.TryCreate(action, target, OrbType.Bop, IntensityTier.Medium,
                        PitchInput.FromHeight(0.5f), InputPath.Toss, bounce * 0.01) != null)
                    created++;
            }
            Assert.AreEqual(1, created);
        }

        [Test]
        public void SeparateActionsEachYieldAnEvent()
        {
            var factory = new MusicalHitFactory();
            var a = factory.TryCreate(factory.BeginAction(), InstrumentType.Erhu, OrbType.Flutter,
                IntensityTier.Medium, PitchInput.FromPetal(2), InputPath.Place, 0);
            var b = factory.TryCreate(factory.BeginAction(), InstrumentType.Erhu, OrbType.Flutter,
                IntensityTier.Medium, PitchInput.FromPetal(2), InputPath.Place, 0);
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreNotEqual(a.StableId, b.StableId);
            Assert.Less(a.StableId, b.StableId, "ids are monotonic and deterministic");
        }

        [Test]
        public void RepeatWindowBoundary()
        {
            var dedup = new CollisionDeduplicator { RepeatWindow = 0.2 };
            Assert.IsTrue(dedup.TryAcceptRepeat("lamp", 1.0));
            Assert.IsFalse(dedup.TryAcceptRepeat("lamp", 1.016), "double-reported PICO tap");
            Assert.IsFalse(dedup.TryAcceptRepeat("lamp", 1.199));
            Assert.IsTrue(dedup.TryAcceptRepeat("lamp", 1.2), "window end is exclusive");
            Assert.IsTrue(dedup.TryAcceptRepeat("chair", 1.2), "keys are independent");
        }

        [Test]
        public void ResetForgetsHistoryAndRestartsIds()
        {
            var factory = new MusicalHitFactory();
            long action = factory.BeginAction();
            factory.TryCreate(action, InstrumentType.Udu, OrbType.Boom, IntensityTier.Quiet, PitchInput.FromPetal(0), InputPath.Toss, 0);
            factory.Reset();
            Assert.AreEqual(0, factory.EventCount);
            var again = factory.TryCreate(factory.BeginAction(), InstrumentType.Udu, OrbType.Boom,
                IntensityTier.Quiet, PitchInput.FromPetal(0), InputPath.Toss, 0);
            Assert.AreEqual(1, again.StableId);
        }

        [Test]
        public void OldActionsAreForgottenWithoutGrowingForever()
        {
            var dedup = new CollisionDeduplicator();
            for (long i = 1; i <= 1000; i++) Assert.IsTrue(dedup.TryAcceptAction(i));
            Assert.IsFalse(dedup.HasFired(1), "bounded memory");
            Assert.IsTrue(dedup.HasFired(1000));
        }
    }

    public class MusicalHitEventTests
    {
        [Test]
        public void TossAndPlaceWithSameChoicesAreSameIntent()
        {
            var toss = new MusicalHitEvent(1, 1, InstrumentType.Erhu, OrbType.Flutter, IntensityTier.Strong,
                PitchInput.FromHeight(0.7f), InputPath.Toss, 10.0);
            var place = new MusicalHitEvent(2, 2, InstrumentType.Erhu, OrbType.Flutter, IntensityTier.Strong,
                PitchInput.FromHeight(0.7f), InputPath.Place, 99.0);
            Assert.IsTrue(toss.IsSameIntent(place));
        }

        [Test]
        public void DifferentOrbTargetOrTierIsDifferentIntent()
        {
            var a = new MusicalHitEvent(1, 1, InstrumentType.Erhu, OrbType.Flutter, IntensityTier.Medium, PitchInput.FromPetal(2), InputPath.Place, 0);
            Assert.IsFalse(a.IsSameIntent(new MusicalHitEvent(2, 2, InstrumentType.Erhu, OrbType.Bop, IntensityTier.Medium, PitchInput.FromPetal(2), InputPath.Place, 0)));
            Assert.IsFalse(a.IsSameIntent(new MusicalHitEvent(2, 2, InstrumentType.Mbira, OrbType.Flutter, IntensityTier.Medium, PitchInput.FromPetal(2), InputPath.Place, 0)));
            Assert.IsFalse(a.IsSameIntent(new MusicalHitEvent(2, 2, InstrumentType.Erhu, OrbType.Flutter, IntensityTier.Quiet, PitchInput.FromPetal(2), InputPath.Place, 0)));
            Assert.IsFalse(a.IsSameIntent(new MusicalHitEvent(2, 2, InstrumentType.Erhu, OrbType.Flutter, IntensityTier.Medium, PitchInput.FromPetal(3), InputPath.Place, 0)));
        }

        [TestCase(-1f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(1f, 1f)]
        [TestCase(1.5f, 1f)]
        public void HeightIsClamped(float input, float expected)
        {
            Assert.AreEqual(expected, PitchInput.FromHeight(input).Height);
        }

        [TestCase(-3, 0)]
        [TestCase(0, 0)]
        [TestCase(4, 4)]
        [TestCase(9, 4)]
        public void PetalIsClamped(int input, int expected)
        {
            Assert.AreEqual(expected, PitchInput.FromPetal(input).Petal);
        }
    }

    public class SelectionModelTests
    {
        [Test]
        public void OrbCyclesWrapBothWays()
        {
            var m = new SelectionModel(3);
            m.CycleOrb(-1);
            Assert.AreEqual(OrbType.Boom, m.SelectedOrb);
            m.CycleOrb(1);
            m.CycleOrb(1);
            Assert.AreEqual(OrbType.Bop, m.SelectedOrb);
        }

        [Test]
        public void TargetCyclesWrapBothWays()
        {
            var m = new SelectionModel(3, focusedTarget: 1);
            m.CycleTarget(1);
            m.CycleTarget(1);
            Assert.AreEqual(0, m.FocusedTarget);
            m.CycleTarget(-1);
            Assert.AreEqual(2, m.FocusedTarget);
        }

        [Test]
        public void TierCyclesQuietMediumStrong()
        {
            var m = new SelectionModel(3, tier: IntensityTier.Strong);
            m.CycleTier();
            Assert.AreEqual(IntensityTier.Quiet, m.Tier);
        }

        [Test]
        public void ChangedFiresOnlyOnRealChange()
        {
            var m = new SelectionModel(3);
            int changes = 0;
            m.Changed += () => changes++;
            m.SelectOrb(OrbType.Flutter);
            m.FocusTarget(0);
            Assert.AreEqual(0, changes);
            m.SelectOrb(OrbType.Boom);
            Assert.AreEqual(1, changes);
        }
    }

    public class ControllerCommandMapperTests
    {
        static ControllerFrame Idle => new ControllerFrame { Connected = true };

        [Test]
        public void HeldButtonPlacesOnceNoAutoRepeat()
        {
            var mapper = new ControllerCommandMapper();
            mapper.Update(Idle, default);
            var pressed = Idle;
            pressed.Primary = true;
            int places = 0;
            for (int frame = 0; frame < 120; frame++)
                places += mapper.Update(pressed, default).Count(c => c == ControllerCommand.Place);
            Assert.AreEqual(1, places);
        }

        [Test]
        public void EitherHandAloneDrivesEveryCommand()
        {
            foreach (int hand in new[] { 0, 1 })
            {
                var mapper = new ControllerCommandMapper();
                var seen = new System.Collections.Generic.HashSet<ControllerCommand>();
                void Step(ControllerFrame f)
                {
                    var cmds = hand == 0 ? mapper.Update(f, default) : mapper.Update(default, f);
                    foreach (var c in cmds) seen.Add(c);
                }
                Step(Idle);
                foreach (var (x, y) in new[] { (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f) })
                {
                    var push = Idle;
                    push.StickX = x;
                    push.StickY = y;
                    Step(push);
                    Step(Idle);
                }
                var a = Idle; a.Primary = true; Step(a); Step(Idle);
                var b = Idle; b.Secondary = true; Step(b); Step(Idle);
                var click = Idle; click.StickClick = true; Step(click); Step(Idle);
                Assert.AreEqual(7, seen.Count, $"hand {hand}");
            }
        }

        [Test]
        public void StickHysteresisPreventsWobbleDoubleFire()
        {
            var mapper = new ControllerCommandMapper();
            mapper.Update(Idle, default);
            int count = 0;
            foreach (float x in new[] { 0.7f, 0.5f, 0.65f, 0.4f, 0.9f, 0.2f, 0.8f })
            {
                var f = Idle;
                f.StickX = x;
                count += mapper.Update(f, default).Count;
            }
            Assert.AreEqual(2, count, "re-arms only after dropping below the release threshold");
        }

        [Test]
        public void DiagonalPushIsOneCommand()
        {
            var mapper = new ControllerCommandMapper();
            mapper.Update(Idle, default);
            var f = Idle;
            f.StickX = 0.8f;
            f.StickY = 0.7f;
            Assert.AreEqual(1, mapper.Update(f, default).Count);
        }

        [Test]
        public void ButtonHeldWhileConnectingIsNotAPhantomPress()
        {
            var mapper = new ControllerCommandMapper();
            var held = Idle;
            held.Primary = true;
            Assert.AreEqual(0, mapper.Update(held, default).Count);
            Assert.AreEqual(0, mapper.Update(held, default).Count);
            Assert.AreEqual(0, mapper.Update(Idle, default).Count);
            Assert.AreEqual(1, mapper.Update(held, default).Count);
        }

        [Test]
        public void DisconnectMidPressThenReconnectDoesNotFire()
        {
            var mapper = new ControllerCommandMapper();
            mapper.Update(Idle, default);
            var held = Idle;
            held.Primary = true;
            Assert.AreEqual(1, mapper.Update(held, default).Count);
            mapper.Update(default, default); // lost
            Assert.AreEqual(0, mapper.Update(held, default).Count, "still held after reconnect");
        }
    }

    public class ReachEnvelopeTests
    {
        [TestCase(0f, 0.9f, 1.5f, ReachResult.Ok)]
        [TestCase(0f, 0.29f, 1.5f, ReachResult.TooLow)]
        [TestCase(0f, 1.41f, 1.5f, ReachResult.TooHigh)]
        [TestCase(0f, 0.9f, 0.99f, ReachResult.TooClose)]
        [TestCase(0f, 0.9f, 3.01f, ReachResult.TooFar)]
        [TestCase(1.5f, 0.9f, 1.5f, ReachResult.OutsideView)]
        public void SeatedBoundaries(float x, float y, float z, ReachResult expected)
        {
            Assert.AreEqual(expected, ReachEnvelope.Seated.Check(x, y, z));
        }

        [Test]
        public void StandingIsAtLeastAsPermissiveAsSeated()
        {
            var seated = ReachEnvelope.Seated;
            var standing = ReachEnvelope.Standing;
            Assert.GreaterOrEqual(standing.MaxHeight, seated.MaxHeight);
            Assert.GreaterOrEqual(standing.MaxYawDegrees, seated.MaxYawDegrees);
        }
    }
}
