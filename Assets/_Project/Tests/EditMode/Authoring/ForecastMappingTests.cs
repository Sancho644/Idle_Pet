using System.Linq;
using IdlePet.Application.Forecast;
using IdlePet.Authoring;
using IdlePet.Authoring.Mapping;
using IdlePet.Domain;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Engine;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Application.Forecast.ForecastTestData;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;
using Effect = IdlePet.Domain.Simulation.Rules.SimulationRuleEffect;

namespace IdlePet.Tests.EditMode.Authoring
{
    public sealed class ForecastMappingTests
    {
        [Test]
        public void RemappedRateAndThresholdChangeNewForecastsAndLeaveExistingResultsIntact()
        {
            using var fixture = new ConfigurationFixture();
            fixture.ChangeNeed(0, property => property.FindPropertyRelative("_baseRatePerHour").stringValue = "-2");
            fixture.SetThresholds("11.345");
            var firstDefinition = Map(fixture.Root);
            var first = new SimulationEngine(firstDefinition);
            var source = new GameState(0, Living(firstDefinition), 42);
            var original = Restore(source);
            var target = At(3_600_000);
            var initialService = new ForecastService(first);
            var initial = initialService.Forecast(source, target);
            var firstSnapshot = Restore(initial.State);

            fixture.ChangeNeed(0, property => property.FindPropertyRelative("_baseRatePerHour").stringValue = "-4");
            fixture.SetThresholds("11.845");
            var second = new SimulationEngine(Map(fixture.Root));
            var updated = new ForecastService(second).Forecast(source, target);

            Matches(first.AdvanceTo(source, target), initial);
            Matches(second.AdvanceTo(source, target), updated);
            Assert.That(initial.State.ActivePet.Needs[NeedId.Satiety].MilliUnits, Is.EqualTo(10_345));
            Assert.That(updated.State.ActivePet.Needs[NeedId.Satiety].MilliUnits, Is.EqualTo(8_345));
            Assert.That(initial.Trace.Single().Request.Timestamp.Milliseconds, Is.EqualTo(1_800_000));
            Assert.That(updated.Trace.Single().Request.Timestamp.Milliseconds, Is.EqualTo(450_000));
            Assert.That(initial.Trace.Single().Request.Payload.Threshold?.MilliUnits, Is.EqualTo(11_345));
            Assert.That(updated.Trace.Single().Request.Payload.Threshold?.MilliUnits, Is.EqualTo(11_845));
            SameState(firstSnapshot, initial.State);
            SameState(firstSnapshot, initialService.Forecast(source, target).State);
            SameState(original, source);
        }

        [Test]
        public void AuthoredCoefficientUsesTheSameExplicitActivationInBothProjections()
        {
            using var fixture = new ConfigurationFixture();
            fixture.SetModifier(0, "test.satiety", NeedId.Satiety, "1.0");
            var firstConfig = Map(fixture.Root);
            var first = WithModifier(firstConfig);
            var source = first.Initialize(At(0), Living(firstConfig), 42, new[] { Event(0, "enable") }).State;
            var initial = Restore(source);
            var target = At(3_600_000);
            var before = new ForecastService(first).Forecast(source, target);
            var beforeSnapshot = Restore(before.State);

            fixture.SetModifier(0, "test.satiety", NeedId.Satiety, "0.5");
            var second = WithModifier(Map(fixture.Root));
            var after = new ForecastService(second).Forecast(source, target);

            Matches(first.AdvanceTo(source, target), before);
            Matches(second.AdvanceTo(source, target), after);
            Assert.That(before.State.ActivePet.Needs[NeedId.Satiety].MilliUnits, Is.EqualTo(10_845));
            Assert.That(after.State.ActivePet.Needs[NeedId.Satiety].MilliUnits, Is.EqualTo(11_595));
            Assert.That(after.State.ActivePet.Needs.Values.Where(p => p.Key != NeedId.Satiety),
                Is.EqualTo(before.State.ActivePet.Needs.Values.Where(p => p.Key != NeedId.Satiety)));
            SameState(initial, source);
            SameState(beforeSnapshot, before.State);
        }

        [Test]
        public void AuthoredBudgetRejectsThenAllowsTheSameCausalChain()
        {
            using var fixture = new ConfigurationFixture();
            ConfigurationFixture.Edit(fixture.Simulation, so => so.FindProperty("_maxEventsPerTimestamp").intValue = 2);
            var first = WithChain(Map(fixture.Root));
            var source = first.Initialize(At(0), null, 42, new[] { Event(1, "A") }).State;
            var original = Restore(source);
            var rejected = new ForecastService(first).Forecast(source, At(1));

            ConfigurationFixture.Edit(fixture.Simulation, so => so.FindProperty("_maxEventsPerTimestamp").intValue = 3);
            var second = WithChain(Map(fixture.Root));
            var accepted = new ForecastService(second).Forecast(source, At(1));

            Matches(first.AdvanceTo(source, At(1)), rejected);
            Matches(second.AdvanceTo(source, At(1)), accepted);
            Assert.That(rejected.Diagnostic.Code, Is.EqualTo(SimulationFaultCode.EventLimit));
            Assert.That(rejected.Diagnostic.Limit, Is.EqualTo(2));
            Assert.That(accepted.Trace, Has.Count.EqualTo(3));
            Assert.That(accepted.State.ProcessedEventsAtCursor, Is.EqualTo(3));
            SameState(original, rejected.State);
            SameState(original, source);
        }

        private static SimulationEngine WithModifier(GameConfiguration cfg) =>
            Engine(cfg, Rule("enable", Effect.SetModifier("test.satiety", true)));

        private static SimulationEngine WithChain(GameConfiguration cfg) => Engine(cfg,
            Rule("A", Effect.Schedule(Event(1, "B"))), Rule("B", Effect.Schedule(Event(1, "C"))), Rule("C"));

        private static GameConfiguration Map(GameConfigSO root)
        {
            var result = GameConfigMapper.Map(root, ConfigurationTarget.Release);
            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Issues));
            return result.Definition;
        }
    }
}
