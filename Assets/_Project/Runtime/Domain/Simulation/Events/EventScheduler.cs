using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Domain.Common.Identifiers;
using IdlePet.Domain.Simulation.Time;

namespace IdlePet.Domain.Simulation.Events
{
    internal sealed class EventScheduler
    {
        private readonly SortedSet<ScheduledEvent> _events;
        private long _nextSequence;

        internal EventScheduler(EventQueueSnapshot snapshot)
        {
            _events = new SortedSet<ScheduledEvent>(snapshot.Events);
            _nextSequence = snapshot.NextSequence;
        }

        internal ScheduledEvent First => _events.Min;

        internal ScheduledEvent Register(LogicalTime time, EventRequest request)
        {
            if (request.Timestamp.CompareTo(time) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "Cannot schedule an event in the past.");
            }

            long nextSequence = checked(_nextSequence + 1);
            var scheduled = new ScheduledEvent(request, _nextSequence);
            _events.Add(scheduled);
            _nextSequence = nextSequence;
            return scheduled;
        }

        internal void Remove(ScheduledEvent entry) => _events.Remove(entry);

        internal bool Cancel(long sequence)
        {
            var entry = _events.FirstOrDefault(candidate => candidate.Sequence == sequence);
            return entry != null && _events.Remove(entry);
        }

        internal void CancelPetLife(PetId petId)
        {
            _events.RemoveWhere(entry => entry.Request.Scope == EventScope.PetLife && entry.Request.PetId.Value.Equals(petId));
        }

        internal EventQueueSnapshot Capture() => new EventQueueSnapshot(_events, _nextSequence);
    }
}
