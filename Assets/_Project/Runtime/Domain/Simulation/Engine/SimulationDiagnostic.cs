using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Time;

namespace IdlePet.Domain.Simulation.Engine
{
    public sealed class SimulationDiagnostic
    {
        internal SimulationDiagnostic(SimulationFaultCode code, LogicalTime timestamp,
            ScheduledEvent currentEvent, string reason, int? limit)
        {
            Code = code;
            Timestamp = timestamp;
            Event = currentEvent;
            Reason = reason;
            Limit = limit;
        }

        public SimulationFaultCode Code { get; }

        public LogicalTime Timestamp { get; }

        public ScheduledEvent Event { get; }

        public string Reason { get; }

        public int? Limit { get; }
    }
}
