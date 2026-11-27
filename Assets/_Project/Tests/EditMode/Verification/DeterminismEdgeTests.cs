using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
using IdlePet.Application.Forecast;
using IdlePet.Domain;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Modifiers;
using IdlePet.Domain.Simulation.Time;
using IdlePet.Editor.BalanceLab;
using IdlePet.Tests.EditMode.Application.Time;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Application.Forecast.ForecastTestData;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;
using Effect = IdlePet.Domain.Simulation.Rules.SimulationRuleEffect;

namespace IdlePet.Tests.EditMode.Verification
{
    public sealed class DeterminismEdgeTests
    {
        [Test]
        public void SchedulerSharesAnUntouchedSnapshotAndCopiesBeforeItsFirstMutation()
        {
            var source = new EventQueueSnapshot(new[] { new ScheduledEvent(Event(10), 7) }, 8);
            var first = new EventScheduler(source);
            var separate = new EventScheduler(source);
            Assert.That(first.First.Sequence, Is.EqualTo(7));
            Assert.That(first.Cancel(99), Is.False);
            first.CancelPetLife(Pet);
            Assert.That(first.Capture(), Is.SameAs(source));
            var inserted = first.Register(At(0), Event(5));
            Assert.That(first.Capture().Events.Select(e => e.Sequence), Is.EqualTo(new[] { 8L, 7L }));
            Assert.That(first.Cancel(inserted.Sequence), Is.True);
            Assert.That(first.Capture().NextSequence, Is.EqualTo(9));
            Assert.That(separate.Capture(), Is.SameAs(source));
            Assert.That(source.NextSequence, Is.EqualTo(8));
            Assert.That(source.Events.Select(e => e.Sequence), Is.EqualTo(new[] { 7L }));
        }

        [Test]
        public void LargeFutureQueueDoesNotConsumeTheEventBudgetAndPreservesSequenceAcrossRestore()
        {
            var cfg = Config(limit: 4);
            var engine = Engine(cfg, Rule("mark"));
            var initial = new GameState(0, null, ulong.MaxValue);
            var queue = new EventQueueSnapshot(Enumerable.Range(0, 4096)
                .Select(i => new ScheduledEvent(Event(10L + i, text: "retained"), i)), 4096);
            var source = GameState.Restore(0, null, initial.Rng, queue, Array.Empty<string>(), 0);
            var direct = LabRunner.Project(engine, source, new[] { 10L });
            var forecast = LabRunner.Project(engine, source, new[] { 1L, 9L, 10L }, forecast: true, restore: true);
            Assert.That(LabReport.Difference(direct, forecast), Is.Null);
            Assert.That(direct.Trace.Count, Is.EqualTo(1));
            Assert.That(direct.State.EventQueue.Events.Count, Is.EqualTo(4095));
            Assert.That(direct.State.EventQueue.NextSequence, Is.EqualTo(4096));
            Assert.That(direct.State.ProcessedEventsAtCursor, Is.EqualTo(1));
            Assert.That(source.EventQueue.Events.Count, Is.EqualTo(4096));
            Assert.That(source.ProcessedEventsAtCursor, Is.Zero);
            Report("large-future-queue", LabReport.State("initial", source), LabReport.Authoritative(direct),
                new XElement("forecast", LabReport.Authoritative(forecast)));
        }

        [TestCase(long.MinValue, long.MaxValue, 0L)]
        [TestCase(long.MaxValue, long.MaxValue, 0L)]
        [TestCase(-71L, 123456L, -800L)]
        [TestCase(71L, 123456L, 800L)]
        public void ExactIntegrationAgreesWithIndependentWideIntegerOracle(long rate, long elapsed, long start)
        {
            var definition = new NeedDefinition(NeedId.Satiety, Exact(-20_000_000), Exact(20_000_000), Exact(start),
                new NeedRate(rate), Array.Empty<NeedValue>());
            var raw = new BigInteger(start) + new BigInteger(rate) * new BigInteger(elapsed);
            long expected = (long)BigInteger.Min(20_000_000, BigInteger.Max(-20_000_000, raw));
            var result = NeedIntegrator.Integrate(Exact(start), new NeedRate(rate), new LogicalDuration(elapsed), definition);
            Assert.That(result.ExactNumerator, Is.EqualTo(expected));
            Assert.That(result.MilliUnits * 3_600_000L + result.Remainder.Numerator, Is.EqualTo(expected));
        }

        [TestCase(-8000000000000L)]
        [TestCase(8000000000000L)]
        public void WideMultiplierProductRoundsOnceAgainstIndependentFraction(long initialRate)
        {
            var modifiers = Enumerable.Range(0, 32).Select(i =>
                Scale("test.m." + i, i % 2 == 0 ? 1_125_000 : 999_999)).ToArray();
            var configuration = Config(rates: _ => initialRate, modifiers: modifiers);
            var denominator = BigInteger.Pow(1_000_000, modifiers.Length);
            var numerator = new BigInteger(initialRate) * BigInteger.Pow(1_125_000, 16) * BigInteger.Pow(999_999, 16);
            var expected = numerator.Sign * ((BigInteger.Abs(numerator) * 2 + denominator) / (2 * denominator));
            Assert.That(NeedRateCalculator.Calculate(configuration, modifiers.Select(m => m.Id))[NeedId.Satiety].MilliUnitsPerHour,
                Is.EqualTo((long)expected));
        }

        [Test]
        public void ReorderingRuleRegistrationAndChangingCulturePreserveTheSameCausalTrace()
        {
            var definition = Config();
            var rules = new[]
            {
                Rule("A", Effect.Schedule(Event(10, "C", 5, source: "source.I")),
                    Effect.Schedule(Event(10, "D", 5, source: "source.i"))),
                Rule("B"), Rule("C", Effect.Schedule(Event(10, "E", 4))), Rule("D"), Rule("E")
            };
            var requests = new[] { Event(10, "A", 10), Event(10, "B", 20) };
            var first = Engine(definition, rules);
            var source = first.Initialize(At(0), null, 42, requests).State;
            var initial = LabReport.State("initial", source);
            var expected = LabRunner.Project(first, source, new[] { 10L });
            var culture = CultureInfo.CurrentCulture;
            LabProjection actual;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                var second = Engine(definition, rules.Reverse().ToArray());
                var restored = Restore(source);
                actual = LabRunner.Project(second, restored, new[] { 9L, 10L, 10L }, forecast: true);
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
            Assert.That(actual.Trace.Select(e => e.Request.TypeId), Is.EqualTo(new[] { "A", "C", "E", "D", "B" }));
            Assert.That(LabReport.Difference(expected, actual), Is.Null);
            Report("causal-chain", initial, LabReport.Authoritative(expected), new XElement("forecast", LabReport.Authoritative(actual)));
        }

        [Test]
        public void FakeClockReturnToMaximumDoesNotReplayEventsAndLaterProgressResumes()
        {
            var engine = Engine(Config(), Rule("mark"));
            var clock = new FakeClock(At(0));
            var source = engine.Initialize(At(0), null, 42, new[] { Event(10), Event(12) }).State;
            var state = source;
            long[] targets = { 10, 10, 2, 10, 12 };
            var trace = new System.Collections.Generic.List<ScheduledEvent>();
            foreach (var target in targets)
            {
                clock.Set(At(target));
                var step = engine.AdvanceTo(state, clock.GetLogicalTime());
                state = step.State;
                trace.AddRange(step.Trace);
            }
            SameState(engine.AdvanceTo(source, At(12)).State, state);
            Assert.That(trace.Select(e => e.Sequence), Is.EqualTo(new[] { 0L, 1L }));
        }
    }
}
