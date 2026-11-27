using System;
using IdlePet.Domain.Definitions;

namespace IdlePet.Domain.Simulation.Time
{
    public sealed class CircadianClock
    {
        private readonly TimeDefinition _definition;
        private readonly LogicalTime _cycleOrigin;

        public CircadianClock(TimeDefinition definition, LogicalTime cycleOrigin)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _cycleOrigin = cycleOrigin;
        }

        public CyclePhase Project(LogicalTime time) => CyclePhase.At(time, _cycleOrigin, _definition.BiologicalDayMilliseconds);
    }
}
