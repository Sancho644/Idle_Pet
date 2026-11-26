using System;

namespace IdlePet.Domain.Definitions
{
    public sealed class SimulationDefinition
    {
        public SimulationDefinition(int maxEventsPerTimestamp)
        {
            if (maxEventsPerTimestamp <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEventsPerTimestamp), "The event limit must be positive.");
            }

            MaxEventsPerTimestamp = maxEventsPerTimestamp;
        }

        public int MaxEventsPerTimestamp { get; }
    }
}
