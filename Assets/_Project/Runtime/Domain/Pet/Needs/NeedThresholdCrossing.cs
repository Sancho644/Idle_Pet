using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Simulation.Time;

namespace IdlePet.Domain.Pet.Needs
{
    public readonly struct NeedThresholdCrossing
    {
        internal NeedThresholdCrossing(NeedId needId, NeedValue threshold, NeedThresholdDirection direction, LogicalDuration elapsed)
        {
            NeedId = needId;
            Threshold = threshold;
            Direction = direction;
            Elapsed = elapsed;
        }

        public NeedId NeedId { get; }

        public NeedValue Threshold { get; }

        public NeedThresholdDirection Direction { get; }

        public LogicalDuration Elapsed { get; }
    }
}
