using IdlePet.Domain;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Random;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;

namespace IdlePet.Tests.EditMode.Domain.Simulation.Engine
{
    public sealed class SimulationInputValidationTests
    {
        [TestCase(5L, SimulationStatus.NoChange)]
        [TestCase(4L, SimulationStatus.BackwardTarget)]
        public void ValidSnapshotKeepsItsStateAndTraceForEqualAndPastTargets(long target, SimulationStatus status)
        {
            var configuration = Configuration();
            var state = Snapshot(configuration);
            var before = Restore(state);
            var engine = CreateEngine(configuration, ("mark", (_, _) => Assert.Fail("A pending event must stay pending.")));

            var result = engine.AdvanceTo(state, At(target));

            Assert.That(result.Status, Is.EqualTo(status));
            Assert.That(result.Diagnostic, Is.Null);
            AssertNoProgress(state, before, result);
        }

        [TestCase(4L)]
        [TestCase(5L)]
        [TestCase(12L)]
        public void UnknownModifierIsDiagnosticForEveryTarget(long target)
        {
            var configuration = Configuration();
            var state = Snapshot(configuration, modifier: "missing");
            var before = Restore(state);
            var engine = CreateEngine(configuration, ("mark", (_, _) => Assert.Fail("An invalid world must not advance.")));

            var result = engine.AdvanceTo(state, At(target));

            AssertValidationFault(state, before, result, SimulationFaultCode.InvalidState);
            StringAssert.Contains("missing", result.Diagnostic.Reason);
            if (target == 5)
            {
                Evidence("invalid-state-equal-target", before, result);
            }
        }

        [TestCase(5L)]
        [TestCase(12L)]
        public void UnknownPendingEventTypeIsDiagnosticBeforeProcessing(long target)
        {
            var configuration = Configuration();
            var state = Snapshot(configuration, eventType: "unregistered");
            var before = Restore(state);

            var result = CreateEngine(configuration).AdvanceTo(state, At(target));

            AssertValidationFault(state, before, result, SimulationFaultCode.UnknownEventType);
            StringAssert.Contains("unregistered", result.Diagnostic.Reason);
        }

        [TestCase(5L)]
        [TestCase(12L)]
        public void NeedOutsideCurrentDefinitionIsDiagnosticBeforeProcessing(long target)
        {
            var configuration = Configuration();
            var pet = new PetState(Pet, NeedsState.Create(NeedTestData.CreateDefinition()));
            Assert.That(configuration.Needs.Entries[NeedId.Satiety].Contains(pet.Needs[NeedId.Satiety]), Is.False);
            var state = Snapshot(configuration, pet: pet);
            var before = Restore(state);
            var engine = CreateEngine(configuration, ("mark", (_, _) => { }));

            var result = engine.AdvanceTo(state, At(target));

            AssertValidationFault(state, before, result, SimulationFaultCode.InvalidState);
            StringAssert.Contains("outside the configured range", result.Diagnostic.Reason);
        }

        [TestCase(5L)]
        [TestCase(12L)]
        public void PersistedCountOverConfiguredBudgetIsDiagnosticBeforeProcessing(long target)
        {
            var configuration = Configuration();
            var state = Snapshot(configuration, processedEvents: configuration.Simulation.MaxEventsPerTimestamp + 1);
            var before = Restore(state);
            var engine = CreateEngine(configuration, ("mark", (_, _) => { }));

            var result = engine.AdvanceTo(state, At(target));

            AssertValidationFault(state, before, result, SimulationFaultCode.InvalidState);
            StringAssert.Contains("timestamp budget", result.Diagnostic.Reason);
        }

        [TestCase(4L)]
        [TestCase(5L)]
        [TestCase(12L)]
        public void ExecuteCommandUsesTheSameDiagnosticWithoutInvokingTheCommand(long target)
        {
            var configuration = Configuration();
            var state = Snapshot(configuration, modifier: "missing");
            var before = Restore(state);
            var engine = CreateEngine(configuration, ("mark", (_, _) => { }));
            bool validated = false;
            bool applied = false;
            var command = new Command(_ =>
            {
                validated = true;
                return null;
            }, _ => applied = true);

            var advance = engine.AdvanceTo(state, At(target));
            var result = engine.ExecuteCommand(state, At(target), command);

            AssertValidationFault(state, before, result, SimulationFaultCode.InvalidState);
            Assert.That(result.Diagnostic.Code, Is.EqualTo(advance.Diagnostic.Code));
            Assert.That(result.Diagnostic.Timestamp, Is.EqualTo(advance.Diagnostic.Timestamp));
            Assert.That(result.Diagnostic.Reason, Is.EqualTo(advance.Diagnostic.Reason));
            Assert.That(result.RejectionReason, Is.Null);
            Assert.That(validated, Is.False);
            Assert.That(applied, Is.False);
        }

        private static GameConfiguration Configuration() => Config(limit: 4, rates: _ => 1,
            modifiers: new[] { Scale("care.rate", 1_500_000) });

        private static GameState Snapshot(GameConfiguration configuration, string modifier = "care.rate", string eventType = "mark",
            int processedEvents = 2, PetState pet = null)
        {
            var streams = RandomStreams.Create(42);
            streams.NextUInt32(RandomStreamId.Birth);
            streams.NextBounded(RandomStreamId.Digestion, 9);
            var queue = new EventQueueSnapshot(new[] { new ScheduledEvent(Event(12, eventType, pet: Pet), 7L) }, 8L);
            return GameState.Restore(5, pet ?? Living(configuration), streams.CaptureSnapshot(), queue, new[] { modifier }, processedEvents);
        }

        private static void AssertValidationFault(GameState state, GameState before, SimulationResult result, SimulationFaultCode code)
        {
            Assert.That(result.Status, Is.EqualTo(SimulationStatus.Fault));
            Assert.That(result.Diagnostic.Code, Is.EqualTo(code));
            Assert.That(result.Diagnostic.Timestamp, Is.EqualTo(At(state.LogicalTimeMilliseconds)));
            AssertNoProgress(state, before, result);
        }

        private static void AssertNoProgress(GameState state, GameState before, SimulationResult result)
        {
            Assert.That(result.State, Is.SameAs(state));
            Assert.That(result.Reached, Is.EqualTo(At(before.LogicalTimeMilliseconds)));
            Assert.That(result.Trace, Is.Empty);
            Assert.That(result.CommittedTransactions, Is.Zero);
            Assert.That(result.ThresholdBoundaries, Is.Zero);
            SameState(before, result.State);
        }
    }
}
