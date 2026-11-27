using System;
using IdlePet.Domain.Common.Numerics;
using NUnit.Framework;

namespace IdlePet.Tests.EditMode.Domain.Numerics
{
    public sealed class NeedNumericsTests
    {
        [TestCase(125L, 450_000_000L)]
        [TestCase(25_500L, 91_800_000_000L)]
        [TestCase(75_123L, 270_442_800_000L)]
        [TestCase(100_000L, 360_000_000_000L)]
        public void ValueKeepsTheWholeMilliUnitsAndExactNumerator(long milliUnits, long numerator)
        {
            var value = new NeedValue(milliUnits, new NeedRemainder(0));

            Assert.That(value.MilliUnits, Is.EqualTo(milliUnits));
            Assert.That(value.ExactNumerator, Is.EqualTo(numerator));
            Assert.That(NeedValue.FromExactNumerator(numerator), Is.EqualTo(value));
        }

        [TestCase(-6_500L)]
        [TestCase(1_250L)]
        [TestCase(0L)]
        public void RatePreservesSignedMilliUnitsPerHour(long milliUnitsPerHour)
        {
            var rate = new NeedRate(milliUnitsPerHour);

            Assert.That(rate.MilliUnitsPerHour, Is.EqualTo(milliUnitsPerHour));
            Assert.That(rate, Is.EqualTo(new NeedRate(milliUnitsPerHour)));
        }

        [TestCase(1_250_000L)]
        [TestCase(1_000_001L)]
        public void CoefficientPreservesMillionths(long millionths)
        {
            var coefficient = new NeedCoefficient(millionths);

            Assert.That(coefficient.Millionths, Is.EqualTo(millionths));
            Assert.That(coefficient, Is.EqualTo(new NeedCoefficient(millionths)));
            Assert.That(coefficient, Is.Not.EqualTo(new NeedCoefficient(millionths - 1)));
        }

        [TestCase(0)]
        [TestCase(3_599_999)]
        public void RemainderAtEitherCanonicalBoundaryRoundTrips(int numerator)
        {
            var original = new NeedValue(125, new NeedRemainder(numerator));
            var restored = NeedValue.FromExactNumerator(original.ExactNumerator);

            Assert.That(original.ExactNumerator, Is.EqualTo(450_000_000L + numerator));
            Assert.That(restored.MilliUnits, Is.EqualTo(125));
            Assert.That(restored.Remainder.Numerator, Is.EqualTo(numerator));
        }

        [TestCase(-1)]
        [TestCase(3_600_000)]
        public void RejectsNonCanonicalRemainder(int numerator)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NeedRemainder(numerator));
        }

        [Test]
        public void EqualMilliUnitsWithDifferentRemaindersHaveDifferentExactValues()
        {
            var lower = new NeedValue(5, new NeedRemainder(1));
            var higher = new NeedValue(5, new NeedRemainder(2));

            Assert.That(lower.CompareTo(higher), Is.LessThan(0));
            Assert.That(higher.CompareTo(lower), Is.GreaterThan(0));
            Assert.That(lower, Is.Not.EqualTo(higher));
            Assert.That(higher.GetHashCode(), Is.EqualTo(NeedValue.FromExactNumerator(higher.ExactNumerator).GetHashCode()));
        }

        [TestCase(long.MinValue)]
        [TestCase(-3_600_000L)]
        [TestCase(-1L)]
        [TestCase(0L)]
        [TestCase(long.MaxValue)]
        public void EveryRepresentableExtremeUsesCanonicalParts(long numerator)
        {
            var value = NeedValue.FromExactNumerator(numerator);
            var restored = new NeedValue(value.MilliUnits, value.Remainder);

            Assert.That(value.Remainder.Numerator, Is.InRange(0, 3_599_999));
            Assert.That(restored.ExactNumerator, Is.EqualTo(numerator));
        }

        [Test]
        public void NegativeExactValueUsesFloorAndPositiveRemainder()
        {
            var value = NeedValue.FromExactNumerator(-1);

            Assert.That(value.MilliUnits, Is.EqualTo(-1));
            Assert.That(value.Remainder.Numerator, Is.EqualTo(3_599_999));
        }

        [TestCase(long.MinValue)]
        [TestCase(long.MaxValue)]
        public void RejectsOverflowDuringScaling(long milliUnits)
        {
            Assert.Throws<OverflowException>(() => new NeedValue(milliUnits, new NeedRemainder(0)));
        }

        [Test]
        public void RejectsOverflowFromTheRemainderAtEachExactLimit()
        {
            var minimum = NeedValue.FromExactNumerator(long.MinValue);
            var maximum = NeedValue.FromExactNumerator(long.MaxValue);

            Assert.Throws<OverflowException>(() =>
                new NeedValue(minimum.MilliUnits, new NeedRemainder(minimum.Remainder.Numerator - 1)));
            Assert.Throws<OverflowException>(() =>
                new NeedValue(maximum.MilliUnits, new NeedRemainder(maximum.Remainder.Numerator + 1)));
        }
    }
}
