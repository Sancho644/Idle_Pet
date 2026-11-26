using System;

namespace IdlePet.Domain.Simulation.Time
{
    public readonly struct LogicalTime : IEquatable<LogicalTime>, IComparable<LogicalTime>
    {
        public LogicalTime(long milliseconds)
        {
            if (milliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(milliseconds), "Logical time cannot be negative.");
            }

            Milliseconds = milliseconds;
        }

        public long Milliseconds { get; }

        public LogicalTime Add(LogicalDuration elapsed) => new LogicalTime(checked(Milliseconds + elapsed.Milliseconds));

        public int CompareTo(LogicalTime other) => Milliseconds.CompareTo(other.Milliseconds);

        public bool Equals(LogicalTime other) => Milliseconds == other.Milliseconds;

        public override bool Equals(object obj) => obj is LogicalTime other && Equals(other);

        public override int GetHashCode() => Milliseconds.GetHashCode();
    }
}
