using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Domain.Common.Identifiers;
using IdlePet.Domain.Simulation.Time;

namespace IdlePet.Domain.Simulation.Events
{
    internal sealed class EventScheduler
    {
        private readonly EventQueueSnapshot _snapshot;
        private SortedSet<ScheduledEvent> _events;
        private long _nextSequence;

        internal EventScheduler(EventQueueSnapshot snapshot)
        {
            _snapshot = snapshot;
            _nextSequence = snapshot.NextSequence;
        }

        internal ScheduledEvent First => _events != null ? _events.Min :
            _snapshot.Events.Count == 0 ? null : _snapshot.Events[0];

        internal ScheduledEvent Register(LogicalTime time, EventRequest request)
        {
            if (request.Timestamp.CompareTo(time) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "Cannot schedule an event in the past.");
            }

            long nextSequence = checked(_nextSequence + 1);
            var scheduled = new ScheduledEvent(request, _nextSequence);
            MutableEvents.Add(scheduled);
            _nextSequence = nextSequence;
            return scheduled;
        }

        internal void Remove(ScheduledEvent entry) => MutableEvents.Remove(entry);

        internal bool Cancel(long sequence)
        {
            var entry = CurrentEvents.FirstOrDefault(candidate => candidate.Sequence == sequence);
            return entry != null && MutableEvents.Remove(entry);
        }

        internal void CancelPetLife(PetId petId)
        {
            bool IsPetLife(ScheduledEvent entry) => entry.Request.Scope == EventScope.PetLife && entry.Request.PetId.Value.Equals(petId);
            if (_events != null || _snapshot.Events.Any(IsPetLife))
            {
                MutableEvents.RemoveWhere(IsPetLife);
            }
        }

        internal EventQueueSnapshot Capture() => _events == null ? _snapshot : EventQueueSnapshot.FromScheduler(_events, _nextSequence);

        private IEnumerable<ScheduledEvent> CurrentEvents => (IEnumerable<ScheduledEvent>)_events ?? _snapshot.Events;

        private SortedSet<ScheduledEvent> MutableEvents => _events ??= new SortedSet<ScheduledEvent>(_snapshot.Events);
    }
}
