using System;
using System.Collections.Generic;

namespace IdlePet.Domain.Simulation.Random
{
    public static class RandomStreamRegistry
    {
        public static IReadOnlyList<RandomStreamId> OrderedIds { get; } = Array.AsReadOnly(new[]
        {
            RandomStreamId.Birth,
            RandomStreamId.Behaviour,
            RandomStreamId.Illness,
            RandomStreamId.Evolution,
            RandomStreamId.Lifecycle,
            RandomStreamId.Digestion
        });

        public static ulong GetSequenceId(RandomStreamId streamId)
        {
            // The explicit enum values are the v1 protocol selectors, never array positions.
            switch (streamId)
            {
                case RandomStreamId.Birth:
                case RandomStreamId.Behaviour:
                case RandomStreamId.Illness:
                case RandomStreamId.Evolution:
                case RandomStreamId.Lifecycle:
                case RandomStreamId.Digestion:
                    return (ulong)streamId;
                default:
                    throw new ArgumentOutOfRangeException(nameof(streamId), streamId, "Unknown random stream.");
            }
        }
    }
}
