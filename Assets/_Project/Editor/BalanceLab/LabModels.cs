using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IdlePet.Authoring;
using IdlePet.Authoring.Mapping;
using IdlePet.Domain;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Time;

namespace IdlePet.Editor.BalanceLab
{
    internal enum LabScenario { Production, FastThresholds, StressLong, PartitionParity, ForecastParity, SelectedProfile }
    internal enum LabMode { AdvanceTo, PartitionedAdvanceTo, Forecast, Compare }

    internal sealed class LabRequest
    {
        internal LabRequest(GameConfigSO root, LabScenario scenario, LabMode mode, ulong seed, long start, long duration,
            int segments, bool activateModifiers = false)
        {
            if (!Enum.IsDefined(typeof(LabScenario), scenario) || !Enum.IsDefined(typeof(LabMode), mode))
            {
                throw new ArgumentException("Select a supported scenario and mode.");
            }
            if (segments < 1 || segments > 4096)
            {
                throw new ArgumentOutOfRangeException(nameof(segments), "Use between 1 and 4096 segments.");
            }

            Root = root;
            Scenario = scenario;
            Mode = mode;
            Seed = seed;
            Origin = new LogicalTime(start);
            Target = Origin.Add(new LogicalDuration(duration));
            Segments = segments;
            ActivateModifiers = activateModifiers;
        }

        internal GameConfigSO Root { get; }
        internal LabScenario Scenario { get; }
        internal LabMode Mode { get; }
        internal ulong Seed { get; }
        internal LogicalTime Origin { get; }
        internal LogicalTime Target { get; }
        internal int Segments { get; }
        internal bool ActivateModifiers { get; }

        internal static LabRequest Parse(GameConfigSO root, LabScenario scenario, LabMode mode, string seed,
            string start, string duration, int segments, bool activateModifiers)
        {
            if (!ulong.TryParse(seed, NumberStyles.None, CultureInfo.InvariantCulture, out ulong worldSeed) ||
                !long.TryParse(start, NumberStyles.None, CultureInfo.InvariantCulture, out long origin) ||
                !long.TryParse(duration, NumberStyles.None, CultureInfo.InvariantCulture, out long elapsed))
            {
                throw new ArgumentException("Enter an unsigned decimal seed and nonnegative integer millisecond values.");
            }
            return new LabRequest(root, scenario, mode, worldSeed, origin, elapsed, segments, activateModifiers);
        }
    }

    internal sealed class LabProjection
    {
        internal LabProjection(GameState state, SimulationStatus status, SimulationDiagnostic diagnostic,
            IEnumerable<ScheduledEvent> trace, int transactions, int thresholdBoundaries, double elapsedMs)
        {
            State = state;
            Status = status;
            Diagnostic = diagnostic;
            Trace = Array.AsReadOnly(trace.ToArray());
            Transactions = transactions;
            ThresholdBoundaries = thresholdBoundaries;
            ElapsedMs = elapsedMs;
        }

        internal GameState State { get; }
        internal SimulationStatus Status { get; }
        internal SimulationDiagnostic Diagnostic { get; }
        internal IReadOnlyList<ScheduledEvent> Trace { get; }
        internal int Transactions { get; }
        internal int ThresholdBoundaries { get; }
        internal double ElapsedMs { get; }
    }

    internal sealed class LabResult
    {
        private static readonly string[] CompareStrategies =
            { "AdvanceTo", "PartitionedAdvanceTo", "Forecast", "PartitionedForecast" };

        internal LabResult(LabRequest request, ConfigurationMappingResult mapping, GameState source = null,
            IEnumerable<LabProjection> projections = null, string difference = null)
        {
            Request = request;
            Mapping = mapping;
            Source = source;
            Projections = Array.AsReadOnly(projections?.ToArray() ?? Array.Empty<LabProjection>());
            Difference = difference;
        }

        internal LabRequest Request { get; }
        internal ConfigurationMappingResult Mapping { get; }
        internal GameState Source { get; }
        internal IReadOnlyList<LabProjection> Projections { get; }
        internal string Difference { get; }
        internal GameConfiguration Definition => Mapping.Definition;

        internal string Strategy(int index) => Request.Mode == LabMode.Compare
            ? CompareStrategies[index] : Request.Mode.ToString();
    }
}
