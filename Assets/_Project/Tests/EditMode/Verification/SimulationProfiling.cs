using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using IdlePet.Application.Forecast;
using IdlePet.Authoring;
using IdlePet.Domain;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Modifiers;
using IdlePet.Domain.Simulation.Random;
using IdlePet.Domain.Simulation.Time;
using IdlePet.Domain.State;
using IdlePet.Editor;
using IdlePet.Editor.BalanceLab;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using static IdlePet.Tests.EditMode.Application.Forecast.ForecastTestData;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;
using Effect = IdlePet.Domain.Simulation.Rules.SimulationRuleEffect;

namespace IdlePet.Tests.EditMode.Verification
{
    // Opt-in profiling entry point. NUnit runs correctness checks without timing assertions.
    public static class SimulationProfiling
    {
        private const int Series = 5;
        private const ulong Seed = 42;
        private static readonly ProfilerMarker MeasureMarker = new ProfilerMarker("IdlePet.T01.03b.Measure");

        public static object Run()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameConfigSO>(GameConfigurationValidation.ProductionAssetPath);
            var production = LabProfiles.Map(root, BalanceProfileKind.Production).Definition;
            var stress = LabProfiles.Map(root, BalanceProfileKind.StressTesting).Definition;
            var cases = new List<object>();
            var living = new GameState(0, Living(production), Seed);
            var engine = new SimulationEngine(production);
            foreach (int hours in new[] { 8, 12, 24, 168, 8760 })
            {
                Advance($"production-{hours}h", production, engine, living, hours * 3_600_000L, 100, cases);
                Advance($"forecast-production-{hours}h", production, engine, living, hours * 3_600_000L, 100, cases, forecast: true);
            }

            var stressEngine = new SimulationEngine(stress);
            foreach (int count in new[] { 0, 8, 32 })
            {
                var definition = count == 0 ? production : stress;
                var ids = definition.RateModifiers.Keys.Take(count).ToArray();
                var sample = GameState.Restore(0, Living(definition), living.Rng, living.EventQueue, ids, 0);
                var rates = NeedRateCalculator.Calculate(definition, ids);
                var needs = sample.ActivePet.Needs;
                var need = definition.Needs.Entries[NeedId.Satiety];
                var duration = new LogicalDuration(31_536_000_000L);
                cases.Add(Measure($"rate-calculator-{count}", definition, sample, 100, () => NeedRateCalculator.Calculate(definition, ids)));
                cases.Add(Measure($"needs-integrator-{count}", definition, sample, 100, () => NeedsIntegrator.Integrate(needs, definition.Needs, rates, duration)));
                cases.Add(Measure($"threshold-calculator-{count}", definition, sample, 100,
                    () => NeedThresholdCalculator.FindCrossings(needs[NeedId.Satiety], rates[NeedId.Satiety], duration, need)));
                cases.Add(Measure($"rates-materialize-{count}", definition, sample, 100, () =>
                    new EffectiveNeedRates(definition.Needs.Entries.Keys.Select(id => KeyValuePair.Create(id, rates[id])))));
                cases.Add(Measure($"needs-restore-{count}", definition, sample, 100, () => NeedsState.Restore(definition.Needs, needs.Values)));
                cases.Add(Measure($"need-integrator-extreme-{count}", definition, sample, 100,
                    () => NeedIntegrator.Integrate(needs[NeedId.Satiety], new NeedRate(long.MinValue), new LogicalDuration(long.MaxValue), need)));
                if (count != 0)
                {
                    Advance($"stress-{count}-365d", stress, stressEngine, sample, duration.Milliseconds, 50, cases);
                    Advance($"forecast-stress-{count}-365d", stress, stressEngine, sample, duration.Milliseconds, 50, cases, forecast: true);
                }
            }

            var rules = Enumerable.Range(0, 64).Select(index => index == 63 ? Rule(Chain(index)) :
                Rule(Chain(index), Effect.Schedule(Event(10, Chain(index + 1))))).Append(Rule("mark")).ToArray();
            var eventEngine = Engine(production, rules);
            var chain = eventEngine.Initialize(At(0), null, Seed, new[] { Event(10, Chain(0)) }).State;
            Advance("causal-chain-64", production, eventEngine, chain, 10, 60, cases);
            Advance("forecast-causal-chain-64", production, eventEngine, chain, 10, 60, cases, forecast: true);
            cases.Add(Measure("rng-restore", production, living, 100, () => RandomStreams.Restore(living.Rng)));
            var streams = RandomStreams.Restore(living.Rng);
            cases.Add(Measure("rng-capture", production, living, 100, streams.CaptureSnapshot));
            foreach (int size in new[] { 0, 128, 1024, 4096 })
            {
                var events = Enumerable.Range(0, size).Select(index => new ScheduledEvent(Event(10 + index), index)).ToArray();
                var state = GameState.Restore(0, null, living.Rng, new EventQueueSnapshot(events, size), Array.Empty<string>(), 0);
                int activeRepeats = size < 1024 ? 5 : 1;
                cases.Add(Measure($"queue-snapshot-{size}", production, state, 50, () => new EventQueueSnapshot(events, size)));
                cases.Add(Measure($"scheduler-restore-{size}", production, state, 50, () => new EventScheduler(state.EventQueue)));
                cases.Add(Measure($"world-clone-{size}", production, state, 50, () => GameStateCloner.Clone(state)));
                cases.Add(Measure($"scheduler-add-cancel-{size}", production, state, 30, () =>
                {
                    var scheduler = new EventScheduler(state.EventQueue);
                    long sequence = scheduler.Register(At(0), Event(size + 20)).Sequence;
                    if (!scheduler.Cancel(sequence))
                    {
                        throw new InvalidOperationException("The inserted event was not cancelled.");
                    }
                    return scheduler.Capture();
                }));
                Advance($"future-queue-{size}", production, eventEngine, state, 5, 40, cases);
                Advance($"forecast-future-queue-{size}", production, eventEngine, state, 5, 40, cases, forecast: true);
                Advance($"one-event-with-future-queue-{size}", production, eventEngine, state, 10, 40, cases);
                Advance($"active-queue-{size}", production, eventEngine, state, size + 10, activeRepeats, cases);
                Advance($"forecast-active-queue-{size}", production, eventEngine, state, size + 10, activeRepeats, cases, forecast: true);
            }

            byte[] control = null;
            int allocated = Allocations(() => control = new byte[64], 20);
            GC.KeepAlive(control);
            return new
            {
                unity = UnityEngine.Application.unityVersion,
                runtime = Type.GetType("Mono.Runtime") != null ? "Editor Mono" : "Editor .NET",
                os = SystemInfo.operatingSystem, cpu = SystemInfo.processorType, series = Series,
                positiveAllocationControl = allocated, expectedAllocationControl = 20,
                methodology = "Stopwatch around individual scenario batches with separate main-thread GC.Alloc counts. " +
                    "Five series, per-case warmups and repetitions below. Counts are events, not bytes. Kernel operation costs are isolated microbenchmarks.",
                cases
            };
        }

        private static string Chain(int index) => "chain." + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static void Advance(string name, GameConfiguration config, SimulationEngine engine, GameState source,
            long target, int repeats, List<object> cases, bool forecast = false)
        {
            var time = At(target);
            var sample = engine.AdvanceTo(source, time);
            if (sample.Status != SimulationStatus.Advanced)
            {
                throw new InvalidOperationException(sample.Diagnostic?.Reason ?? sample.Status.ToString());
            }
            var service = new ForecastService(engine);
            Func<object> run = forecast ? () => service.Forecast(source, time) : () => engine.AdvanceTo(source, time);
            cases.Add(Measure(name, config, source, repeats, run, target, sample.Trace.Count,
                sample.ThresholdBoundaries, sample.CommittedTransactions));
        }

        private static object Measure(string name, GameConfiguration config, GameState source, int repeats,
            Func<object> action, long target = 0, int events = 0, int thresholds = 0, int transactions = 0)
        {
            int warmups = Math.Min(8, repeats);
            object sink = null;
            for (int i = 0; i < warmups; i++)
            {
                sink = action();
            }
            var series = new double[Series];
            for (int i = 0; i < Series; i++)
            {
                using var marker = MeasureMarker.Auto();
                long start = Stopwatch.GetTimestamp();
                for (int j = 0; j < repeats; j++)
                {
                    sink = action();
                }
                series[i] = (Stopwatch.GetTimestamp() - start) * 1_000_000.0 / Stopwatch.Frequency / repeats;
            }
            int allocationRuns = Math.Min(20, repeats);
            double allocationEvents = (double)Allocations(() => sink = action(), allocationRuns) / allocationRuns;
            GC.KeepAlive(sink);
            return new
            {
                name, config.ProfileId, rulesVersion = config.Needs.RulesVersion, balanceVersion = config.Needs.BalanceVersion,
                contentVersion = config.Needs.ContentVersion, seed = Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                intervalMs = target, queue = source.EventQueue.Events.Count, modifiers = source.ActiveModifierIds.Count,
                events, thresholds, transactions, warmups, repeats, seriesMicroseconds = series,
                medianUs = series.OrderBy(v => v).ElementAt(Series / 2), minUs = series.Min(), maxUs = series.Max(),
                allocationRuns, allocationEvents
            };
        }

        private static int Allocations(Action action, int repetitions)
        {
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1,
                ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            for (int i = 0; i < repetitions; i++)
            {
                action();
            }
            recorder.Stop();
            return recorder.Count == 0 ? 0 : (int)recorder.GetSample(0).Count;
        }
    }
}
