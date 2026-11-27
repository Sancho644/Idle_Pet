using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using IdlePet.Application.Forecast;
using IdlePet.Domain;
using IdlePet.Domain.Common.Identifiers;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Time;
using IdlePet.Domain.State;

namespace IdlePet.Editor.BalanceLab
{
    internal static class LabRunner
    {
        internal static LabResult Run(LabRequest request)
        {
            BalanceProfileKind? profile = request.Scenario switch
            {
                LabScenario.FastThresholds => BalanceProfileKind.FastTesting,
                LabScenario.StressLong => BalanceProfileKind.StressTesting,
                LabScenario.SelectedProfile => null,
                _ => BalanceProfileKind.Production
            };
            var mapping = LabProfiles.Map(request.Root, profile);
            if (!mapping.Succeeded)
            {
                return new LabResult(request, mapping);
            }

            var definition = mapping.Definition;
            var pet = new PetState(new PetId("lab.pet"), NeedsState.Create(definition.Needs));
            var source = new GameState(request.Origin.Milliseconds, pet, request.Seed);
            if (request.ActivateModifiers && definition.ProfileKind != BalanceProfileKind.Production)
            {
                source = GameState.Restore(source.LogicalTimeMilliseconds, source.ActivePet, source.Rng,
                    source.EventQueue, definition.RateModifiers.Keys, source.ProcessedEventsAtCursor);
            }

            var engine = new SimulationEngine(definition);
            var full = new[] { request.Target.Milliseconds };
            var cuts = Uniform(request.Origin.Milliseconds, request.Target.Milliseconds, request.Segments);
            switch (request.Mode)
            {
                case LabMode.AdvanceTo:
                    return new LabResult(request, mapping, source, new[] { Project(engine, source, full) });
                case LabMode.PartitionedAdvanceTo:
                    return new LabResult(request, mapping, source, new[] { Project(engine, source, cuts) });
                case LabMode.Forecast:
                    return new LabResult(request, mapping, source, new[] { Project(engine, source, full, forecast: true) });
                default:
                    var projections = new[] { Project(engine, source, full), Project(engine, source, cuts),
                        Project(engine, source, full, forecast: true), Project(engine, source, cuts, forecast: true) };
                    string diff = projections.Skip(1).Select(other => LabReport.Difference(projections[0], other))
                        .FirstOrDefault(mismatch => mismatch != null);
                    return new LabResult(request, mapping, source, projections, diff);
            }
        }

        internal static LabProjection Project(SimulationEngine engine, GameState source, IReadOnlyList<long> cuts,
            bool forecast = false, bool restore = false)
        {
            var service = forecast ? new ForecastService(engine) : null;
            var trace = new List<ScheduledEvent>();
            var state = source;
            var status = SimulationStatus.NoChange;
            SimulationDiagnostic diagnostic = null;
            int transactions = 0;
            int thresholds = 0;
            bool advanced = false;
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < cuts.Count; i++)
            {
                var target = new LogicalTime(cuts[i]);
                if (service != null)
                {
                    var result = service.Forecast(state, target);
                    Record(result.State, result.Status, result.Diagnostic, result.Trace, result.CommittedTransactions,
                        result.ThresholdBoundaries);
                }
                else
                {
                    var result = engine.AdvanceTo(state, target);
                    Record(result.State, result.Status, result.Diagnostic, result.Trace, result.CommittedTransactions,
                        result.ThresholdBoundaries);
                }

                if (status != SimulationStatus.Advanced && status != SimulationStatus.NoChange)
                {
                    break;
                }
                if (restore && i == cuts.Count / 2)
                {
                    state = GameStateCloner.Clone(state);
                }
            }

            if (advanced && status == SimulationStatus.NoChange)
            {
                status = SimulationStatus.Advanced;
            }
            return new LabProjection(state, status, diagnostic, trace, transactions, thresholds, watch.Elapsed.TotalMilliseconds);

            void Record(GameState next, SimulationStatus current, SimulationDiagnostic issue,
                IReadOnlyList<ScheduledEvent> events, int count, int thresholdCount)
            {
                state = next;
                status = current;
                diagnostic = issue;
                trace.AddRange(events);
                transactions += count;
                thresholds += thresholdCount;
                advanced |= current == SimulationStatus.Advanced;
            }
        }

        internal static long[] Uniform(long start, long target, int segments)
        {
            long duration = checked(target - start);
            return Enumerable.Range(1, segments)
                .Select(i => start + duration / segments * i + duration % segments * i / segments).ToArray();
        }
    }
}
