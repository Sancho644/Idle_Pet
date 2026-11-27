using System;
using System.Linq;
using IdlePet.Application.Forecast;
using IdlePet.Domain;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Random;
using IdlePet.Domain.Simulation.Time;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Application.Forecast.ForecastTestData;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;
using Effect = IdlePet.Domain.Simulation.Rules.SimulationRuleEffect;

namespace IdlePet.Tests.EditMode.Application.Forecast
{
    public sealed class ForecastFailureTests
    {
        [TestCase(4L, SimulationStatus.BackwardTarget)]
        [TestCase(5L, SimulationStatus.NoChange)]
        [TestCase(7L, SimulationStatus.Advanced)]
        public void ForecastsAnEmptyWorldAtAllTimeBoundaries(long target, SimulationStatus status)
        {
            var engine = new SimulationEngine(Config());
            var service = new ForecastService(engine);
            var source = new GameState(5, null, 0);
            var before = Restore(source);

            var result = service.Forecast(source, At(target));

            Matches(engine.AdvanceTo(source, At(target)), result);
            Assert.That(result.Status, Is.EqualTo(status));
            Assert.That(result.State, Is.Not.SameAs(source));
            Assert.That(result.State.ActivePet, Is.Null);
            Assert.That(result.Trace, Is.Empty);
            SameState(before, source);
            if (target >= 5)
            {
                Matches(engine.AdvanceTo(source, At(target)), service.ForecastFor(source, new LogicalDuration(target - 5)));
            }
        }

        [Test]
        public void MissingWorldAndUnrepresentableTargetFailExplicitly()
        {
            var engine = new SimulationEngine(Config());
            var service = new ForecastService(engine);
            var source = new GameState(long.MaxValue, null, 42);
            var before = Restore(source);
            Assert.Throws<ArgumentNullException>(() => new ForecastService(null));
            Assert.Throws<ArgumentNullException>(() => service.Forecast(null, At(7)));
            Assert.Throws<ArgumentNullException>(() => service.ForecastFor(null, new LogicalDuration(0)));
            Assert.Throws<OverflowException>(() => service.ForecastFor(source, new LogicalDuration(1)));
            SameState(before, source);
        }

        [TestCase(4L, false)]
        [TestCase(5L, false)]
        [TestCase(10L, false)]
        [TestCase(5L, true)]
        [TestCase(10L, true)]
        public void InvalidInputHasDiagnosticBeforeAnyProgress(long target, bool unknownEvent)
        {
            var cfg = Config();
            var engine = new SimulationEngine(cfg);
            var seed = new GameState(5, Living(cfg), 42);
            var queue = unknownEvent
                ? new EventQueueSnapshot(new[] { new ScheduledEvent(Event(8, "unknown", text: "data"), 3) }, 4)
                : EventQueueSnapshot.Empty;
            var source = GameState.Restore(5, seed.ActivePet, seed.Rng, queue,
                unknownEvent ? Array.Empty<string>() : new[] { "unknown" }, 0);
            var before = Restore(source);

            var result = new ForecastService(engine).Forecast(source, At(target));

            Matches(engine.AdvanceTo(source, At(target)), result);
            Assert.That(result.Status, Is.EqualTo(SimulationStatus.Fault));
            Assert.That(result.Diagnostic.Code, Is.EqualTo(unknownEvent ? SimulationFaultCode.UnknownEventType : SimulationFaultCode.InvalidState));
            Assert.That(result.Diagnostic.IsInputStateFailure, Is.True);
            Assert.That(result.Reached, Is.EqualTo(At(5)));
            Assert.That(result.State, Is.Not.SameAs(source));
            Assert.That(result.CommittedTransactions, Is.Zero);
            Assert.That(result.Trace, Is.Empty);
            SameState(before, result.State);
            SameState(before, source);
        }

        [Test]
        public void FailedForecastDiscardsNeedsRngAndScheduledEventsAtTheFaultingTimestamp()
        {
            var cfg = Config(rates: _ => 1, modifiers: new[] { Scale("changed", 2_000_000) });
            var engine = Engine(cfg, Rule("good", Effect.Draw(RandomStreamId.Behaviour)),
                Rule("change", Effect.SetNeed(Pet, NeedId.Bond, Exact(0)), Effect.SetModifier("changed", true),
                    Effect.Draw(RandomStreamId.Birth), Effect.DrawBounded(RandomStreamId.Illness, 17),
                    Effect.Schedule(Event(4, "bad")), Effect.Schedule(Event(12))),
                Rule("bad", Effect.Fail("forecast fixture fault")), Rule("mark"));
            var source = engine.Initialize(At(0), Living(cfg), 42,
                new[] { Event(2, "good"), Event(4, "change", pet: Pet), Event(7) }).State;
            var before = Restore(source);
            var service = new ForecastService(engine);
            var result = service.Forecast(source, At(10));
            var repeat = service.Forecast(source, At(10));
            var actual = engine.AdvanceTo(source, At(10));
            var lastStable = engine.AdvanceTo(source, At(2));

            Matches(actual, result);
            Matches(actual, repeat);
            Assert.That(result.Status, Is.EqualTo(SimulationStatus.Fault));
            Assert.That(result.Diagnostic.IsInputStateFailure, Is.False);
            Assert.That(result.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.RuleFailure));
            Assert.That(result.Diagnostic.Event.Request.TypeId, Is.EqualTo("bad"));
            Assert.That(result.Reached, Is.EqualTo(At(2)));
            Assert.That(result.Trace.Select(e => e.Request.TypeId), Is.EqualTo(new[] { "good" }));
            SameState(lastStable.State, result.State);
            SameState(before, source);
            Report("forecast-fault-after-rng-and-scheduling", State("initial", before), State("source-after", source),
                ForecastRecord("forecast", result), Result("control", actual), Result("last-stable-control", lastStable));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DirectAndIndirectCyclesExhaustOnlyTheForecastBranchBudget(bool indirect)
        {
            var cfg = Config(limit: 3);
            var engine = Engine(cfg, Rule("A", Effect.Draw(RandomStreamId.Birth), Effect.Schedule(Event(3, indirect ? "B" : "A"))),
                Rule("B", Effect.Schedule(Event(3, "A"))));
            var source = engine.Initialize(At(0), null, 42, new[] { Event(3, "A") }).State;
            var before = Restore(source);

            var result = new ForecastService(engine).Forecast(source, At(3));

            Matches(engine.AdvanceTo(source, At(3)), result);
            Assert.That(result.Status, Is.EqualTo(SimulationStatus.Fault));
            Assert.That(result.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.EventLimit));
            Assert.That(result.Diagnostic.Limit, Is.EqualTo(3));
            Assert.That(result.Diagnostic.Event.Sequence, Is.EqualTo(3));
            Assert.That(result.Trace, Is.Empty);
            SameState(before, result.State);
            SameState(before, source);
        }

        [Test]
        public void RollbackRestoresLivingPetAndItsEventsAfterAProvisionalTermination()
        {
            var cfg = Config(rates: _ => 1);
            var engine = Engine(cfg, Rule("end", Effect.Terminate(Pet)),
                Rule("bad", Effect.Fail("failure after termination")), Rule("life"));
            var source = engine.Initialize(At(0), Living(cfg), 42,
                new[] { Event(4, "end", 1, pet: Pet), Event(4, "bad", 2), Event(5, "life", pet: Pet) }).State;
            var before = Restore(source);

            var result = new ForecastService(engine).Forecast(source, At(10));

            Matches(engine.AdvanceTo(source, At(10)), result);
            Assert.That(result.Status, Is.EqualTo(SimulationStatus.Fault));
            Assert.That(result.Diagnostic.Event.Request.TypeId, Is.EqualTo("bad"));
            Assert.That(result.State.ActivePet.TerminatedAt, Is.Null);
            Assert.That(result.State.EventQueue.Events, Has.Count.EqualTo(3));
            SameState(before, result.State);
            SameState(before, source);
        }

        [TestCase(4L)]
        [TestCase(8L)]
        public void TerminalForecastStopsLifeAndContinuesWorldWithoutChangingTheRealPet(long time)
        {
            var cfg = Config(rates: id => id == NeedId.Satiety ? -2 : 0);
            var engine = Engine(cfg, Rule("end", Effect.Terminate(Pet)), Rule("life", Effect.Draw(RandomStreamId.Birth)),
                Rule("world", Effect.Draw(RandomStreamId.Digestion)));
            var source = engine.Initialize(At(0), Living(cfg), 42, new[]
            {
                Event(3, "life", pet: Pet), Event(4, "end", 1, pet: Pet), Event(4, "life", pet: Pet),
                Event(5, "life", pet: Pet), Event(7, "world")
            }).State;
            var before = Restore(source);
            var service = new ForecastService(engine);

            var result = service.Forecast(source, At(time));
            var alreadyTerminal = service.Forecast(result.State, At(9));
            var actual = engine.AdvanceTo(source, At(time));

            Matches(actual, result);
            Matches(engine.AdvanceTo(actual.State, At(9)), alreadyTerminal);
            Assert.That(result.State.ActivePet.Id, Is.EqualTo(Pet));
            Assert.That(result.State.ActivePet.TerminatedAt, Is.EqualTo(At(4)));
            Assert.That(result.State.ActivePet.Needs[NeedId.Satiety].ExactNumerator, Is.EqualTo(92));
            Assert.That(result.State.EventQueue.Events.All(e => e.Request.Scope == EventScope.World), Is.True);
            Assert.That(result.Trace.Select(e => e.Request.TypeId),
                Is.EqualTo(time == 4 ? new[] { "life", "end" } : new[] { "life", "end", "world" }));
            var expected = RandomStreams.Restore(source.Rng);
            expected.NextUInt32(RandomStreamId.Birth);
            if (time == 8)
            {
                expected.NextUInt32(RandomStreamId.Digestion);
            }

            Assert.That(result.State.Rng.Streams, Is.EqualTo(expected.CaptureSnapshot().Streams));
            Assert.That(alreadyTerminal.State.ActivePet.Needs[NeedId.Satiety].ExactNumerator, Is.EqualTo(92));
            Assert.That(alreadyTerminal.Reached, Is.EqualTo(At(9)));
            Assert.That(source.ActivePet.TerminatedAt, Is.Null);
            SameState(before, source);
            if (time == 8)
            {
                Report("forecast-terminal", State("initial", before), State("source-after", source),
                    ForecastRecord("forecast", result), Result("control", actual), ForecastRecord("continued", alreadyTerminal));
            }
        }
    }
}
