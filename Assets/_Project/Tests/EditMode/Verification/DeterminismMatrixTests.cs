using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using IdlePet.Authoring;
using IdlePet.Domain;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Random;
using IdlePet.Editor;
using IdlePet.Editor.BalanceLab;
using NUnit.Framework;
using UnityEditor;
using static IdlePet.Tests.EditMode.Application.Forecast.ForecastTestData;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;
using Effect = IdlePet.Domain.Simulation.Rules.SimulationRuleEffect;

namespace IdlePet.Tests.EditMode.Verification
{
    public sealed class DeterminismMatrixTests
    {
        private static readonly long[] Durations = { 60_000, 3_600_000, 28_800_000, 86_400_000, 604_800_000, 31_536_000_000 };
        private static readonly ulong[] Seeds = { 0, 1, 42, ulong.MaxValue };
        private const ulong PartitionSeed = 0x132685f3UL;
        private const long Origin = 19;

        private static IEnumerable<TestCaseData> Cases()
        {
            foreach (BalanceProfileKind profile in new[] { BalanceProfileKind.Production, BalanceProfileKind.FastTesting, BalanceProfileKind.StressTesting })
            foreach (long duration in Durations)
            foreach (ulong seed in Seeds)
            {
                yield return new TestCaseData(profile, duration, seed);
            }
        }

        [TestCaseSource(nameof(Cases))]
        public void EveryProjectionPreservesFullStateTraceAndSource(BalanceProfileKind profile, long duration, ulong seed)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameConfigSO>(GameConfigurationValidation.ProductionAssetPath);
            var mapped = LabProfiles.Map(root, profile);
            Assert.That(mapped.Succeeded, Is.True, string.Join("\n", mapped.Issues));
            var definition = mapped.Definition;
            long target = Origin + duration;
            var (engine, source) = CreateWorld(definition, target, seed);
            var before = LabReport.State("source", source);
            var baseline = LabRunner.Project(engine, source, new[] { target });
            Assert.That(baseline.Status, Is.EqualTo(SimulationStatus.Advanced), baseline.Diagnostic?.Reason);
            var irregular = Irregular(Origin, target, 31, PartitionSeed ^ seed);
            var boundaries = baseline.Trace.SelectMany(e => new[] { e.Request.Timestamp.Milliseconds - 1,
                    e.Request.Timestamp.Milliseconds, e.Request.Timestamp.Milliseconds + 1 })
                .Concat(new[] { Origin, Origin, target, target }).Where(value => value >= Origin && value <= target)
                .OrderBy(value => value).ToArray();
            (string Name, long[] Cuts, bool Forecast, bool Restore)[] variants =
            {
                ("uniform7", LabRunner.Uniform(Origin, target, 7), false, false),
                ("uniform31", LabRunner.Uniform(Origin, target, 31), false, false),
                ("uniform128", LabRunner.Uniform(Origin, target, 128), false, false),
                ("irregular31", irregular, false, false),
                ("event-threshold-boundaries", boundaries, false, false),
                ("restore-midpoint", irregular, false, true),
                ("forecast", new[] { target }, true, false),
                ("partitioned-forecast", boundaries, true, true),
            };
            var evidence = new XElement("matrixCase", new XAttribute("scenarioId", "full-state-parity"),
                new XAttribute("profile", profile), new XAttribute("seed", seed), new XAttribute("origin", Origin),
                new XAttribute("target", target), new XAttribute("duration", duration),
                new XAttribute("partitionSeed", PartitionSeed ^ seed),
                new XAttribute("events", baseline.Trace.Count), new XAttribute("baselineTransactions", baseline.Transactions));
            foreach (var variant in variants)
            {
                var candidate = LabRunner.Project(engine, source, variant.Cuts, variant.Forecast, variant.Restore);
                var difference = LabReport.Difference(baseline, candidate);
                Assert.That(difference, Is.Null, () => string.Format(CultureInfo.InvariantCulture,
                    "full-state-parity seed={0} profile={1} target={2} partition={3} cuts=[{4}]. {5}",
                    seed, profile, target, variant.Name, string.Join(",", variant.Cuts), difference));
                Assert.That(LabReport.Difference(before, LabReport.State("source", source), ""), Is.Null);
                evidence.Add(new XElement("comparison", new XAttribute("strategy", variant.Name),
                    new XAttribute("transactions", candidate.Transactions), new XAttribute("steps", variant.Cuts.Length),
                    new XAttribute("cuts", string.Join(",", variant.Cuts.Select(n => n.ToString(CultureInfo.InvariantCulture))))));
            }
            TestContext.Out.WriteLine("IDLEPET_MATRIX " + evidence.ToString(SaveOptions.DisableFormatting));
        }

        private static (SimulationEngine, GameState) CreateWorld(GameConfiguration cfg, long target, ulong seed)
        {
            var values = cfg.Needs.Entries.Values.Select(need => KeyValuePair.Create(need.Id,
                NeedValue.FromExactNumerator(need.InitialValue.ExactNumerator + (need.InitialValue.Equals(need.Maximum) ? -1 : 1))));
            var pet = new PetState(Pet, NeedsState.Restore(cfg.Needs, values));
            if (cfg.ProfileKind == BalanceProfileKind.Production)
            {
                return (new SimulationEngine(cfg), new GameState(Origin, pet, seed));
            }
            string mod = cfg.RateModifiers.Keys.First();
            var engine = Engine(cfg, Rule("A", Effect.DrawBounded(RandomStreamId.Behaviour, 0x80000001),
                    Effect.Draw(RandomStreamId.Birth), Effect.Schedule(Event(100, "C", 5)),
                    Effect.Schedule(Event(target + 1, "future", text: "beyond target"))),
                Rule("B", Effect.Draw(RandomStreamId.Illness)), Rule("C", Effect.Draw(RandomStreamId.Evolution)),
                Rule("enable", Effect.SetModifier(mod, true)), Rule("disable", Effect.SetModifier(mod, false)), Rule("future"));
            var initialized = engine.Initialize(At(Origin), pet, seed, new[]
            {
                Event(Origin, "enable"), Event(100, "A"), Event(100, "B", 20), Event(101, "disable"),
                Event(110, "enable"), Event(target, "future")
            });
            Assert.That(initialized.Status, Is.EqualTo(SimulationStatus.Initialized));
            var state = initialized.State;
            if (cfg.ProfileKind == BalanceProfileKind.StressTesting)
            {
                state = GameState.Restore(Origin, pet, state.Rng, state.EventQueue, cfg.RateModifiers.Keys,
                    state.ProcessedEventsAtCursor);
            }
            return (engine, state);
        }

        private static long[] Irregular(long start, long target, int segments, ulong seed)
        {
            ulong state = seed;
            ulong width = (ulong)(target - start) + 1UL;
            var cuts = new long[segments];
            for (int i = 0; i < segments - 1; i++)
            {
                state = unchecked(state * 6364136223846793005UL + 1442695040888963407UL);
                cuts[i] = start + (long)(state % width);
            }
            cuts[segments - 1] = target;
            Array.Sort(cuts);
            return cuts;
        }
    }
}
