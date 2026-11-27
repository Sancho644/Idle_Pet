using System;
using System.Linq;
using IdlePet.Domain;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Random;
using IdlePet.Domain.Simulation.Time;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;

namespace IdlePet.Tests.EditMode.Domain.Simulation.Engine
{
    public sealed class SimulationTransactionTests
    {
        [Test]
        public void RollbackRestoresNeedsQueueSequenceModifiersAndAllRngAfterACommittedEarlierEvent()
        {
            var cfg = Config(rates: id => id == NeedId.Satiety ? -1 : 0, modifiers: new[] { Scale("twice", 2_000_000) });
            var failing = Build(true);
            var successful = Build(false);
            var initial = failing.Initialize(At(0), Living(cfg), 42,
                new[] { Event(10, "A"), Event(20, "B", pet: Pet), Event(21, "future") }).State;
            var first = failing.AdvanceTo(initial, At(10));
            var fault = failing.AdvanceTo(initial, At(30));

            Assert.That(fault.Status, Is.EqualTo(SimulationStatus.Fault));
            Assert.That(fault.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.RuleFailure));
            Assert.That(fault.Diagnostic.Event.Request.TypeId, Is.EqualTo("bad"));
            Assert.That(fault.Diagnostic.Timestamp.Milliseconds, Is.EqualTo(20));
            Assert.That(fault.Trace.Single().Request.TypeId, Is.EqualTo("A"));
            SameState(first.State, fault.State);
            var retry = failing.AdvanceTo(Restore(fault.State), At(30));
            Assert.That(retry.Diagnostic.Event.Sequence, Is.EqualTo(fault.Diagnostic.Event.Sequence));
            Assert.That(retry.Diagnostic.Reason, Is.EqualTo(fault.Diagnostic.Reason));
            Assert.That(retry.Trace, Is.Empty);
            SameState(first.State, retry.State);

            var repaired = successful.AdvanceTo(Restore(fault.State), At(30));
            var control = successful.AdvanceTo(initial, At(30));
            Assert.That(repaired.Status, Is.EqualTo(SimulationStatus.Advanced));
            SameState(control.State, repaired.State);
            SameTrace(control.Trace, first.Trace.Concat(repaired.Trace));
            Assert.That(repaired.Trace.Any(entry => entry.Request.TypeId == "A"), Is.False);
            Evidence("rollback-after-rng-and-queue", initial, fault, repaired);

            SimulationEngine Build(bool fail) => CreateEngine(cfg,
                ("A", (c, _) => c.NextUInt32(RandomStreamId.Illness)),
                ("B", (c, _) =>
                {
                    c.SetNeed(Pet, NeedId.Satiety, Exact(77));
                    c.NextUInt32(RandomStreamId.Birth);
                    c.NextBounded(RandomStreamId.Behaviour, 10);
                    c.SetModifierActive("twice", true);
                    c.Schedule(Event(20, "bad", 3));
                    c.Schedule(Event(23, "future"));
                }),
                ("future", (c, _) => c.NextUInt32(RandomStreamId.Digestion)),
                ("bad", (c, _) =>
                {
                    c.NextUInt32(RandomStreamId.Lifecycle);
                    if (fail)
                    {
                        throw new SimulationRuleException("test failure");
                    }
                }));
        }

        [TestCase(1, 1, 2L, true)]
        [TestCase(1, 2, 2L, false)]
        [TestCase(4, 4, 2L, true)]
        [TestCase(4, 5, 2L, false)]
        public void ConfiguredBudgetAllowsExactlyTheLimitForAnEntireCausalChain(int limit, int events, long timestamp, bool success)
        {
            var cfg = Config(limit);
            var engine = CreateEngine(cfg, ("chain", (context, entry) =>
            {
                context.NextUInt32(RandomStreamId.Birth);
                int left = int.Parse(entry.Request.Payload.Text, System.Globalization.CultureInfo.InvariantCulture);
                if (left > 1)
                {
                    context.Schedule(Event(context.Time.Milliseconds, "chain", entry.Request.Priority - 1, text: (left - 1).ToString()));
                }
            }));
            var initial = engine.Initialize(At(0), null, 42, new[] { Event(timestamp, "chain", text: events.ToString()) }).State;
            var result = engine.AdvanceTo(initial, At(timestamp));

            if (success)
            {
                Assert.That(result.Status, Is.EqualTo(SimulationStatus.Advanced));
                Assert.That(result.Trace, Has.Count.EqualTo(events));
                Assert.That(result.State.ProcessedEventsAtCursor, Is.EqualTo(events));
            }
            else
            {
                Assert.That(result.Status, Is.EqualTo(SimulationStatus.Fault));
                Assert.That(result.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.EventLimit));
                Assert.That(result.Diagnostic.Limit, Is.EqualTo(limit));
                Assert.That(result.Trace, Is.Empty);
                SameState(initial, result.State);
                if (limit == 4)
                {
                    Evidence("timestamp-limit", initial, result);
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DirectAndIndirectCyclesFailWithoutPublishingEvenAtInitialization(bool indirect)
        {
            var engine = CreateEngine(Config(5), ("A", (c, _) =>
            {
                c.NextUInt32(RandomStreamId.Birth);
                c.Schedule(Event(c.Time.Milliseconds, indirect ? "B" : "A"));
            }), ("B", (c, _) => c.Schedule(Event(c.Time.Milliseconds, "A"))));
            var result = engine.Initialize(At(0), null, 42, new[] { Event(0, "A") });

            AssertInitializationFailed(result);
            Assert.That(result.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.EventLimit));
            Assert.That(result.Diagnostic.Event.Sequence, Is.EqualTo(5));
            Assert.That(result.Diagnostic.Limit, Is.EqualTo(5));
        }

        [Test]
        public void InvalidStateAndRateOverflowAreDiagnosedBeforeAnySegmentCommits()
        {
            var cfg = Config(rates: id => id == NeedId.Satiety ? long.MaxValue : 0,
                modifiers: new[] { Scale("twice", 2_000_000) });
            var initial = new GameState(0, Living(cfg), 42);
            var engine = CreateEngine(cfg);
            var invalid = GameState.Restore(0, initial.ActivePet, initial.Rng, EventQueueSnapshot.Empty, new[] { "unknown" }, 0);
            var highRate = GameState.Restore(0, initial.ActivePet, initial.Rng, EventQueueSnapshot.Empty, new[] { "twice" }, 0);

            var first = engine.AdvanceTo(invalid, At(10));
            Assert.That(first.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.InvalidState));
            SameState(invalid, first.State);
            var second = engine.AdvanceTo(highRate, At(10));
            Assert.That(second.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.Overflow));
            Assert.That(second.CommittedTransactions, Is.Zero);
            SameState(highRate, second.State);
        }

        [Test]
        public void AModifierIntroducedInATransactionIsValidatedBeforePublication()
        {
            var cfg = Config(rates: id => id == NeedId.Satiety ? long.MaxValue : 0,
                modifiers: new[] { Scale("twice", 2_000_000) });
            var engine = CreateEngine(cfg, ("boost", (c, _) => c.SetModifierActive("twice", true)));
            var first = engine.Initialize(At(0), Living(cfg), 42, new[] { Event(1, "boost") }).State;

            var fault = engine.AdvanceTo(first, At(1));

            Assert.That(fault.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.Overflow));
            Assert.That(fault.Trace, Is.Empty);
            SameState(first, fault.State);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FutureTimeOverflowAndEventSequenceOverflowRollBackTheTimestamp(bool sequence)
        {
            var engine = CreateEngine(Config(), ("overflow", (context, _) =>
            {
                context.NextUInt32(RandomStreamId.Birth);
                var next = sequence ? context.Time : context.Time.Add(new LogicalDuration(1));
                context.Schedule(Event(next.Milliseconds, "overflow"));
            }));
            long time = sequence ? 1 : long.MaxValue;
            var seeded = new GameState(time - 1, null, 42);
            var queue = new EventQueueSnapshot(new[] { new ScheduledEvent(Event(time, "overflow"), long.MaxValue - 1) }, long.MaxValue);
            var state = GameState.Restore(time - 1, null, seeded.Rng, queue, Array.Empty<string>(), 0);

            var result = engine.AdvanceTo(state, At(time));

            Assert.That(result.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.Overflow));
            Assert.That(result.Diagnostic.Event.Sequence, Is.EqualTo(long.MaxValue - 1));
            Assert.That(result.Trace, Is.Empty);
            SameState(state, result.State);
        }

        [Test]
        public void CapturedContextCannotReadOrMutateAfterCommit()
        {
            SimulationEventContext captured = null;
            var engine = CreateEngine(Config(), ("keep", (context, _) => captured = context));
            var initialized = engine.Initialize(At(0), null, 42, new[] { Event(0, "keep") });
            Assert.That(initialized.Status, Is.EqualTo(SimulationStatus.Initialized));

            Assert.Throws<InvalidOperationException>(() => captured.NextUInt32(RandomStreamId.Birth));
            Assert.Throws<InvalidOperationException>(() => captured.Schedule(Event(1, "keep")));
            Assert.Throws<InvalidOperationException>(() => _ = captured.Time);
        }

        [Test]
        public void OrdinaryHandlerErrorsAreDiagnosticButFatalFailuresPropagate()
        {
            var engine = CreateEngine(Config(), ("ordinary", (c, _) =>
            {
                c.NextUInt32(RandomStreamId.Birth);
                throw new Exception("ordinary fixture failure");
            }), ("fatal", (_, _) => throw new OutOfMemoryException("fatal fixture failure")));
            var ordinary = engine.Initialize(At(0), null, 42, new[] { Event(1, "ordinary") }).State;
            var fatal = engine.Initialize(At(0), null, 42, new[] { Event(1, "fatal") }).State;

            var result = engine.AdvanceTo(ordinary, At(3));

            Assert.That(result.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.RuleFailure));
            Assert.That(result.Diagnostic.Reason, Is.EqualTo("ordinary fixture failure"));
            SameState(ordinary, result.State);
            Assert.Throws<OutOfMemoryException>(() => engine.AdvanceTo(fatal, At(3)));
        }

        [TestCase("past", SimulationFaultCode.PastEvent)]
        [TestCase("unknown", SimulationFaultCode.UnknownEventType)]
        public void InvalidEventRegistrationRollsBackRatherThanSkippingTheFailedEvent(string kind, SimulationFaultCode expected)
        {
            var engine = CreateEngine(Config(), ("attempt", (context, _) =>
                context.Schedule(kind == "past" ? Event(0, "attempt") : Event(2, "unknown"))));
            var initial = engine.Initialize(At(0), null, 42, new[] { Event(1, "attempt") }).State;

            var fault = engine.AdvanceTo(initial, At(5));

            Assert.That(fault.Diagnostic.Code, Is.EqualTo(expected));
            Assert.That(fault.Trace, Is.Empty);
            SameState(initial, fault.State);
        }
    }
}
