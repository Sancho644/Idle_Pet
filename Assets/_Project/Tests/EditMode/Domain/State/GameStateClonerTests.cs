using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Domain;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Pet;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Random;
using IdlePet.Domain.Simulation.Time;
using IdlePet.Domain.State;
using IdlePet.Tests.EditMode.Domain.Simulation.Engine;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Application.Forecast.ForecastTestData;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;

namespace IdlePet.Tests.EditMode.Domain.State
{
    public sealed class GameStateClonerTests
    {
        [Test]
        public void CopiesAnEmptyWorldWithZeroSeed()
        {
            var world = new GameState(0, null, 0);
            var branch = GameStateCloner.Clone(world);

            Assert.That(branch, Is.Not.SameAs(world));
            Assert.That(branch.ActivePet, Is.Null);
            Assert.That(branch.Rng.WorldSeed, Is.Zero);
            Assert.That(branch.EventQueue.Events, Is.Empty);
            SameState(world, branch);
            Assert.Throws<ArgumentNullException>(() => GameStateCloner.Clone(null));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CopiesCompleteStateWithNonzeroProgressAndFullWidthSeed(bool terminal)
        {
            var cfg = Config();
            var needs = NeedsState.Restore(cfg.Needs,
                cfg.Needs.Entries.Keys.Select(id => KeyValuePair.Create(id, Exact(111 + (int)id))));
            var pet = new PetState(Pet, needs, terminal ? At(6) : (LogicalTime?)null);
            var streams = RandomStreams.Create(ulong.MaxValue);
            foreach (var stream in streams.CaptureSnapshot().Streams)
            {
                streams.NextUInt32(stream.StreamId);
            }
            var requests = new List<ScheduledEvent> { new ScheduledEvent(Event(15, text: "payload"), 10) };
            if (!terminal)
            {
                var threshold = new NeedThresholdCrossing(NeedId.Bond, Exact(72), NeedThresholdDirection.Falling, default);
                requests.Add(new ScheduledEvent(new EventRequest(At(15), 0, "need.Bond", "need.threshold",
                    EventScope.PetLife, Pet, new EventPayload(threshold)), 11));
            }
            var world = GameState.Restore(7, pet, streams.CaptureSnapshot(),
                new EventQueueSnapshot(requests, 12), new[] { "second", "first" }, 3);
            var original = SimulationTestData.State("initial", world);

            var first = GameStateCloner.Clone(world);
            var second = GameStateCloner.Clone(world);

            Assert.That(first, Is.Not.SameAs(world).And.Not.SameAs(second));
            Assert.That(first.ActiveModifierIds, Is.Not.SameAs(world.ActiveModifierIds));
            Assert.That(first.ActivePet.Needs.Values.Count, Is.EqualTo(8));
            Assert.That(first.ActivePet.Needs.Values.Values.All(value => value.Remainder.Numerator != 0), Is.True);
            Assert.That(first.Rng.Streams.Count, Is.EqualTo(6));
            Assert.That(first.Rng.WorldSeed, Is.EqualTo(ulong.MaxValue));
            Assert.That(first.EventQueue.NextSequence, Is.EqualTo(12));
            Assert.That(first.ProcessedEventsAtCursor, Is.EqualTo(3));
            SameState(world, first);
            SameState(world, second);

            Assert.Throws<NotSupportedException>(() => ((IList<string>)first.ActiveModifierIds).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<ScheduledEvent>)first.EventQueue.Events).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<RandomStreamSnapshot>)first.Rng.Streams).Clear());
            Assert.Throws<NotSupportedException>(() => ((IDictionary<NeedId, NeedValue>)first.ActivePet.Needs.Values).Clear());
            var queue = new EventScheduler(first.EventQueue);
            Assert.That(queue.Cancel(10), Is.True);
            var rng = RandomStreams.Restore(first.Rng);
            rng.NextUInt32(RandomStreamId.Birth);
            Assert.That(queue.Capture().Events.Count, Is.EqualTo(world.EventQueue.Events.Count - 1));
            Assert.That(rng.CaptureSnapshot().Streams, Is.Not.EqualTo(world.Rng.Streams));
            SameState(world, first);
            SameState(world, second);
            if (!terminal)
            {
                Report("deep-copy", original, SimulationTestData.State("source-after", world),
                    SimulationTestData.State("branch", first), SimulationTestData.State("second-branch", second));
            }
        }

        [Test]
        public void ClonedCursorKeepsAnAlreadyExhaustedBudgetForCommandsAtTheSameTime()
        {
            var engine = Engine(Config(limit: 2), Rule("mark"));
            var source = engine.Initialize(At(0), null, 42, new[] { Event(0), Event(0) }).State;
            var before = Restore(source);
            var branch = GameStateCloner.Clone(source);
            var command = new Command(_ => null, context => context.Schedule(Event(0)));

            var outcome = engine.ExecuteCommand(branch, At(0), command);

            Assert.That(outcome.Status, Is.EqualTo(SimulationStatus.Fault));
            Assert.That(outcome.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.EventLimit));
            Assert.That(outcome.Diagnostic.Event.Sequence, Is.EqualTo(2));
            Assert.That(outcome.State.ProcessedEventsAtCursor, Is.EqualTo(2));
            Assert.That(outcome.Trace, Is.Empty);
            SameState(before, outcome.State);
            SameState(before, source);
        }
    }
}
