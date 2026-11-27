using System;
using System.Collections.Generic;

namespace IdlePet.Domain.Simulation.Random
{
    public sealed class RandomStreamsSnapshot
    {
        public RandomStreamsSnapshot(ulong worldSeed, int rngAlgorithmVersion, IEnumerable<RandomStreamSnapshot> streams)
        {
            if (rngAlgorithmVersion != Pcg32.AlgorithmVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(rngAlgorithmVersion), rngAlgorithmVersion,
                    "Unsupported RNG algorithm version.");
            }

            if (streams == null)
            {
                throw new ArgumentNullException(nameof(streams));
            }

            var byId = new Dictionary<RandomStreamId, RandomStreamSnapshot>();
            var selectors = new HashSet<ulong>();
            foreach (var stream in streams)
            {
                ulong expectedSelector = RandomStreamRegistry.GetSequenceId(stream.StreamId);
                if (byId.ContainsKey(stream.StreamId))
                {
                    throw new ArgumentException($"Duplicate random stream {stream.StreamId}.", nameof(streams));
                }

                if (stream.SequenceId > Pcg32.MaxSequenceId)
                {
                    throw new ArgumentOutOfRangeException(nameof(streams), $"The selector for {stream.StreamId} exceeds 63 bits.");
                }

                if (!selectors.Add(stream.SequenceId))
                {
                    throw new ArgumentException($"Duplicate random selector {stream.SequenceId}.", nameof(streams));
                }

                if (stream.SequenceId != expectedSelector)
                {
                    throw new ArgumentException($"The selector for {stream.StreamId} must be {expectedSelector}.", nameof(streams));
                }

                byId.Add(stream.StreamId, stream);
            }

            var orderedIds = RandomStreamRegistry.OrderedIds;
            var ordered = new RandomStreamSnapshot[orderedIds.Count];
            for (int i = 0; i < ordered.Length; i++)
            {
                if (!byId.TryGetValue(orderedIds[i], out ordered[i]))
                {
                    throw new ArgumentException($"Missing random stream {orderedIds[i]}.", nameof(streams));
                }
            }

            WorldSeed = worldSeed;
            RngAlgorithmVersion = rngAlgorithmVersion;
            Streams = Array.AsReadOnly(ordered);
        }

        public ulong WorldSeed { get; }

        public int RngAlgorithmVersion { get; }

        public IReadOnlyList<RandomStreamSnapshot> Streams { get; }
    }
}
