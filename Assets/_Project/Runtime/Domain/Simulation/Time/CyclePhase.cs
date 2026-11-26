using System;

namespace IdlePet.Domain.Simulation.Time
{
    public readonly struct CyclePhase
    {
        internal CyclePhase(long cycleIndex, long elapsedInCycleMilliseconds)
        {
            CycleIndex = cycleIndex;
            ElapsedInCycleMilliseconds = elapsedInCycleMilliseconds;
        }

        public long CycleIndex { get; }

        public long ElapsedInCycleMilliseconds { get; }

        internal static CyclePhase At(LogicalTime time, LogicalTime origin, long periodMilliseconds)
        {
            // Two nonnegative logical timestamps always have a representable signed difference.
            long relative = checked(time.Milliseconds - origin.Milliseconds);
            long cycleIndex = Math.DivRem(relative, periodMilliseconds, out long phase);
            // Division truncates toward zero. Before the origin, use floor division instead.
            if (phase < 0)
            {
                cycleIndex = checked(cycleIndex - 1);
                phase = checked(phase + periodMilliseconds);
            }

            return new CyclePhase(cycleIndex, phase);
        }
    }
}
