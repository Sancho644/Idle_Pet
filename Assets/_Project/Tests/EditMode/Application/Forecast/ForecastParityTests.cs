using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Application.Forecast;
using IdlePet.Domain;
using IdlePet.Domain.Pet;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Random;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Application.Forecast.ForecastTestData;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;
using Effect = IdlePet.Domain.Simulation.Rules.SimulationRuleEffect;

namespace IdlePet.Tests.EditMode.Application.Forecast
{
    public sealed class ForecastParityTests
    {
        private const long Year = 31_536_000_000L;
        private const long Far = 34_560_000_000L;
        private const uint Bound = 0x80000001u;

        [TestCase(60_000L)]
        [TestCase(3_600_000L)]
        [TestCase(28_800_000L)]
        [TestCase(86_400_000L)]
        [TestCase(604_800_000L)]
        [TestCase(Year)]
        public void ForecastMatchesDirectAdvanceWithFixedPointThresholdsAndRealRejectionSampling(long time)
        {
            var (engine, source) = MixedWorld();
            var before = Restore(source);
            var forecast = new ForecastService(engine).Forecast(source, At(time));
            var actual = engine.AdvanceTo(source, At(time));

            Matches(actual, forecast);
            SameState(before, source);
            Assert.That(forecast.State, Is.Not.SameAs(source));
            Assert.That(forecast.State.ActivePet.TerminatedAt, Is.Null);
            var atThreshold = forecast.Trace.Where(e => e.Request.Timestamp.Milliseconds == 6).ToArray();
            Assert.That(atThreshold.Select(e => e.Request.TypeId),
                Is.EqualTo(new[] { "need.threshold", "need.threshold", "A", "C", "B" }));
            Assert.That(atThreshold.Take(2).Select(e => e.Request.Payload.NeedId),
                Is.EquivalentTo(new[] { NeedId.Satiety, NeedId.Energy }));
            var firstRaw = RandomStreams.Restore(source.Rng).NextUInt32(RandomStreamId.Behaviour);
            Assert.That(firstRaw, Is.LessThan((uint)((1UL << 32) % Bound)), "The first raw result must be rejected.");
            var expected = RandomStreams.Restore(source.Rng);
            expected.NextBounded(RandomStreamId.Behaviour, Bound);
            expected.NextUInt32(RandomStreamId.Birth);
            expected.NextUInt32(RandomStreamId.Illness);
            expected.NextUInt32(RandomStreamId.Evolution);
            Assert.That(forecast.State.Rng.Streams, Is.EqualTo(expected.CaptureSnapshot().Streams));
            Assert.That(forecast.State.ActiveModifierIds, Is.Empty);
            Assert.That(source.EventQueue.Events.Any(e => e.Request.TypeId == "cancelled"), Is.True);
            Assert.That(forecast.Trace.Any(e => e.Request.TypeId == "cancelled"), Is.False);
            Assert.That(forecast.State.EventQueue.Events.Any(e => e.Request.TypeId == "cancelled"), Is.False);
            Assert.That(forecast.State.EventQueue.Events.Select(e => e.Request.Timestamp.Milliseconds), Has.Member(Far + 1));
            Assert.That(forecast.Trace.Count(e => e.Request.Timestamp.Milliseconds == 60_000), Is.EqualTo(1));
            var afterRightEdge = forecast.State.EventQueue.Events.Any(e => e.Request.Timestamp.Milliseconds == 60_001);
            Assert.That(afterRightEdge, Is.EqualTo(time == 60_000));
            if (time == Year)
            {
                var original = State("initial", before);
                var sourceAfter = State("source-after", source);
                var predicted = ForecastRecord("forecast", forecast);
                var control = Result("control", actual);
                Report("forecast-vs-advance-365-days", original, sourceAfter, predicted, control);
                Report("rng-isolation", original, sourceAfter, predicted, control);
                Report("queue-isolation", original, sourceAfter, predicted, control);
                Report("coincident-thresholds-and-causal-chain", original, sourceAfter, predicted, control);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RepeatedForecastsAndChildBranchesAreIndependentInEitherOrder(bool longFirst)
        {
            var (engine, source) = MixedWorld();
            var before = Restore(source);
            var service = new ForecastService(engine);
            var first = service.Forecast(source, At(longFirst ? Year : 60_000));
            var firstBefore = Restore(first.State);
            var second = service.Forecast(source, At(longFirst ? 60_000 : Year));
            var repeated = service.Forecast(source, first.Target);
            var shortResult = longFirst ? second : first;
            var longResult = longFirst ? first : second;
            var child = service.Forecast(shortResult.State, longResult.Target);
            var changed = engine.AdvanceTo(source, At(10));
            var fromChanged = service.Forecast(changed.State, longResult.Target);

            var actual = engine.AdvanceTo(source, longResult.Target);
            Matches(actual, longResult);
            Matches(engine.AdvanceTo(source, shortResult.Target), shortResult);
            SameState(firstBefore, first.State);
            SameState(first.State, repeated.State);
            SameTrace(first.Trace, repeated.Trace);
            Assert.That(first.State, Is.Not.SameAs(repeated.State));
            SameState(longResult.State, child.State);
            SameTrace(longResult.Trace, shortResult.Trace.Concat(child.Trace));
            SameState(longResult.State, fromChanged.State);
            SameTrace(longResult.Trace, changed.Trace.Concat(fromChanged.Trace));
            SameState(before, source);
            if (longFirst)
            {
                Report("independent-forecasts", State("initial", before), State("source-after", source),
                    ForecastRecord("short", shortResult), ForecastRecord("long", longResult),
                    ForecastRecord("repeated", repeated), ForecastRecord("child", child),
                    Result("control", actual));
            }
        }

        [Test]
        public void PartitionedForecastAndAdvanceHaveTheSameStateAndCombinedTrace()
        {
            var (engine, source) = MixedWorld(terminal: true);
            var before = Restore(source);
            var service = new ForecastService(engine);
            var full = service.Forecast(source, At(Year));
            var fullControl = engine.AdvanceTo(source, At(Year));
            long[] cuts = { 2, 5, 6, 7, 9, 13, 18, 20, 20, 21, 60_000, 60_001, Year };
            var forecastTrace = new List<ScheduledEvent>();
            var controlTrace = new List<ScheduledEvent>();
            var branch = source;
            var control = source;
            foreach (long cut in cuts)
            {
                var partial = service.Forecast(branch, At(cut));
                var actual = engine.AdvanceTo(control, At(cut));
                Matches(actual, partial);
                branch = partial.State;
                control = actual.State;
                forecastTrace.AddRange(partial.Trace);
                controlTrace.AddRange(actual.Trace);
            }

            Matches(fullControl, full);
            SameState(full.State, branch);
            SameState(full.State, control);
            SameTrace(full.Trace, forecastTrace);
            SameTrace(full.Trace, controlTrace);
            SameState(before, source);
            Assert.That(branch.ActivePet.TerminatedAt, Is.EqualTo(At(20)));
            Report("forecast-partition-parity", State("initial", before), State("source-after", source),
                ForecastRecord("forecast", full), Result("control", fullControl), State("partitioned-forecast", branch),
                State("partitioned-control", control), Trace("forecast-partition-trace", forecastTrace),
                Trace("control-partition-trace", controlTrace));
        }

        private static (SimulationEngine, GameState) MixedWorld(bool terminal = false)
        {
            var configuration = Config(rates: id => id == NeedId.Satiety || id == NeedId.Energy ? -5 : 1,
                thresholds: id => id == NeedId.Satiety || id == NeedId.Energy ? new[] { 75L } : Array.Empty<long>(),
                modifiers: new[] { Scale("accelerate", 2_000_000) });
            var pet = new PetState(Pet, NeedsState.Restore(configuration.Needs,
                configuration.Needs.Entries.Keys.Select(id => KeyValuePair.Create(id, Exact(101)))));
            var rules = new[]
            {
                Rule("prime", RandomStreamRegistry.OrderedIds.Select(Effect.Draw).ToArray()),
                Rule("prime.secondary", Effect.Draw(RandomStreamId.Lifecycle)),
                Rule("A", Effect.DrawBounded(RandomStreamId.Behaviour, Bound), Effect.Draw(RandomStreamId.Birth),
                    Effect.Schedule(Event(6, "C", 5)), Effect.Schedule(Event(Far + 1, text: "created in branch"))),
                Rule("B", Effect.Draw(RandomStreamId.Evolution)),
                Rule("C", Effect.Draw(RandomStreamId.Illness)),
                Rule("on", Effect.SetModifier("accelerate", true)),
                Rule("off", Effect.SetModifier("accelerate", false)),
                Rule("cancel", Effect.Cancel(6)),
                Rule("cancelled", Effect.Draw(RandomStreamId.Digestion)),
                Rule("end", Effect.Terminate(Pet)),
                Rule("mark")
            };
            var requests = new List<EventRequest>
            {
                Event(0, "prime"), Event(6, "A", 10), Event(6, "B", 20), Event(9, "on"), Event(13, "off"),
                Event(18, "cancel"), Event(22, "cancelled"), Event(Far, text: "original future"),
                Event(60_000), Event(60_001), Event(1, "prime.secondary"), Event(2, "prime.secondary")
            };
            if (terminal)
            {
                requests.Add(Event(20, "end", pet: Pet));
            }
            var engine = Engine(configuration, rules);
            var initialized = engine.Initialize(At(0), pet, 42, requests);
            Assert.That(initialized.Status, Is.EqualTo(SimulationStatus.Initialized));
            var preForecast = engine.AdvanceTo(initialized.State, At(2));
            Assert.That(preForecast.Status, Is.EqualTo(SimulationStatus.Advanced));
            Assert.That(preForecast.Trace.Select(entry => entry.Request.TypeId),
                Is.EqualTo(new[] { "prime.secondary", "prime.secondary" }));
            Assert.That(preForecast.State.ProcessedEventsAtCursor, Is.EqualTo(1));
            return (engine, preForecast.State);
        }
    }
}
