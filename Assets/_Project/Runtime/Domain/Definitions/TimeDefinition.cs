using System;

namespace IdlePet.Domain.Definitions
{
    public sealed class TimeDefinition
    {
        public TimeDefinition(
            long visualDayMilliseconds,
            long biologicalDayMilliseconds,
            long minNightMilliseconds,
            long maxNightMilliseconds,
            long scheduleChangeIntervalMilliseconds)
        {
            VisualDayMilliseconds = RequirePositive(visualDayMilliseconds, nameof(visualDayMilliseconds));
            BiologicalDayMilliseconds = RequirePositive(biologicalDayMilliseconds, nameof(biologicalDayMilliseconds));
            MinNightMilliseconds = RequirePositive(minNightMilliseconds, nameof(minNightMilliseconds));
            MaxNightMilliseconds = RequirePositive(maxNightMilliseconds, nameof(maxNightMilliseconds));
            ScheduleChangeIntervalMilliseconds = RequirePositive(scheduleChangeIntervalMilliseconds, nameof(scheduleChangeIntervalMilliseconds));

            if (minNightMilliseconds > maxNightMilliseconds || maxNightMilliseconds > biologicalDayMilliseconds)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNightMilliseconds),
                    "The night window must be ordered and fit within a biological day.");
            }
        }

        public long VisualDayMilliseconds { get; }

        public long BiologicalDayMilliseconds { get; }

        public long MinNightMilliseconds { get; }

        public long MaxNightMilliseconds { get; }

        public long ScheduleChangeIntervalMilliseconds { get; }

        private static long RequirePositive(long duration, string parameterName)
        {
            if (duration <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "The duration must be positive.");
            }

            return duration;
        }
    }
}
