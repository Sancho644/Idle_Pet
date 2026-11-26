using System;
using IdlePet.Domain;
using IdlePet.Domain.Simulation.Time;
using NUnit.Framework;

namespace IdlePet.Tests.EditMode.Domain.Simulation.Time
{
    public sealed class LogicalTimeTests
    {
        [TestCase(0L)]
        [TestCase(1_234_567L)]
        [TestCase(long.MaxValue)]
        public void TimestampAndDurationRoundTripTheirExactMilliseconds(long milliseconds)
        {
            var time = new LogicalTime(milliseconds);
            var duration = new LogicalDuration(milliseconds);

            Assert.That(time.Milliseconds, Is.EqualTo(milliseconds));
            Assert.That(new LogicalTime(time.Milliseconds), Is.EqualTo(time));
            Assert.That(new LogicalDuration(duration.Milliseconds), Is.EqualTo(duration));
            Assert.That(time.GetHashCode(), Is.EqualTo(new LogicalTime(milliseconds).GetHashCode()));
            Assert.That(duration.GetHashCode(), Is.EqualTo(new LogicalDuration(milliseconds).GetHashCode()));
        }

        [Test]
        public void DefaultValuesAreZeroAndCorrectlyOrdered()
        {
            LogicalTime zero = default;
            LogicalDuration nothing = default;
            var next = new LogicalTime(1);

            Assert.That(zero, Is.EqualTo(new LogicalTime(0)));
            Assert.That(nothing, Is.EqualTo(new LogicalDuration(0)));
            Assert.That(zero.CompareTo(next), Is.LessThan(0));
            Assert.That(next.CompareTo(zero), Is.GreaterThan(0));
            Assert.That(zero.CompareTo(new LogicalTime(0)), Is.Zero);
            Assert.That(next.Add(nothing), Is.EqualTo(next));
        }

        [TestCase(-1L)]
        [TestCase(long.MinValue)]
        public void NegativeTimeAndDurationAreRejected(long milliseconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LogicalTime(milliseconds));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LogicalDuration(milliseconds));
        }

        [Test]
        public void AddsMillisecondsAndLargeIntervalsUpToTheRepresentableLimit()
        {
            var initial = new LogicalTime(7);
            var nearLimit = new LogicalDuration(long.MaxValue - 8);
            var finalMillisecond = new LogicalDuration(1);

            Assert.That(initial.Add(finalMillisecond).Milliseconds, Is.EqualTo(8));
            Assert.That(initial.Add(nearLimit.Add(finalMillisecond)).Milliseconds, Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void TimestampAndDurationAdditionSignalOverflow()
        {
            var limit = new LogicalDuration(long.MaxValue);
            var oneMillisecond = new LogicalDuration(1);

            Assert.Throws<OverflowException>(() => new LogicalTime(long.MaxValue).Add(oneMillisecond));
            Assert.Throws<OverflowException>(() => limit.Add(oneMillisecond));
        }

        [Test]
        public void HourConversionIsExactAndCheckedAtBothBounds()
        {
            const long maxHours = 2_562_047_788_015;

            Assert.That(LogicalDuration.FromHours(0).Milliseconds, Is.Zero);
            Assert.That(LogicalDuration.FromHours(24).Milliseconds, Is.EqualTo(86_400_000));
            Assert.That(LogicalDuration.FromHours(maxHours).Milliseconds, Is.EqualTo(9_223_372_036_854_000_000L));
            Assert.Throws<OverflowException>(() => LogicalDuration.FromHours(maxHours + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => LogicalDuration.FromHours(-1));
        }

        [TestCase(1_000L, 1_001L, true, 1L)]
        [TestCase(1_000L, 1_000L, true, 0L)]
        [TestCase(1_000L, 999L, false, 0L)]
        [TestCase(0L, long.MaxValue, true, long.MaxValue)]
        [TestCase(long.MaxValue, 0L, false, 0L)]
        public void ValidatesTargetsAgainstTheProcessedCursor(long cursor, long target, bool accepted, long milliseconds)
        {
            var state = new GameState(cursor, null, 42UL);
            var current = SimulationClock.GetTime(state);

            var allowed = SimulationClock.TryGetElapsed(current, new LogicalTime(target), out var elapsed);

            Assert.That(allowed, Is.EqualTo(accepted));
            Assert.That(elapsed.Milliseconds, Is.EqualTo(milliseconds));
            Assert.That(state.LogicalTimeMilliseconds, Is.EqualTo(cursor));
            if (accepted)
            {
                Assert.That(current.Add(elapsed).Milliseconds, Is.EqualTo(target));
            }
        }
    }
}
