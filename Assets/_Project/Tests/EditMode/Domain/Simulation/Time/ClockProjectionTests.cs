using System;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Simulation.Time;
using NUnit.Framework;

namespace IdlePet.Tests.EditMode.Domain.Simulation.Time
{
    public sealed class ClockProjectionTests
    {
        private const long VisualDay = 7_200_000;
        private const long BiologicalDay = 86_400_000;

        [TestCase(0L, 0L, 0L, 0L, 0L)]
        [TestCase(3_600_000L, 0L, 3_600_000L, 0L, 3_600_000L)]
        [TestCase(7_200_000L, 1L, 0L, 0L, 7_200_000L)]
        [TestCase(21_600_000L, 3L, 0L, 0L, 21_600_000L)]
        [TestCase(43_200_000L, 6L, 0L, 0L, 43_200_000L)]
        [TestCase(86_400_000L, 12L, 0L, 1L, 0L)]
        [TestCase(172_800_000L, 24L, 0L, 2L, 0L)]
        public void ProductionReferenceValuesUseDistinctPeriods(long elapsed, long worldCycle, long worldPhase,
            long biologicalCycle, long biologicalPhase)
        {
            var definition = CreateDefinition(VisualDay, BiologicalDay);
            var time = new LogicalTime(elapsed);

            AssertPhase(new WorldClock(definition, default).Project(time), worldCycle, worldPhase, VisualDay);
            AssertPhase(new CircadianClock(definition, default).Project(time), biologicalCycle, biologicalPhase, BiologicalDay);
        }

        [TestCase(1L, 0L, 1L, 0L, 1L)]
        [TestCase(VisualDay - 1, 0L, VisualDay - 1, 0L, VisualDay - 1)]
        [TestCase(BiologicalDay - 1, 11L, VisualDay - 1, 0L, BiologicalDay - 1)]
        [TestCase(315_360_000_000L, 43_800L, 0L, 3_650L, 0L)]
        [TestCase(long.MaxValue - 1, 1_281_023_894_007L, 4_375_806L, 106_751_991_167L, 25_975_806L)]
        [TestCase(long.MaxValue, 1_281_023_894_007L, 4_375_807L, 106_751_991_167L, 25_975_807L)]
        public void ProjectionHandlesBoundariesAndLargeTimestamps(long elapsed, long worldCycle, long worldPhase,
            long biologicalCycle, long biologicalPhase)
        {
            var definition = CreateDefinition(VisualDay, BiologicalDay);
            var time = new LogicalTime(elapsed);

            AssertPhase(new WorldClock(definition, default).Project(time), worldCycle, worldPhase, VisualDay);
            AssertPhase(new CircadianClock(definition, default).Project(time), biologicalCycle, biologicalPhase, BiologicalDay);
        }

        [Test]
        public void ChangingOnePeriodChangesOnlyItsProjection()
        {
            var baseline = CreateDefinition(10, 30);
            var changedVisual = CreateDefinition(12, 30);
            var changedBiological = CreateDefinition(10, 27);
            var time = new LogicalTime(35);

            Assert.That(new WorldClock(changedBiological, default).Project(time), Is.EqualTo(new WorldClock(baseline, default).Project(time)));
            Assert.That(new CircadianClock(changedVisual, default).Project(time), Is.EqualTo(new CircadianClock(baseline, default).Project(time)));
            AssertPhase(new WorldClock(changedVisual, default).Project(time), 2, 11, 12);
            AssertPhase(new CircadianClock(changedBiological, default).Project(time), 1, 8, 27);
        }

        [TestCase(19L, -1L, 9L, -1L, 9L)]
        [TestCase(20L, 0L, 0L, -1L, 10L)]
        [TestCase(30L, 1L, 0L, 0L, 0L)]
        [TestCase(50L, 3L, 0L, 1L, 0L)]
        [TestCase(0L, -2L, 0L, -2L, 10L)]
        public void ExplicitOriginsMarkCycleZeroAndSupportEarlierTimes(long elapsed, long worldCycle, long worldPhase,
            long biologicalCycle, long biologicalPhase)
        {
            var definition = CreateDefinition(10, 20);
            var time = new LogicalTime(elapsed);

            AssertPhase(new WorldClock(definition, new LogicalTime(20)).Project(time), worldCycle, worldPhase, 10);
            AssertPhase(new CircadianClock(definition, new LogicalTime(30)).Project(time), biologicalCycle, biologicalPhase, 20);
        }

        [Test]
        public void OriginsAndPeriodsNearLongMaxValueDoNotOverflow()
        {
            var definition = CreateDefinition(1, long.MaxValue);
            var lateOrigin = new LogicalTime(long.MaxValue);
            var earlyOrigin = new LogicalTime(1);

            AssertPhase(new WorldClock(definition, lateOrigin).Project(default), -long.MaxValue, 0, 1);
            AssertPhase(new CircadianClock(definition, lateOrigin).Project(earlyOrigin), -1, 1, long.MaxValue);
            AssertPhase(new CircadianClock(definition, earlyOrigin).Project(lateOrigin), 0, long.MaxValue - 1, long.MaxValue);
        }

        [Test]
        public void ProjectionIsIndependentOfReadOrderAndRestoresFromExplicitInputs()
        {
            var definition = CreateDefinition(12, 40);
            var worldOrigin = new LogicalTime(37);
            var biologicalOrigin = new LogicalTime(84);
            var final = new LogicalTime(long.MaxValue);
            var world = new WorldClock(definition, worldOrigin);
            var biological = new CircadianClock(definition, biologicalOrigin);
            CyclePhase directWorld = world.Project(final);
            CyclePhase directBiological = biological.Project(final);

            foreach (long start in new[] { 0L, 100L, 120L, 100L, 500_000_000L })
            {
                biological.Project(new LogicalTime(start));
                world.Project(new LogicalTime(start));
            }

            Assert.That(biological.Project(final), Is.EqualTo(directBiological));
            Assert.That(world.Project(final), Is.EqualTo(directWorld));

            var restoredDefinition = CreateDefinition(definition.VisualDayMilliseconds, definition.BiologicalDayMilliseconds);
            var restoredTime = new LogicalTime(final.Milliseconds);
            Assert.That(new WorldClock(restoredDefinition, new LogicalTime(worldOrigin.Milliseconds)).Project(restoredTime), Is.EqualTo(directWorld));
            Assert.That(new CircadianClock(restoredDefinition, new LogicalTime(biologicalOrigin.Milliseconds)).Project(restoredTime), Is.EqualTo(directBiological));
        }

        [Test]
        public void ClockConstructorsRequireExplicitDefinitions()
        {
            Assert.Throws<ArgumentNullException>(() => new WorldClock(null, default));
            Assert.Throws<ArgumentNullException>(() => new CircadianClock(null, default));
        }

        private static TimeDefinition CreateDefinition(long visual, long biological)
        {
            return new TimeDefinition(visual, biological, 1, 1, biological);
        }

        private static void AssertPhase(CyclePhase phase, long cycle, long elapsed, long period)
        {
            Assert.That(phase.CycleIndex, Is.EqualTo(cycle));
            Assert.That(phase.ElapsedInCycleMilliseconds, Is.EqualTo(elapsed));
            Assert.That(phase.ElapsedInCycleMilliseconds, Is.InRange(0, period - 1));
        }
    }
}
