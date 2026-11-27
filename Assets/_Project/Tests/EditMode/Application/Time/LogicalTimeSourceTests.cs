using System;
using System.Linq;
using IdlePet.Application.Ports.Time;
using IdlePet.Domain;
using IdlePet.Domain.Simulation.Time;
using NUnit.Framework;

namespace IdlePet.Tests.EditMode.Application.Time
{
    public sealed class LogicalTimeSourceTests
    {
        [Test]
        public void FakeClocksReadAndAdvanceIndependently()
        {
            var first = new FakeClock(new LogicalTime(1_000));
            var second = new FakeClock(new LogicalTime(1_000));

            Assert.That(first.GetLogicalTime().Milliseconds, Is.EqualTo(1_000));
            first.Advance(new LogicalDuration(110));
            Assert.That(first.GetLogicalTime().Milliseconds, Is.EqualTo(1_110));
            Assert.That(first.GetLogicalTime(), Is.EqualTo(first.GetLogicalTime()));
            Assert.That(second.GetLogicalTime().Milliseconds, Is.EqualTo(1_000));
        }

        [Test]
        public void InjectedSourceReplaysRepeatedAndBackwardTargetsWithoutChangingGameState()
        {
            var fake = new FakeClock(new LogicalTime(1_000));
            ILogicalTimeSource source = fake;
            var state = new GameState(2_000, null, 42UL);
            var reports = new[] { 1_000L, 2_000L, 1_500L, 2_000L, 3_000L };
            var expected = new[] { (false, 0L), (true, 0L), (false, 0L), (true, 0L), (true, 1_000L) };

            for (int i = 0; i < reports.Length; i++)
            {
                fake.Set(new LogicalTime(reports[i]));
                LogicalTime target = source.GetLogicalTime();

                Assert.That(target.Milliseconds, Is.EqualTo(reports[i]));
                var allowed = SimulationClock.TryGetElapsed(SimulationClock.GetTime(state), target, out var elapsed);
                Assert.That((allowed, elapsed.Milliseconds), Is.EqualTo(expected[i]));
                Assert.That(source.GetLogicalTime(), Is.EqualTo(target));
                Assert.That(state.LogicalTimeMilliseconds, Is.EqualTo(2_000));
            }
        }

        [Test]
        public void FakeClockSignalsOverflowAndKeepsItsLastTarget()
        {
            var fake = new FakeClock(new LogicalTime(long.MaxValue));

            Assert.Throws<OverflowException>(() => fake.Advance(new LogicalDuration(1)));

            Assert.That(fake.GetLogicalTime().Milliseconds, Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void FakeClockStaysInEditModeAndTheTimePortLivesInApplication()
        {
            var domain = typeof(SimulationClock).Assembly;
            var application = typeof(ILogicalTimeSource).Assembly;
            var tests = typeof(FakeClock).Assembly;

            Assert.That(domain.GetName().Name, Is.EqualTo("IdlePet.Domain"));
            Assert.That(application.GetName().Name, Is.EqualTo("IdlePet.Application"));
            Assert.That(tests.GetName().Name, Is.EqualTo("IdlePet.Tests.EditMode"));
            Assert.That(domain.GetReferencedAssemblies().Select(reference => reference.Name), Has.No.Member(application.GetName().Name));
        }
    }
}
