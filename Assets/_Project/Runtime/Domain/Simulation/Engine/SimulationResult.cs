using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Time;

namespace IdlePet.Domain.Simulation.Engine
{
    public sealed class SimulationResult
    {
        internal SimulationResult(SimulationStatus status, LogicalTime origin, LogicalTime target, GameState state,
            IEnumerable<ScheduledEvent> trace, int committedTransactions, int thresholdBoundaries,
            SimulationDiagnostic diagnostic = null, string rejectionReason = null)
        {
            Status = status;
            Origin = origin;
            Target = target;
            State = state;
            Trace = Array.AsReadOnly(trace.ToArray());
            CommittedTransactions = committedTransactions;
            ThresholdBoundaries = thresholdBoundaries;
            Diagnostic = diagnostic;
            RejectionReason = rejectionReason;
        }

        public SimulationStatus Status { get; }

        public LogicalTime Origin { get; }

        public LogicalTime Target { get; }

        public LogicalTime? Reached => State == null ? (LogicalTime?)null : SimulationClock.GetTime(State);

        /// <summary>The returned snapshot. A failed initialization has no snapshot.</summary>
        public GameState State { get; }

        public IReadOnlyList<ScheduledEvent> Trace { get; }

        public int CommittedTransactions { get; }

        public int ThresholdBoundaries { get; }

        public SimulationDiagnostic Diagnostic { get; }

        public string RejectionReason { get; }
    }
}
