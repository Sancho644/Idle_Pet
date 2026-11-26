using System;

namespace IdlePet.Domain.Simulation.Time
{
    public readonly struct LogicalDuration : IEquatable<LogicalDuration>
    {
        public LogicalDuration(long milliseconds)
        {
            if (milliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(milliseconds), "Logical duration cannot be negative.");
            }

            Milliseconds = milliseconds;
        }

        public long Milliseconds { get; }

        public static LogicalDuration FromHours(long hours)
        {
            if (hours < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(hours), "A duration in hours cannot be negative.");
            }

            const long millisecondsPerHour = TimeSpan.TicksPerHour / TimeSpan.TicksPerMillisecond;
            return new LogicalDuration(checked(hours * millisecondsPerHour));
        }

        public LogicalDuration Add(LogicalDuration other) => new LogicalDuration(checked(Milliseconds + other.Milliseconds));

        public bool Equals(LogicalDuration other) => Milliseconds == other.Milliseconds;

        public override bool Equals(object obj) => obj is LogicalDuration other && Equals(other);

        public override int GetHashCode() => Milliseconds.GetHashCode();
    }
}
