using System;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Time;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Domain.Needs.NeedSimulationTestData;

namespace IdlePet.Tests.EditMode.Domain.Needs
{
    public sealed class NeedIntegratorTests
    {
        [TestCase(125L, 7L, 1L, 132L)]
        [TestCase(125L, -7L, 1L, 118L)]
        [TestCase(125L, 0L, long.MaxValue, 125L)]
        [TestCase(125L, long.MinValue, 0L, 125L)]
        [TestCase(1L, -2L, 1L, -1L)]
        [TestCase(-20L, -7L, 2L, -34L)]
        public void IntegratesExactNumeratorsWithSignedRates(long start, long rate, long elapsed, long expected)
        {
            var definition = Single(-120, 360, start);

            var result = NeedIntegrator.Integrate(Exact(start), new NeedRate(rate), new LogicalDuration(elapsed), definition);

            Assert.That(result, Is.EqualTo(Exact(expected)));
            Assert.That(result.ExactNumerator, Is.EqualTo(expected));
            Assert.That(result.Remainder.Numerator, Is.InRange(0, 3_599_999));
        }

        [Test]
        public void RestoredRemainderSurvivesSpeedAndDirectionChanges()
        {
            var definition = Single(-12_000_000, 12_000_000, 3_600_003);
            var first = NeedIntegrator.Integrate(definition.InitialValue, new NeedRate(2), new LogicalDuration(3), definition);
            var restored = new NeedValue(first.MilliUnits, new NeedRemainder(first.Remainder.Numerator));

            var second = NeedIntegrator.Integrate(restored, new NeedRate(7), new LogicalDuration(2), definition);
            var third = NeedIntegrator.Integrate(second, new NeedRate(-11), new LogicalDuration(1), definition);

            Assert.That(first.Remainder.Numerator, Is.EqualTo(9));
            Assert.That(second.Remainder.Numerator, Is.EqualTo(23));
            Assert.That(third.MilliUnits, Is.EqualTo(1));
            Assert.That(third.Remainder.Numerator, Is.EqualTo(12));
            Assert.That(third.ExactNumerator, Is.EqualTo(3_600_012));
        }

        [TestCase(-3L, 8L, 101L)]
        [TestCase(-3L, 9L, 101L)]
        [TestCase(3L, -8L, -101L)]
        [TestCase(3L, -9L, -101L)]
        public void ExactBoundaryAndOvershootBothKeepTheConfiguredFraction(long start, long rate, long boundary)
        {
            var definition = Single(-101, 101, start);

            var result = NeedIntegrator.Integrate(definition.InitialValue, new NeedRate(rate), new LogicalDuration(13), definition);

            Assert.That(result, Is.EqualTo(Exact(boundary)));
            Assert.That(result.Remainder.Numerator, Is.EqualTo(Exact(boundary).Remainder.Numerator));
        }

        [TestCase(101L, long.MaxValue, -7L, 94L)]
        [TestCase(-101L, long.MinValue, 7L, -94L)]
        public void SaturationDoesNotLeaveDebtOrExcessWhenDirectionChanges(long boundary, long outward, long inward, long expected)
        {
            var definition = Single(-101, 101, boundary);
            var saturated = NeedIntegrator.Integrate(Exact(boundary), new NeedRate(outward), new LogicalDuration(long.MaxValue), definition);
            var reversed = NeedIntegrator.Integrate(saturated, new NeedRate(inward), new LogicalDuration(1), definition);

            Assert.That(saturated, Is.EqualTo(Exact(boundary)));
            Assert.That(reversed.ExactNumerator, Is.EqualTo(expected));
        }

        [TestCase(long.MaxValue, long.MaxValue)]
        [TestCase(long.MinValue, long.MinValue)]
        public void ProductsOutsideInt64SaturateAtExactInt64Extremes(long rate, long expected)
        {
            var definition = Single(long.MinValue, long.MaxValue, 1);

            var result = NeedIntegrator.Integrate(Exact(1), new NeedRate(rate), new LogicalDuration(long.MaxValue), definition);

            Assert.That(result, Is.EqualTo(Exact(expected)));
            Assert.That(result.ExactNumerator, Is.EqualTo(expected));
        }

        [TestCase(long.MinValue, long.MaxValue, 2L, long.MaxValue - 1)]
        [TestCase(long.MaxValue, long.MinValue / 2 - 2, 2L, -5L)]
        [TestCase(long.MaxValue - 2, 1L, 1L, long.MaxValue - 1)]
        [TestCase(long.MinValue + 2, -1L, 1L, long.MinValue + 1)]
        public void ProductsAndValuesNearEitherLimitCanStillReturnAnInteriorResult(
            long start, long rate, long duration, long expected)
        {
            var definition = Single(long.MinValue, long.MaxValue, start);

            var result = NeedIntegrator.Integrate(Exact(start), new NeedRate(rate), new LogicalDuration(duration), definition);

            Assert.That(result.ExactNumerator, Is.EqualTo(expected));
        }

        [TestCase(-102L)]
        [TestCase(102L)]
        public void InvalidInputIsRejectedEvenForAZeroDurationAndRate(long numerator)
        {
            var definition = Single(-101, 101, 0);

            Assert.Throws<ArgumentOutOfRangeException>(() => NeedIntegrator.Integrate(Exact(numerator), default, default, definition));
        }
    }
}
