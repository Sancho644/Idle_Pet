using System;

namespace IdlePet.Domain.Simulation.Random
{
    public sealed class Pcg32
    {
        public const int AlgorithmVersion = 1;
        public const ulong MaxSequenceId = 0x7FFFFFFFFFFFFFFFUL;
        private const ulong Multiplier = 6364136223846793005UL;

        private readonly ulong _increment;
        private readonly Func<uint> _nextRaw;
        private ulong _state;

        private Pcg32(ulong sequenceId, ulong state)
        {
            if (sequenceId > MaxSequenceId)
            {
                throw new ArgumentOutOfRangeException(nameof(sequenceId), "A PCG32 selector must fit in 63 bits.");
            }

            _increment = (sequenceId << 1) | 1UL;
            _state = state;
            _nextRaw = NextUInt32;
        }

        public ulong SequenceId => _increment >> 1;

        public ulong State => _state;

        public static Pcg32 Create(ulong seed, ulong sequenceId)
        {
            var rng = new Pcg32(sequenceId, 0);
            rng.Step();
            rng._state = unchecked(rng._state + seed);
            rng.Step();
            return rng;
        }

        public static Pcg32 Restore(ulong sequenceId, ulong state) => new Pcg32(sequenceId, state);

        public uint NextUInt32()
        {
            ulong previous = Step();
            uint xorshifted = unchecked((uint)(((previous >> 18) ^ previous) >> 27));
            int rotation = (int)(previous >> 59);
            return (xorshifted >> rotation) | (xorshifted << ((-rotation) & 31));
        }

        public uint NextBounded(uint bound) => SampleBounded(bound, _nextRaw);

        internal static uint SampleBounded(uint bound, Func<uint> nextRaw)
        {
            if (bound == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bound), "The exclusive upper bound must be positive.");
            }

            uint threshold = (uint)((1UL << 32) % bound);
            uint raw;
            do
            {
                raw = nextRaw();
            }
            while (raw < threshold);

            return raw % bound;
        }

        private ulong Step()
        {
            ulong previous = _state;
            _state = unchecked(previous * Multiplier + _increment);
            return previous;
        }
    }
}
