using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IdlePet.Domain;
using IdlePet.Domain.Pet;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Random;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;

namespace IdlePet.Tests.EditMode.Domain.Simulation.Events
{
    public sealed class EventSchedulerTests
    {
        [Test]
        public void CanonicalOrderingUsesEveryKeyAndDoesNotDependOnInputOrderOrCulture()
        {
            var events = new[]
            {
                new ScheduledEvent(Event(2, priority: int.MinValue, source: "A"), 0),
                new ScheduledEvent(Event(1, priority: int.MaxValue, source: "A"), 1),
                new ScheduledEvent(Event(1, priority: int.MinValue, source: "i"), 2),
                new ScheduledEvent(Event(1, priority: int.MinValue, source: "I"), 4),
                new ScheduledEvent(Event(1, priority: int.MinValue, source: "I"), 3),
            };
            CultureInfo prior = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                var first = new EventQueueSnapshot(events, 5);
                var second = new EventQueueSnapshot(events.Reverse(), 5);

                Assert.That(first.Events.Select(e => e.Sequence), Is.EqualTo(new long[] { 3, 4, 2, 1, 0 }));
                Assert.That(second.Events, Is.EqualTo(first.Events));
                Assert.Throws<NotSupportedException>(() => ((IList<ScheduledEvent>)first.Events).Clear());
            }
            finally
            {
                CultureInfo.CurrentCulture = prior;
            }
        }

        [Test]
        public void RegistrationCancellationAndRestorationKeepTheMonotonicSequence()
        {
            var queue = new EventScheduler(EventQueueSnapshot.Empty);
            Assert.That(queue.First, Is.Null);
            var future = queue.Register(At(4), Event(8));
            var now = queue.Register(At(4), Event(4));
            Assert.That(queue.First, Is.SameAs(now));
            Assert.That(queue.Cancel(now.Sequence), Is.True);
            Assert.That(queue.Cancel(now.Sequence), Is.False);
            var point = queue.Capture();
            var independent = new EventScheduler(point);

            Assert.That(queue.Register(At(4), Event(6)).Sequence, Is.EqualTo(2));
            Assert.That(independent.First, Is.SameAs(future));
            Assert.That(point.Events, Is.EqualTo(new[] { future }));
            Assert.That(independent.Register(At(4), Event(9)).Sequence, Is.EqualTo(2));
            independent.Remove(future);
            Assert.That(independent.Capture().NextSequence, Is.EqualTo(3));
            Assert.That(queue.Capture().Events, Does.Contain(future));
        }

        [Test]
        public void PastTimeAndSequenceOverflowLeaveQueueAndCounterUntouched()
        {
            var scheduler = new EventScheduler(new EventQueueSnapshot(Array.Empty<ScheduledEvent>(), long.MaxValue - 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Register(At(4), Event(3)));
            var last = scheduler.Register(At(4), Event(4));
            Assert.That(last.Sequence, Is.EqualTo(long.MaxValue - 1));
            Assert.Throws<OverflowException>(() => scheduler.Register(At(4), Event(5)));
            Assert.That(scheduler.Capture().Events, Is.EqualTo(new[] { last }));
            Assert.That(scheduler.Capture().NextSequence, Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void QueueSnapshotRejectsDuplicatesMissingEntriesAndInconsistentNextSequence()
        {
            var stored = new ScheduledEvent(Event(2), 1);
            Assert.Throws<ArgumentException>(() => new EventQueueSnapshot(new[] { stored, stored }, 2));
            Assert.Throws<ArgumentException>(() => new EventQueueSnapshot(new[] { stored }, 1));
            Assert.Throws<ArgumentException>(() => new EventQueueSnapshot(new ScheduledEvent[] { null }, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EventQueueSnapshot(Array.Empty<ScheduledEvent>(), -1));
            Assert.Throws<ArgumentNullException>(() => new EventQueueSnapshot(null, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScheduledEvent(Event(2), -1));
        }

        [Test]
        public void EventRequestRejectsInvalidIdentifiersScopeAndPetBinding()
        {
            Assert.Throws<ArgumentException>(() => Event(2, source: "bad id"));
            Assert.Throws<ArgumentException>(() => Event(2, "bad type!"));
            Assert.Throws<ArgumentException>(() => new EventRequest(At(2), 1, "src", "mark", EventScope.PetLife, null));
            Assert.Throws<ArgumentException>(() => new EventRequest(At(2), 1, "src", "mark", EventScope.World, Pet));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EventRequest(At(2), 1, "src", "mark", (EventScope)0, null));
        }

        [Test]
        public void StableGameStateRejectsPastDueAndOrphanedPetEvents()
        {
            var rng = RandomStreams.Create(13).CaptureSnapshot();
            var due = new EventQueueSnapshot(new[] { new ScheduledEvent(Event(4), 0) }, 1);
            var orphan = new EventQueueSnapshot(new[] { new ScheduledEvent(Event(5, pet: Pet), 0) }, 1);
            Assert.Throws<ArgumentException>(() => GameState.Restore(4, null, rng, due, Array.Empty<string>(), 0));
            Assert.Throws<ArgumentException>(() => GameState.Restore(5, null, rng, due, Array.Empty<string>(), 0));
            Assert.Throws<ArgumentException>(() => GameState.Restore(4, null, rng, orphan, Array.Empty<string>(), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => GameState.Restore(0, null, rng, EventQueueSnapshot.Empty, Array.Empty<string>(), 1));
            Assert.Throws<ArgumentNullException>(() => GameState.Restore(0, null, null, EventQueueSnapshot.Empty, Array.Empty<string>(), 0));
        }

        [Test]
        public void StableGameStateCopiesAndOrdersActiveModifiersAndRejectsFutureTerminalTime()
        {
            var rng = RandomStreams.Create(13).CaptureSnapshot();
            var input = new[] { "a", "I" };
            var state = GameState.Restore(5, null, rng, EventQueueSnapshot.Empty, input, 0);
            input[0] = "elsewhere";

            Assert.That(state.ActiveModifierIds, Is.EqualTo(new[] { "I", "a" }));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)state.ActiveModifierIds).Clear());
            Assert.Throws<ArgumentException>(() => GameState.Restore(5, null, rng, EventQueueSnapshot.Empty, new[] { "a", "a" }, 0));
            var pet = Living(Config());
            Assert.Throws<ArgumentException>(() => new GameState(5, new PetState(pet.Id, pet.Needs, At(6)), 13));
        }
    }
}
