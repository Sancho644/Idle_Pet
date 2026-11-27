using System;
using System.Collections.Generic;

namespace IdlePet.Domain.Simulation.Random
{
    public sealed class RandomStreams
    {
        private readonly Dictionary<RandomStreamId, Pcg32> _streams;

        private RandomStreams(ulong worldSeed, Dictionary<RandomStreamId, Pcg32> streams)
        {
            WorldSeed = worldSeed;
            _streams = streams;
        }

        public ulong WorldSeed { get; }

        public int RngAlgorithmVersion => Pcg32.AlgorithmVersion;

        public static RandomStreams Create(ulong worldSeed)
        {
            var ids = RandomStreamRegistry.OrderedIds;
            var streams = new Dictionary<RandomStreamId, Pcg32>(ids.Count);
            foreach (RandomStreamId id in ids)
            {
                streams.Add(id, Pcg32.Create(worldSeed, RandomStreamRegistry.GetSequenceId(id)));
            }

            return new RandomStreams(worldSeed, streams);
        }

        public static RandomStreams Restore(RandomStreamsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            // The sealed snapshot validates its copied contents before any instance can observe it.
            var streams = new Dictionary<RandomStreamId, Pcg32>(snapshot.Streams.Count);
            foreach (var stream in snapshot.Streams)
            {
                streams.Add(stream.StreamId, Pcg32.Restore(stream.SequenceId, stream.State));
            }

            return new RandomStreams(snapshot.WorldSeed, streams);
        }

        public uint NextUInt32(RandomStreamId streamId) => GetStream(streamId).NextUInt32();

        public uint NextBounded(RandomStreamId streamId, uint bound) => GetStream(streamId).NextBounded(bound);

        public RandomStreamSnapshot CaptureStream(RandomStreamId streamId)
        {
            Pcg32 rng = GetStream(streamId);
            return new RandomStreamSnapshot(streamId, rng.SequenceId, rng.State);
        }

        public RandomStreamsSnapshot CaptureSnapshot()
        {
            var ids = RandomStreamRegistry.OrderedIds;
            var streams = new RandomStreamSnapshot[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                streams[i] = CaptureStream(ids[i]);
            }

            return new RandomStreamsSnapshot(WorldSeed, RngAlgorithmVersion, streams);
        }

        private Pcg32 GetStream(RandomStreamId streamId)
        {
            if (!_streams.TryGetValue(streamId, out Pcg32 rng))
            {
                throw new ArgumentOutOfRangeException(nameof(streamId), streamId, "Unknown random stream.");
            }

            return rng;
        }
    }
}
