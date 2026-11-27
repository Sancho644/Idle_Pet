using System;
using System.Linq;
using IdlePet.Authoring;
using IdlePet.Authoring.Mapping;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Modifiers;
using IdlePet.Domain.Simulation.Time;
using IdlePet.Editor;
using NUnit.Framework;
using UnityEditor;

namespace IdlePet.Tests.EditMode.Authoring
{
    public sealed class NeedsIntegrationMappingTests
    {
        [TestCase(0L, 80L, 90L, 100L, 10L)]
        [TestCase(1L, 74L, 85L, 97L, 15L)]
        [TestCase(4L, 56L, 70L, 88L, 30L)]
        [TestCase(8L, 32L, 50L, 76L, 50L)]
        [TestCase(12L, 8L, 30L, 64L, 70L)]
        [TestCase(24L, 0L, 0L, 28L, 100L)]
        public void ProductionWakingReferenceValuesComeFromTheActualAssets(long hours, long satiety, long energy, long hygiene, long boredom)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameConfigSO>(GameConfigurationValidation.ProductionAssetPath);
            var definition = Map(root);
            var rates = NeedRateCalculator.Calculate(definition, Array.Empty<string>());
            var initial = NeedsState.Create(definition.Needs);

            var final = NeedsIntegrator.Integrate(initial, definition.Needs, rates, LogicalDuration.FromHours(hours));

            Assert.That(definition.RateModifiers, Is.Empty);
            Assert.That(final[NeedId.Satiety].MilliUnits, Is.EqualTo(satiety * 1000));
            Assert.That(final[NeedId.Energy].MilliUnits, Is.EqualTo(energy * 1000));
            Assert.That(final[NeedId.Hygiene].MilliUnits, Is.EqualTo(hygiene * 1000));
            Assert.That(final[NeedId.Boredom].MilliUnits, Is.EqualTo(boredom * 1000));
            Assert.That(final[NeedId.Health], Is.EqualTo(initial[NeedId.Health]));
            Assert.That(final[NeedId.Happiness], Is.EqualTo(initial[NeedId.Happiness]));
            Assert.That(final[NeedId.Discipline], Is.EqualTo(initial[NeedId.Discipline]));
            Assert.That(final[NeedId.Bond], Is.EqualTo(initial[NeedId.Bond]));
            Assert.That(final.Values.Values.Select(value => value.Remainder.Numerator), Is.All.Zero);
        }

        [Test]
        public void RemappingAnAuthoredBaseRateChangesOnlyTheNewIntegration()
        {
            using var fixture = new ConfigurationFixture();
            fixture.ChangeNeed(0, need => need.FindPropertyRelative("_baseRatePerHour").stringValue = "-2");
            var before = Map(fixture.Root);
            var initial = NeedsState.Create(before.Needs);
            var previous = IntegrateHour(before, initial, Array.Empty<string>());

            fixture.ChangeNeed(0, need => need.FindPropertyRelative("_baseRatePerHour").stringValue = "-7");
            var after = Map(fixture.Root);
            var remapped = IntegrateHour(after, NeedsState.Create(after.Needs), Array.Empty<string>());

            Assert.That(before.Needs.Entries[NeedId.Satiety].BaseRate.MilliUnitsPerHour, Is.EqualTo(-2000));
            Assert.That(after.Needs.Entries[NeedId.Satiety].BaseRate.MilliUnitsPerHour, Is.EqualTo(-7000));
            Assert.That(initial[NeedId.Satiety].MilliUnits, Is.EqualTo(12_345));
            Assert.That(previous[NeedId.Satiety].MilliUnits, Is.EqualTo(10_345));
            Assert.That(remapped[NeedId.Satiety].MilliUnits, Is.EqualTo(5_345));
            Assert.That(IntegrateHour(before, initial, Array.Empty<string>()).Values, Is.EqualTo(previous.Values));
        }

        [Test]
        public void RemappingACoefficientOnlyChangesAnExplicitlyActivatedTarget()
        {
            using var fixture = new ConfigurationFixture();
            var before = Map(fixture.Root);
            var initial = NeedsState.Create(before.Needs);
            var active = new[] { "test.satiety" };
            var previous = IntegrateHour(before, initial, active);

            fixture.SetModifier(0, "test.satiety", NeedId.Satiety, "0.5");
            var after = Map(fixture.Root);
            var remapped = IntegrateHour(after, NeedsState.Create(after.Needs), active);

            Assert.That(before.RateModifiers["test.satiety"].Coefficient.Millionths, Is.EqualTo(1_250_000));
            Assert.That(after.RateModifiers["test.satiety"].Coefficient.Millionths, Is.EqualTo(500_000));
            Assert.That(initial[NeedId.Satiety].MilliUnits, Is.EqualTo(12_345));
            Assert.That(previous[NeedId.Satiety].MilliUnits, Is.EqualTo(10_470));
            Assert.That(remapped[NeedId.Satiety].MilliUnits, Is.EqualTo(11_595));
            Assert.That(remapped.Values.Where(pair => pair.Key != NeedId.Satiety),
                Is.EqualTo(previous.Values.Where(pair => pair.Key != NeedId.Satiety)));
            Assert.That(IntegrateHour(after, initial, Array.Empty<string>()).Values,
                Is.EqualTo(IntegrateHour(before, initial, Array.Empty<string>()).Values));
        }

        private static GameConfiguration Map(GameConfigSO root)
        {
            var mapped = GameConfigMapper.Map(root, ConfigurationTarget.Release);
            Assert.That(mapped.Succeeded, Is.True, string.Join("\n", mapped.Issues));
            return mapped.Definition;
        }

        private static NeedsState IntegrateHour(GameConfiguration definition, NeedsState initial, string[] active)
        {
            var rates = NeedRateCalculator.Calculate(definition, active);
            return NeedsIntegrator.Integrate(initial, definition.Needs, rates, LogicalDuration.FromHours(1));
        }
    }
}
