using System;
using System.Collections.Generic;
using System.Linq;

namespace IdlePet.Domain.Simulation.Events
{
    public sealed class EventQueueSnapshot
    {
        public static EventQueueSnapshot Empty { get; } = new EventQueueSnapshot(Array.Empty<ScheduledEvent>(), 0);

        public EventQueueSnapshot(IEnumerable<ScheduledEvent> events, long nextSequence)
            : this(ValidateAndOrder(events, nextSequence), nextSequence)
        {
        }

        private EventQueueSnapshot(ScheduledEvent[] ordered, long nextSequence)
        {
            Events = Array.AsReadOnly(ordered);
            NextSequence = nextSequence;
        }

        // The scheduler owns the sorted set and allocates unique sequence numbers before insertion.
        internal static EventQueueSnapshot FromScheduler(SortedSet<ScheduledEvent> events, long nextSequence) =>
            new EventQueueSnapshot(events.ToArray(), nextSequence);

        private static ScheduledEvent[] ValidateAndOrder(IEnumerable<ScheduledEvent> events, long nextSequence)
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
            return ordered;
        }

        public IReadOnlyList<ScheduledEvent> Events { get; }

        public long NextSequence { get; }
    }
}
