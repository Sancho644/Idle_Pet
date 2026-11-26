using System;

namespace IdlePet.Domain.Simulation.Events
{
    public sealed class ScheduledEvent : IComparable<ScheduledEvent>
    {
        public ScheduledEvent(EventRequest request, long sequence)
        {
            if (sequence < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            Request = request ?? throw new ArgumentNullException(nameof(request));
            Sequence = sequence;
        }

        public EventRequest Request { get; }

        public long Sequence { get; }

        public int CompareTo(ScheduledEvent other)
        {
            if (other == null)
            {
                return 1;
            }

            int order = Request.Timestamp.CompareTo(other.Request.Timestamp);
            if (order == 0)
            {
                order = Request.Priority.CompareTo(other.Request.Priority);
            }

            if (order == 0)
            {
                order = StringComparer.Ordinal.Compare(Request.SourceId, other.Request.SourceId);
            }

            return order == 0 ? Sequence.CompareTo(other.Sequence) : order;
        }
    }
}
