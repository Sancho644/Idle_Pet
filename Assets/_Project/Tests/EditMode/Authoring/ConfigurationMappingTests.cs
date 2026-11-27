using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IdlePet.Authoring.Mapping;
using IdlePet.Domain;
using IdlePet.Domain.Common.Identifiers;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet;
using IdlePet.Domain.Pet.Needs;
using NUnit.Framework;
using UnityEditor;

namespace IdlePet.Tests.EditMode.Authoring
{
    public sealed class ConfigurationMappingTests
    {
        [Test]
        public void MapsAllSixTypesExactlyWithoutMutatingAssets()
        {
            using var fixture = new ConfigurationFixture();
            fixture.SetThresholds("25.5", "0.125");
            string[] before = fixture.Assets.Select(asset => EditorJsonUtility.ToJson(asset)).ToArray();

            GameConfiguration configuration = Map(fixture);
            var need = configuration.Needs.Entries[NeedId.Satiety];

            Assert.That(configuration.ProfileKind, Is.EqualTo(BalanceProfileKind.Production));
            Assert.That(configuration.ProfileId, Is.EqualTo("test.Production"));
            Assert.That(configuration.Simulation.MaxEventsPerTimestamp, Is.EqualTo(31));
            Assert.That(configuration.Time.VisualDayMilliseconds, Is.EqualTo(780_000L));
            Assert.That(configuration.Time.BiologicalDayMilliseconds, Is.EqualTo(86_400_000L));
            Assert.That(configuration.Time.MinNightMilliseconds, Is.EqualTo(21_600_000L));
            Assert.That(configuration.Time.MaxNightMilliseconds, Is.EqualTo(36_000_000L));
            Assert.That(configuration.Time.ScheduleChangeIntervalMilliseconds, Is.EqualTo(604_800_000L));
            Assert.That(configuration.Needs.Entries, Has.Count.EqualTo(8));
            Assert.That(configuration.Needs.Entries.ContainsKey(NeedId.Comfort), Is.False);
            Assert.That(configuration.Needs.RulesVersion, Is.EqualTo("1.2"));
            Assert.That(configuration.Needs.BalanceVersion, Is.EqualTo("2.3"));
            Assert.That(configuration.Needs.ContentVersion, Is.EqualTo("3.4.5"));
            Assert.That(need.InitialValue.MilliUnits, Is.EqualTo(12_345));
            Assert.That(need.InitialValue.Remainder.Numerator, Is.Zero);
            Assert.That(need.BaseRate.MilliUnitsPerHour, Is.EqualTo(-1_500));
            Assert.That(need.Thresholds.Select(value => value.MilliUnits), Is.EqualTo(new[] { 125L, 25_500L }));
            Assert.That(configuration.RateModifiers["test.satiety"].Id, Is.EqualTo("test.satiety"));
            Assert.That(configuration.RateModifiers["test.satiety"].Target, Is.EqualTo(NeedId.Satiety));
            Assert.That(configuration.RateModifiers["test.satiety"].Coefficient.Millionths, Is.EqualTo(1_250_000));
            Assert.That(configuration.RateModifiers["test.energy"].Coefficient.Millionths, Is.EqualTo(1));
            Assert.That(fixture.Assets.Select(asset => EditorJsonUtility.ToJson(asset)), Is.EqualTo(before));
        }

        [Test]
        public void RemappingUsesChangedAuthoredValuesWithoutChangingTheOldDefinitionOrState()
        {
            using var fixture = new ConfigurationFixture();
            fixture.SetThresholds("25.500");
            GameConfiguration original = Map(fixture);
            var oldState = NeedsState.Create(original.Needs);

            fixture.ChangeNeed(0, need => need.FindPropertyRelative("_initialValue").stringValue = "75.123");
            fixture.SetThresholds("30.250");
            fixture.SetModifier(0, "test.satiety", NeedId.Satiety, "2.25");
            GameConfiguration remapped = Map(fixture);
            var newState = NeedsState.Create(remapped.Needs);

            Assert.That(remapped.Needs.Entries[NeedId.Satiety].Thresholds.Single().MilliUnits, Is.EqualTo(30_250));
            Assert.That(original.Needs.Entries[NeedId.Satiety].Thresholds.Single().MilliUnits, Is.EqualTo(25_500));
            Assert.That(original.RateModifiers["test.satiety"].Coefficient.Millionths, Is.EqualTo(1_250_000));
            Assert.That(remapped.RateModifiers["test.satiety"].Coefficient.Millionths, Is.EqualTo(2_250_000));
            Assert.That(newState[NeedId.Satiety].MilliUnits, Is.EqualTo(75_123));
            Assert.That(oldState[NeedId.Satiety].MilliUnits, Is.EqualTo(12_345));
            Assert.That(original.Needs.Entries[NeedId.Satiety].InitialValue.MilliUnits, Is.EqualTo(12_345));
        }

        [Test]
        public void SharedDefinitionsKeepMultipleGameStatesIndependent()
        {
            using var fixture = new ConfigurationFixture();
            var definitions = Map(fixture).Needs;
            var first = new GameState(0, new PetState(new PetId("first"), NeedsState.Create(definitions)), 42UL);
            var snapshot = first.ActivePet.Needs.Values.ToDictionary(entry => entry.Key, entry => entry.Value);
            snapshot[NeedId.Satiety] = new NeedValue(20_000, new NeedRemainder(125));

            var second = new GameState(20, new PetState(new PetId("second"), NeedsState.Restore(definitions, snapshot)), 42UL);

            Assert.That(second.ActivePet.Needs[NeedId.Satiety].Remainder.Numerator, Is.EqualTo(125));
            Assert.That(first.ActivePet.Needs[NeedId.Satiety].MilliUnits, Is.EqualTo(12_345));
            Assert.That(definitions.Entries[NeedId.Satiety].InitialValue, Is.EqualTo(first.ActivePet.Needs[NeedId.Satiety]));
        }

        [TestCase(BalanceProfileKind.FastTesting)]
        [TestCase(BalanceProfileKind.StressTesting)]
        public void TestingUsesTheExplicitProfileAndReleaseRejectsIt(BalanceProfileKind kind)
        {
            using var fixture = new ConfigurationFixture(kind);

            var testing = GameConfigMapper.Map(fixture.Root, ConfigurationTarget.Testing);
            var release = GameConfigMapper.Map(fixture.Root, ConfigurationTarget.Release);

            Assert.That(testing.Definition.ProfileKind, Is.EqualTo(kind));
            Assert.That(testing.Definition.ProfileId, Is.EqualTo("test." + kind));
            Assert.That(release.Succeeded, Is.False);
            Assert.That(release.Definition, Is.Null);
            Assert.That(release.Issues.Single().Code, Is.EqualTo(ConfigurationIssueCode.NonProductionProfile));
        }

        [Test]
        public void DecimalParsingAndOrdinalModifierIdsAreCultureIndependent()
        {
            using var fixture = new ConfigurationFixture();
            fixture.SetModifier(0, "TEST.I", NeedId.Satiety, "1.25");
            fixture.SetModifier(1, "test.i", NeedId.Energy, "0");
            var before = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                GameConfiguration configuration = Map(fixture);

                Assert.That(configuration.RateModifiers.Keys, Is.EqualTo(new[] { "TEST.I", "test.i" }));
                Assert.That(configuration.RateModifiers["TEST.I"].Coefficient.Millionths, Is.EqualTo(1_250_000));
                Assert.That(configuration.RateModifiers["test.i"].Coefficient.Millionths, Is.Zero);
            }
            finally
            {
                CultureInfo.CurrentCulture = before;
            }
        }

        [Test]
        public void DomainCopiesModifierCollectionsAndEnforcesTheRateMultiplierContract()
        {
            using var fixture = new ConfigurationFixture();
            GameConfiguration mapped = Map(fixture);
            var modifiers = mapped.RateModifiers.Values.ToList();
            var definition = new GameConfiguration(mapped.ProfileKind, mapped.ProfileId, mapped.Simulation,
                mapped.Time, mapped.Needs, modifiers);

            modifiers.Clear();

            Assert.That(definition.RateModifiers, Has.Count.EqualTo(2));
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, RateModifierDefinition>)definition.RateModifiers).Clear());
            Assert.Throws<ArgumentOutOfRangeException>(() => new RateModifierDefinition("id", NeedId.Satiety, new NeedCoefficient(-1)));
        }

        private static GameConfiguration Map(ConfigurationFixture fixture)
        {
            ConfigurationMappingResult result = GameConfigMapper.Map(fixture.Root, ConfigurationTarget.Release);
            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Issues));
            return result.Definition;
        }
    }
}
