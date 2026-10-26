using System.Linq;
using NUnit.Framework;
using UnityEngine;

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
            [Test]
        public void MenuPressRecentersOncePerPress()
        {
            var idle = new ControllerFrame { Connected = true };
            var mapper = new ControllerCommandMapper();
            mapper.Update(idle, default);
            var menu = idle;
            menu.Menu = true;
            int count = 0;
            for (int frame = 0; frame < 60; frame++) count += mapper.Update(menu, default).Count(c => c == ControllerCommand.Recenter);
            mapper.Update(idle, default);
            count += mapper.Update(menu, default).Count(c => c == ControllerCommand.Recenter);
            Assert.AreEqual(2, count, "one recenter per press, holding never repeats");
        }

        static StagePose Recenter(float x, float z, Vector3 forward, Vector3 up)
        {
            Assert.IsTrue(StagePlacement.TryRecenter(x, z, forward.x, forward.y, forward.z, up.x, up.y, up.z, out var pose));
            return pose;
        }

        [Test]
        public void RecenterPutsStartPointUnderHeadFacingGaze()
        {
            var still = Recenter(0f, 0f, Vector3.forward, Vector3.up);
            Assert.AreEqual(0f, still.X);
            Assert.AreEqual(0f, still.Z);
            Assert.AreEqual(0f, still.YawDegrees, 1e-3f, "already centred: nothing moves");

            var turned = Recenter(1.5f, -2f, Vector3.right, Vector3.up);
            Assert.AreEqual(1.5f, turned.X);
            Assert.AreEqual(-2f, turned.Z);
            Assert.AreEqual(90f, turned.YawDegrees, 1e-3f);

            var tilted = Quaternion.Euler(30f, -90f, 10f); // looking down-left with a head tilt
            Assert.AreEqual(-90f, Recenter(0f, 0f, tilted * Vector3.forward, tilted * Vector3.up).YawDegrees, 1e-3f);
        }

        [Test]
        public void RecenterWorksLookingStraightDownOrUp()
        {
            var down = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(90f, 0f, 0f);
            Assert.AreEqual(180f, Mathf.Abs(Recenter(0f, 0f, down * Vector3.forward, down * Vector3.up).YawDegrees), 1e-3f,
                "facing back while looking at the floor still faces back");
            var up = Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(-89f, 0f, 0f);
            Assert.AreEqual(90f, Recenter(0f, 0f, up * Vector3.forward, up * Vector3.up).YawDegrees, 1e-2f);
            Assert.IsFalse(StagePlacement.TryRecenter(0f, 0f, 0f, -1f, 0f, 0f, 0f, 0f, out _), "no heading at all: keep the stage where it is");
        }

        [Test]
        public void MixLevelsStepDownToMuteAndWrapAroundOneTapEach()
        {
            int level = 0;
            var seen = new System.Collections.Generic.List<string> { MixLevels.Label(level) };
            for (int tap = 0; tap < MixLevels.Count; tap++)
            {
                level = MixLevels.Next(level);
                seen.Add(MixLevels.Label(level));
            }
            CollectionAssert.AreEqual(new[] { "Vol 100%", "Vol 60%", "Vol 30%", "Muted", "Vol 100%" }, seen);
            Assert.AreEqual(0f, MixLevels.Gain(3));
            Assert.IsTrue(MixLevels.IsMuted(3));
            Assert.AreEqual(1f, MixLevels.Gain(0));
        }

        [Test]
        public void PostureTogglesAndStandingLiftFitsTheStandingReach()
        {
            Assert.AreEqual(PlayPosture.Standing, PostureLayout.Next(PlayPosture.Seated));
            Assert.AreEqual(PlayPosture.Seated, PostureLayout.Next(PlayPosture.Standing));
            Assert.AreEqual(0f, PostureLayout.ControlsLift(PlayPosture.Seated));
            Assert.Greater(PostureLayout.ControlsLift(PlayPosture.Standing), 0f);
            // Top pitch tile (1.15 m seated) stays below standing eye level once lifted.
            Assert.Less(1.15f + PostureLayout.StandingLift, PostureLayout.EyeHeight(PlayPosture.Standing));
            Assert.AreEqual(ReachResult.Ok, PostureLayout.Reach(PlayPosture.Standing).Check(0f, 1.15f + PostureLayout.StandingLift, 1.5f));
            Assert.AreEqual(ReachResult.TooHigh, PostureLayout.Reach(PlayPosture.Seated).Check(0f, 1.15f + PostureLayout.StandingLift, 1.5f),
                "the lifted layout is for standing only");
        }

        [Test]
        public void FirstCharacterOnAnObjectIsCuriousTheNextOneFindsItsSibling()
        {
            var clock = new MusicClock(0.0);
            var sequencer = new EchoSequencer(clock);
            EchoEvent Plant(OrbType orb, InstrumentType target, int step)
            {
                var hit = new MusicalHitEvent(step + 1, step + 1, target, orb, IntensityTier.Medium, PitchInput.FromPetal(1), InputPath.Place, clock.TimeOf(0, step));
                Assert.AreEqual(EchoAddResult.Added, sequencer.TryAdd(hit, out var echo));
                return echo;
            }
            var first = Plant(OrbType.Flutter, InstrumentType.Erhu, 0);
            Assert.AreEqual(Expression.Curious, CharacterReactions.OnPlanted(sequencer.Echoes, first, out long none));
            Assert.AreEqual(0, none);
            var otherObject = Plant(OrbType.Flutter, InstrumentType.Udu, 2);
            Assert.AreEqual(Expression.Curious, CharacterReactions.OnPlanted(sequencer.Echoes, otherObject, out _), "a sibling must be on the same object");
            var otherKind = Plant(OrbType.Bop, InstrumentType.Erhu, 4);
            Assert.AreEqual(Expression.Curious, CharacterReactions.OnPlanted(sequencer.Echoes, otherKind, out _), "a sibling must be the same character");
            var second = Plant(OrbType.Flutter, InstrumentType.Erhu, 6);
            Assert.AreEqual(Expression.Sibling, CharacterReactions.OnPlanted(sequencer.Echoes, second, out long sibling));
            Assert.AreEqual(first.StableId, sibling);
            Assert.IsFalse(CharacterReactions.IsExcited(CharacterReactions.ExcitedAt - 1));
            Assert.IsTrue(CharacterReactions.IsExcited(CharacterReactions.ExcitedAt));
        }
    }
}
