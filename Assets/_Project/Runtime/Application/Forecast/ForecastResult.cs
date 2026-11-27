using System;
using System.Collections.Generic;
using IdlePet.Domain;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Time;

namespace IdlePet.Application.Forecast
{
    public sealed class ForecastResult
    {
        private readonly SimulationResult _simulation;

        internal ForecastResult(SimulationResult simulation)
        {
            _simulation = simulation;
            Origin = simulation.Origin;
            Target = simulation.Target;
            Diagnostic = simulation.Diagnostic;
        }

        internal ForecastResult(LogicalTime origin, LogicalTime target, SimulationDiagnostic blocker)
        {
            Origin = origin;
            Target = target;
            Diagnostic = blocker;
        }

        public SimulationStatus Status => _simulation?.Status ?? SimulationStatus.UnsupportedRuleSet;

        public LogicalTime Origin { get; }

        public LogicalTime Target { get; }

        public LogicalTime? Reached => _simulation?.Reached;

        /// <summary>The forecast branch, or null when the rule set was rejected before simulation.</summary>
        public GameState State => _simulation?.State;

        public IReadOnlyList<ScheduledEvent> Trace => _simulation?.Trace ?? Array.Empty<ScheduledEvent>();

        public int CommittedTransactions => _simulation?.CommittedTransactions ?? 0;

        public int ThresholdBoundaries => _simulation?.ThresholdBoundaries ?? 0;

        public SimulationDiagnostic Diagnostic { get; }
    }
}
