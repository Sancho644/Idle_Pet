using System;
using System.Linq;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Time;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Domain.Needs.NeedSimulationTestData;

namespace IdlePet.Tests.EditMode.Domain.Needs
{
    public sealed class NeedThresholdCalculatorTests
    {
        [TestCase(-6L, 7L, 3L, NeedThresholdDirection.Rising)]
        [TestCase(26L, -7L, 3L, NeedThresholdDirection.Falling)]
        [TestCase(4L, 2L, 3L, NeedThresholdDirection.Rising)]
        [TestCase(16L, -2L, 3L, NeedThresholdDirection.Falling)]
        [TestCase(9L, 1L, 1L, NeedThresholdDirection.Rising)]
        [TestCase(11L, -1L, 1L, NeedThresholdDirection.Falling)]
        public void ReturnsTheFirstDiscreteMillisecondAtOrBeyondTheExactThreshold(
            long start, long rate, long time, NeedThresholdDirection direction)
        {
            var definition = Single(-100, 100, start, 10);

            var crossing = NeedThresholdCalculator.FindCrossings(Exact(start), new NeedRate(rate), new LogicalDuration(time), definition).Single();

            Assert.That(crossing.NeedId, Is.EqualTo(NeedId.Satiety));
            Assert.That(crossing.Threshold, Is.EqualTo(Exact(10)));
            Assert.That(crossing.Direction, Is.EqualTo(direction));
            Assert.That(crossing.Elapsed.Milliseconds, Is.EqualTo(time));
            Assert.That(NeedThresholdCalculator.FindCrossings(
                Exact(start), new NeedRate(rate), new LogicalDuration(time - 1), definition), Is.Empty);
        }

        [Test]
        public void FractionalComparisonUsesFullNumerator()
        {
            var threshold = Exact(10);

            Assert.That(Exact(9).MilliUnits, Is.EqualTo(threshold.MilliUnits));
            Assert.That(Exact(11).MilliUnits, Is.EqualTo(threshold.MilliUnits));
            Assert.That(Exact(9).CompareTo(threshold), Is.LessThan(0));
            Assert.That(Exact(11).CompareTo(threshold), Is.GreaterThan(0));
        }

        [TestCase(5L, 0L, 100L)]
        [TestCase(5L, -1L, 100L)]
        [TestCase(15L, 1L, 100L)]
        [TestCase(10L, 1L, 100L)]
        [TestCase(10L, -1L, 100L)]
        [TestCase(9L, 1L, 0L)]
        public void DoesNotReportThresholdsThatAreNotFutureCrossings(long start, long rate, long elapsed)
        {
            var definition = Single(-20, 20, start, 10);

            Assert.That(NeedThresholdCalculator.FindCrossings(Exact(start), new NeedRate(rate), new LogicalDuration(elapsed), definition), Is.Empty);
        }

        [TestCase(1L, 7L, new long[] { 3, 6, 8, 9, 11 })]
        [TestCase(11L, -7L, new long[] { 9, 8, 6, 3, 1 })]
        public void ReturnsAllTiesAndSaturationThresholdsInTravelOrder(long start, long rate, long[] expected)
        {
            var definition = Single(1, 11, start, 1, 3, 6, 8, 9, 11);
            var crossings = NeedThresholdCalculator.FindCrossings(Exact(start), new NeedRate(rate), new LogicalDuration(2), definition);

            Assert.That(crossings.Select(crossing => crossing.Threshold.ExactNumerator), Is.EqualTo(expected));
            Assert.That(crossings.Select(crossing => crossing.Elapsed.Milliseconds), Is.EqualTo(new[] { 1L, 1L, 1L, 2L, 2L }));
        }

        [TestCase(long.MinValue, long.MaxValue, long.MaxValue, 3L)]
        [TestCase(long.MaxValue, long.MinValue, long.MinValue, 2L)]
        [TestCase(0L, 1L, long.MaxValue, long.MaxValue)]
        public void SupportsWideDistancesAndTheLargestSignedRates(long start, long rate, long target, long expected)
        {
            var definition = Single(long.MinValue, long.MaxValue, start, target);

            var crossings = NeedThresholdCalculator.FindCrossings(Exact(start), new NeedRate(rate), new LogicalDuration(long.MaxValue), definition);

            Assert.That(crossings.Single().Elapsed.Milliseconds, Is.EqualTo(expected));
        }

        [TestCase(long.MinValue, 1L, long.MaxValue)]
        [TestCase(long.MaxValue, -1L, long.MinValue)]
        public void UnrepresentablyDistantThresholdsDoNotProduceWrappedOrClampedTimes(long start, long rate, long target)
        {
            var definition = Single(long.MinValue, long.MaxValue, start, target);

            Assert.That(NeedThresholdCalculator.FindCrossings(Exact(start), new NeedRate(rate), new LogicalDuration(long.MaxValue), definition), Is.Empty);
        }

        [Test]
        public void InvalidCurrentValueIsRejectedBeforeLookingForCrossings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NeedThresholdCalculator.FindCrossings(Exact(11), default, default, Single(0, 10, 0, 5)));
        }
    }
}
