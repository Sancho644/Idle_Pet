using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IdlePet.Authoring;
using IdlePet.Authoring.Mapping;
using IdlePet.Domain;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Editor;
using NUnit.Framework;
using UnityEditor;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;

namespace IdlePet.Tests.EditMode.Authoring
{
    public sealed class SimulationEngineMappingTests
    {
        [TestCase(256, true)]
        [TestCase(257, false)]
        public void ActualProductionBudgetAcceptsExactly256AndRejects257(int count, bool allowed)
        {
            var cfg = LoadProduction();
            Assert.That(cfg.Simulation.MaxEventsPerTimestamp, Is.EqualTo(256));
            var engine = WithChain(cfg);
            var state = engine.Initialize(At(0), null, 42, new[] { Chain(1, count) }).State;
            var result = engine.AdvanceTo(state, At(1));

            if (allowed)
            {
                Assert.That(result.Status, Is.EqualTo(SimulationStatus.Advanced));
                Assert.That(result.Trace, Has.Count.EqualTo(256));
                Assert.That(result.State.ProcessedEventsAtCursor, Is.EqualTo(256));
            }
            else
            {
                Assert.That(result.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.EventLimit));
                Assert.That(result.Diagnostic.Event.Sequence, Is.EqualTo(256));
                Assert.That(result.Trace, Is.Empty);
                SameState(state, result.State);
            }
        }

        [Test]
        public void SeparateTimestampsUseSeparateBudgetsWithoutAnOfflineEventLimit()
        {
            var cfg = LoadProduction();
            var engine = WithChain(cfg);
            var state = engine.Initialize(At(0), null, 42, new[] { Chain(1, 256), Chain(10_000, 256) }).State;
            var result = engine.AdvanceTo(state, At(10_000));

            Assert.That(result.Status, Is.EqualTo(SimulationStatus.Advanced));
            Assert.That(result.Trace, Has.Count.EqualTo(512));
            Assert.That(result.State.EventQueue.NextSequence, Is.EqualTo(512));
            Assert.That(result.State.ProcessedEventsAtCursor, Is.EqualTo(256));
        }

        [Test]
        public void ChangingAuthoredBudgetChangesTheCausalChainAcceptance()
        {
            using var fixture = new ConfigurationFixture();
            ConfigurationFixture.Edit(fixture.Simulation, so => so.FindProperty("_maxEventsPerTimestamp").intValue = 2);
            var before = Map(fixture.Root);
            var first = WithChain(before);
            var state = first.Initialize(At(0), null, 42, new[] { Chain(1, 3) }).State;
            var failure = first.AdvanceTo(state, At(1));
            ConfigurationFixture.Edit(fixture.Simulation, so => so.FindProperty("_maxEventsPerTimestamp").intValue = 3);
            var after = Map(fixture.Root);
            var successful = WithChain(after).AdvanceTo(state, At(1));

            Assert.That(before.Simulation.MaxEventsPerTimestamp, Is.EqualTo(2));
            Assert.That(after.Simulation.MaxEventsPerTimestamp, Is.EqualTo(3));
            Assert.That(failure.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.EventLimit));
            SameState(state, failure.State);
            Assert.That(successful.Trace, Has.Count.EqualTo(3));
        }

        [Test]
        public void AuthoredRateAndThresholdChangesAffectValuesAndMathematicalBoundaries()
        {
            using var fixture = new ConfigurationFixture();
            fixture.ChangeNeed(0, property => property.FindPropertyRelative("_baseRatePerHour").stringValue = "-2");
            fixture.SetThresholds("11.345");
            var before = Map(fixture.Root);
            var state = new GameState(0, Living(before), 42);
            var first = CreateEngine(before).AdvanceTo(state, At(3_600_000));
            fixture.ChangeNeed(0, property => property.FindPropertyRelative("_baseRatePerHour").stringValue = "-4");
            fixture.SetThresholds("11.845");
            var after = Map(fixture.Root);
            var second = CreateEngine(after).AdvanceTo(state, At(3_600_000));

            Assert.That(first.Trace.Single().Request.Timestamp.Milliseconds, Is.EqualTo(1_800_000));
            Assert.That(first.Trace.Single().Request.Payload.Threshold?.MilliUnits, Is.EqualTo(11_345));
            Assert.That(second.Trace.Single().Request.Timestamp.Milliseconds, Is.EqualTo(450_000));
            Assert.That(second.Trace.Single().Request.Payload.Threshold?.MilliUnits, Is.EqualTo(11_845));
            Assert.That(first.State.ActivePet.Needs[NeedId.Satiety].MilliUnits, Is.EqualTo(10_345));
            Assert.That(second.State.ActivePet.Needs[NeedId.Satiety].MilliUnits, Is.EqualTo(8_345));
            Assert.That(before.Needs.Entries[NeedId.Satiety].BaseRate.MilliUnitsPerHour, Is.EqualTo(-2_000));
            Assert.That(state.ActivePet.Needs[NeedId.Satiety].MilliUnits, Is.EqualTo(12_345));
        }

        [Test]
        public void AuthoredCoefficientChangesOnlyTheExplicitlyActivatedNeed()
        {
            using var fixture = new ConfigurationFixture();
            var before = Map(fixture.Root);
            fixture.SetModifier(0, "test.satiety", NeedId.Satiety, "0.5");
            var after = Map(fixture.Root);
            var original = Start(before);
            var initial = CreateEngine(before).AdvanceTo(original, At(3_600_000));
            var updated = CreateEngine(after).AdvanceTo(Start(after), At(3_600_000));

            Assert.That(initial.State.ActivePet.Needs[NeedId.Satiety].MilliUnits, Is.EqualTo(10_470));
            Assert.That(updated.State.ActivePet.Needs[NeedId.Satiety].MilliUnits, Is.EqualTo(11_595));
            Assert.That(initial.State.ActivePet.Needs.Values.Where(p => p.Key != NeedId.Satiety),
                Is.EqualTo(updated.State.ActivePet.Needs.Values.Where(p => p.Key != NeedId.Satiety)));
            Assert.That(original.ActivePet.Needs[NeedId.Satiety].MilliUnits, Is.EqualTo(12_345));

            GameState Start(GameConfiguration configuration) => CreateEngine(configuration,
                ("enable", (c, _) => c.SetModifierActive("test.satiety", true)))
                .Initialize(At(0), Living(configuration), 42, new[] { Event(0, "enable") }).State;
        }

        private static EventRequest Chain(long time, int count) => Event(time, "chain", text: count.ToString(CultureInfo.InvariantCulture));

        private static SimulationEngine WithChain(GameConfiguration cfg) => CreateEngine(cfg, ("chain", (c, e) =>
        {
            int remaining = int.Parse(e.Request.Payload.Text, CultureInfo.InvariantCulture) - 1;
            if (remaining > 0)
            {
                c.Schedule(Chain(c.Time.Milliseconds, remaining));
            }
        }));

        private static GameConfiguration LoadProduction() =>
            Map(AssetDatabase.LoadAssetAtPath<GameConfigSO>(GameConfigurationValidation.ProductionAssetPath));

        private static GameConfiguration Map(GameConfigSO root)
        {
            var result = GameConfigMapper.Map(root, ConfigurationTarget.Release);
            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Issues));
            return result.Definition;
        }
    }
}
