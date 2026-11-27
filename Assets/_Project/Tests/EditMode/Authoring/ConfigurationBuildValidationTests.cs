using System;
using System.Linq;
using IdlePet.Authoring;
using IdlePet.Authoring.Mapping;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace IdlePet.Tests.EditMode.Authoring
{
    public sealed class ConfigurationBuildValidationTests
    {
        [Test]
        public void ProductionAssetIsPlayerRootAndPassesTheActualCallbackWithoutChanges()
        {
            string path = GameConfigurationValidation.ProductionAssetPath;
            GameConfigSO root = AssetDatabase.LoadAssetAtPath<GameConfigSO>(path);
            Assert.That(root, Is.Not.Null);
            Assert.That(Resources.Load<GameConfigSO>("IdlePet/GameConfig"), Is.EqualTo(root));
            string[] configPaths = AssetDatabase.GetDependencies(path, true)
                .Where(dependency => dependency.EndsWith(".asset", StringComparison.Ordinal))
                .ToArray();
            Assert.That(configPaths, Has.Length.EqualTo(6));
            string[] before = configPaths.Select(SnapshotAsset).ToArray();

            var result = GameConfigMapper.Map(root, ConfigurationTarget.Release, AssetDatabase.GetAssetPath);
            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Issues));
            Assert.That(GameConfigurationValidation.CheckProduction(), Is.Empty);
            var buildOptions = new BuildPlayerOptions { scenes = new[] { "Assets/Scenes/SampleScene.unity" } };
            Assert.DoesNotThrow(() => new ConfigurationBuildValidator().ValidateBuild(buildOptions));

            Assert.That(configPaths.Select(SnapshotAsset), Is.EqualTo(before));
            Assert.That(result.Definition.ProfileKind, Is.EqualTo(BalanceProfileKind.Production));
            Assert.That(result.Definition.ProfileId, Is.EqualTo("production"));
            Assert.That(result.Definition.RateModifiers, Is.Empty);
            Assert.That(result.Definition.Time.VisualDayMilliseconds, Is.EqualTo(7_200_000));
            Assert.That(result.Definition.Time.BiologicalDayMilliseconds, Is.EqualTo(86_400_000));
            Assert.That(result.Definition.Time.MinNightMilliseconds, Is.EqualTo(21_600_000));
            Assert.That(result.Definition.Time.MaxNightMilliseconds, Is.EqualTo(36_000_000));
            Assert.That(result.Definition.Time.ScheduleChangeIntervalMilliseconds, Is.EqualTo(604_800_000));
            Assert.That(result.Definition.Needs.BalanceVersion, Is.EqualTo("0.1"));
        }

        [TestCase(NeedId.Satiety, 80_000L, -6_000L, 10_000L)]
        [TestCase(NeedId.Energy, 90_000L, -5_000L, 5_000L)]
        [TestCase(NeedId.Hygiene, 100_000L, -3_000L, 10_000L)]
        [TestCase(NeedId.Health, 100_000L, 0L, -1L)]
        [TestCase(NeedId.Happiness, 70_000L, 0L, -1L)]
        [TestCase(NeedId.Boredom, 10_000L, 5_000L, -1L)]
        [TestCase(NeedId.Discipline, 20_000L, 0L, -1L)]
        [TestCase(NeedId.Bond, 0L, 0L, -1L)]
        public void ProductionNeedsUseApprovedBalance(NeedId id, long initial, long rate, long threshold)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameConfigSO>(GameConfigurationValidation.ProductionAssetPath);
            var result = GameConfigMapper.Map(root, ConfigurationTarget.Release);
            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Issues));

            var entry = result.Definition.Needs.Entries[id];

            Assert.That(entry.Minimum.MilliUnits, Is.Zero);
            Assert.That(entry.Maximum.MilliUnits, Is.EqualTo(100_000));
            Assert.That(entry.InitialValue.MilliUnits, Is.EqualTo(initial));
            Assert.That(entry.InitialValue.Remainder.Numerator, Is.Zero);
            Assert.That(entry.BaseRate.MilliUnitsPerHour, Is.EqualTo(rate));
            long[] expectedThresholds = threshold < 0 ? Array.Empty<long>() : new[] { threshold };
            Assert.That(entry.Thresholds.Select(value => value.MilliUnits), Is.EqualTo(expectedThresholds));
        }

        [Test]
        public void CallbackStopsWhenTheActualMappingReportsACriticalError()
        {
            using var fixture = new ConfigurationFixture();
            ConfigurationFixture.Edit(fixture.Simulation, s => s.FindProperty("_maxEventsPerTimestamp").intValue = 0);
            const string path = "Assets/TestFixtures/InvalidSimulation.asset";
            string before = EditorJsonUtility.ToJson(fixture.Simulation);
            var validator = new ConfigurationBuildValidator(_ =>
                GameConfigMapper.Map(fixture.Root, ConfigurationTarget.Release, asset => path).Issues);

            var error = Assert.Throws<BuildFailedException>(() => validator.ValidateBuild(default));

            Assert.That(error.Message, Does.Contain(path).And.Contain("InvalidEventLimit").And.Contain("MaxEventsPerTimestamp"));
            Assert.That(EditorJsonUtility.ToJson(fixture.Simulation), Is.EqualTo(before));
        }

        [Test]
        public void CallbackRejectsATestProfileAtThePlayerRoot()
        {
            using var fixture = new ConfigurationFixture(BalanceProfileKind.FastTesting);
            var validator = new ConfigurationBuildValidator(_ => GameConfigurationValidation.CheckProduction(
                new[] { fixture.Root }, new[] { fixture.Profile }, asset => GameConfigurationValidation.ProductionAssetPath));

            var error = Assert.Throws<BuildFailedException>(() => validator.ValidateBuild(default));

            Assert.That(error.Message, Does.Contain("NonProductionProfile").And.Contain("Kind"));
        }

        [Test]
        public void ProductionDiscoveryRejectsZeroOrMultipleRoots()
        {
            using var first = new ConfigurationFixture();
            using var second = new ConfigurationFixture();

            var missing = GameConfigurationValidation.CheckProduction(Array.Empty<GameConfigSO>(),
                Array.Empty<BalanceProfileSO>(), _ => "unused");
            var ambiguous = GameConfigurationValidation.CheckProduction(new[] { first.Root, second.Root },
                Array.Empty<BalanceProfileSO>(), asset => asset == first.Root ? "Assets/First.asset" : "Assets/Second.asset");

            Assert.That(missing.Single().Code, Is.EqualTo(ConfigurationIssueCode.MissingRoot));
            Assert.That(ambiguous.Single().Code, Is.EqualTo(ConfigurationIssueCode.AmbiguousRoot));
            Assert.That(ambiguous.Single().Asset, Does.Contain("Assets/First.asset").And.Contain("Assets/Second.asset"));
        }

        [Test]
        public void ProductionRootMustBeAtThePlayerResourcesPath()
        {
            using var fixture = new ConfigurationFixture();
            const string path = "Assets/SomeOtherPath/GameConfig.asset";

            var issues = GameConfigurationValidation.CheckProduction(new[] { fixture.Root }, new[] { fixture.Profile }, _ => path);

            Assert.That(issues.Single().Code, Is.EqualTo(ConfigurationIssueCode.IncorrectRootPath));
            Assert.That(issues.Single().Asset, Is.EqualTo(path));
        }

        [TestCase("Assets/TestProfiles/FastTesting.asset", true)]
        [TestCase("Assets/Editor/TestProfiles/FastTesting.asset", false)]
        public void TestProfileAssetsRequireEditorOnlyPlacement(string path, bool expectError)
        {
            using var production = new ConfigurationFixture();
            using var testing = new ConfigurationFixture(BalanceProfileKind.FastTesting);

            var issues = GameConfigurationValidation.CheckProduction(new[] { production.Root },
                new[] { production.Profile, testing.Profile },
                asset => asset == testing.Profile ? path : GameConfigurationValidation.ProductionAssetPath);

            if (expectError)
            {
                Assert.That(issues.Single().Code, Is.EqualTo(ConfigurationIssueCode.NonProductionProfile));
                Assert.That(issues.Single().Asset, Is.EqualTo(path));
            }
            else
            {
                Assert.That(issues, Is.Empty);
            }
        }

        private static string SnapshotAsset(string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            return EditorUtility.IsDirty(asset) + ":" + EditorJsonUtility.ToJson(asset);
        }
    }
}
