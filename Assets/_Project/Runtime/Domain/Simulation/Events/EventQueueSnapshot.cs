using System;
using System.Collections.Generic;
using System.Linq;

namespace IdlePet.Domain.Simulation.Events
{
    public sealed class EventQueueSnapshot
    {
        public static EventQueueSnapshot Empty { get; } = new EventQueueSnapshot(Array.Empty<ScheduledEvent>(), 0);

        public EventQueueSnapshot(IEnumerable<ScheduledEvent> events, long nextSequence)
        {
            if (nextSequence < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nextSequence));
            }

            var ordered = (events ?? throw new ArgumentNullException(nameof(events))).ToArray();
            var sequences = new HashSet<long>();
            foreach (var entry in ordered)
            {
                if (entry == null || entry.Sequence >= nextSequence || !sequences.Add(entry.Sequence))
                {
                    throw new ArgumentException("The queue contains a missing event or an invalid or duplicate sequence.", nameof(events));
                }
            }

            Array.Sort(ordered);
            Events = Array.AsReadOnly(ordered);
            NextSequence = nextSequence;
        }

        public IReadOnlyList<ScheduledEvent> Events { get; }

        public long NextSequence { get; }
    }
}
