using System;
using System.Linq;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Random;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;

namespace IdlePet.Tests.EditMode.Domain.Simulation.Engine
{
    public sealed class SimulationCommandTests
    {
        [Test]
        public void RejectionObservesNaturalProgressAndMatchesAdvanceWithoutTheCommand()
        {
            var cfg = Config(rates: id => id == NeedId.Satiety ? -1 : 0, modifiers: new[] { Scale("zero", 0) });
            var engine = CreateEngine(cfg, ("natural", (c, _) =>
            {
                c.SetModifierActive("zero", true);
                c.NextUInt32(RandomStreamId.Illness);
                c.Schedule(Event(12, "natural"));
            }));
            var initial = engine.Initialize(At(0), Living(cfg), 42, new[] { Event(5, "natural") }).State;
            var control = engine.AdvanceTo(initial, At(10));
            var rejected = engine.ExecuteCommand(initial, At(10), new Command(
                state => state.ActivePet.Needs[NeedId.Satiety].ExactNumerator < 100 ? "test.depleted" : null,
                _ => Assert.Fail("A rejected command must not apply any effect.")));

            Assert.That(rejected.Status, Is.EqualTo(SimulationStatus.CommandRejected));
            Assert.That(rejected.RejectionReason, Is.EqualTo("test.depleted"));
            SameState(control.State, rejected.State);
            SameTrace(control.Trace, rejected.Trace);
            Assert.That(rejected.State.EventQueue.NextSequence, Is.EqualTo(2));
            Evidence("r04-rejection", initial, rejected, control);
        }

        [TestCase(0L)]
        [TestCase(6L)]
        public void AcceptedCommandStabilizesSameTimeChildrenAndMayUseRng(long time)
        {
            var cfg = Config();
            var engine = CreateEngine(cfg, ("child", (c, _) =>
            {
                c.NextUInt32(RandomStreamId.Digestion);
                c.SetNeed(Pet, NeedId.Satiety, Exact(333));
            }));
            var start = engine.Initialize(At(0), Living(cfg), 42, Array.Empty<IdlePet.Domain.Simulation.Events.EventRequest>()).State;
            var result = engine.ExecuteCommand(start, At(time), new Command(
                state =>
                {
                    Assert.That(state.LogicalTimeMilliseconds, Is.EqualTo(time));
                    return null;
                },
                c =>
                {
                    c.NextUInt32(RandomStreamId.Birth);
                    c.Schedule(Event(c.Time.Milliseconds, "child", pet: Pet));
                }));

            Assert.That(result.Status, Is.EqualTo(SimulationStatus.CommandApplied));
            Assert.That(result.Trace.Single().Request.TypeId, Is.EqualTo("child"));
            Assert.That(result.State.ActivePet.Needs[NeedId.Satiety].ExactNumerator, Is.EqualTo(333));
            Assert.That(result.State.Rng.Streams, Is.Not.EqualTo(start.Rng.Streams));
        }

        [Test]
        public void BackdatedCommandIsRejectedWithoutValidationOrExecution()
        {
            var engine = CreateEngine(Config());
            var before = new IdlePet.Domain.GameState(5, null, 42);
            var result = engine.ExecuteCommand(before, At(4), new Command(
                _ => throw new AssertionException("Validation should not be called."),
                _ => throw new AssertionException("Apply should not be called.")));

            Assert.That(result.Status, Is.EqualTo(SimulationStatus.CommandRejected));
            Assert.That(result.RejectionReason, Is.EqualTo("command.past"));
            Assert.That(result.State, Is.SameAs(before));
        }

        [Test]
        public void CommandFailureKeepsTheNaturalCheckpointAndTrace()
        {
            var cfg = Config(rates: _ => 1);
            var engine = CreateEngine(cfg, ("mark", (c, _) => c.NextUInt32(RandomStreamId.Evolution)),
                ("fail", (_, _) => throw new SimulationRuleException("fault in command child")));
            var initial = engine.Initialize(At(0), Living(cfg), 42, new[] { Event(5) }).State;
            var control = engine.AdvanceTo(initial, At(10));

            var fault = engine.ExecuteCommand(initial, At(10), new Command(_ => null, c =>
            {
                c.NextUInt32(RandomStreamId.Behaviour);
                c.SetNeed(Pet, NeedId.Bond, Exact(950));
                c.Schedule(Event(10, "fail"));
            }));

            Assert.That(fault.Status, Is.EqualTo(SimulationStatus.Fault));
            Assert.That(fault.Diagnostic.Event.Request.TypeId, Is.EqualTo("fail"));
            SameState(control.State, fault.State);
            SameTrace(control.Trace, fault.Trace);
        }

        [Test]
        public void NaturalFailureDoesNotValidateOrApplyTheCommand()
        {
            var cfg = Config();
            var engine = CreateEngine(cfg, ("mark", (_, _) => throw new SimulationRuleException("natural failure")));
            var initial = engine.Initialize(At(0), null, 42, new[] { Event(1) }).State;
            var result = engine.ExecuteCommand(initial, At(2), new Command(
                _ => throw new AssertionException("Unexpected validation."),
                _ => throw new AssertionException("Unexpected application.")));

            Assert.That(result.Diagnostic.Event.Request.TypeId, Is.EqualTo("mark"));
            SameState(initial, result.State);
        }

        [Test]
        public void IndependentCommandsAtOneTimestampShareTheCommittedBudgetEvenAfterRestore()
        {
            var cfg = Config(2);
            var engine = CreateEngine(cfg, ("mark", (_, _) => { }));
            var initial = engine.Initialize(At(0), null, 42, new[] { Event(3) }).State;
            var command = new Command(_ => null, c => c.Schedule(Event(c.Time.Milliseconds)));
            var first = engine.ExecuteCommand(initial, At(3), command);
            var second = engine.ExecuteCommand(Restore(first.State), At(3), command);
            var later = engine.ExecuteCommand(second.State, At(4), command);

            Assert.That(first.Status, Is.EqualTo(SimulationStatus.CommandApplied));
            Assert.That(first.Trace, Has.Count.EqualTo(2));
            Assert.That(first.State.ProcessedEventsAtCursor, Is.EqualTo(2));
            Assert.That(second.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.EventLimit));
            SameState(first.State, second.State);
            Assert.That(second.Trace, Is.Empty);
            Assert.That(later.Status, Is.EqualTo(SimulationStatus.CommandApplied));
            Assert.That(later.State.ProcessedEventsAtCursor, Is.EqualTo(1));
        }
    }
}
