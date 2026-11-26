using System.Linq;
using IdlePet.Authoring.Mapping;
using IdlePet.Domain.Pet.Needs;
using NUnit.Framework;

namespace IdlePet.Tests.EditMode.Authoring
{
    public sealed class ConfigurationValidationTests
    {
        [Test]
        public void RejectsMissingRootAndUnknownTarget()
        {
            var result = GameConfigMapper.Map(null, (ConfigurationTarget)99);

            Assert.That(result.Definition, Is.Null);
            Assert.That(result.Issues.Select(issue => issue.Code), Is.EquivalentTo(new[]
            {
                ConfigurationIssueCode.MissingRoot, ConfigurationIssueCode.UnknownEnumValue
            }));
        }

        [Test]
        public void MissingProductionProfileHasNoFallback()
        {
            using var fixture = new ConfigurationFixture();
            ConfigurationFixture.Edit(fixture.Root, s => s.FindProperty("_profile").objectReferenceValue = null);

            var release = GameConfigMapper.Map(fixture.Root, ConfigurationTarget.Release);
            Assert.That(release.Definition, Is.Null);
            Assert.That(release.Issues.Single().Code, Is.EqualTo(ConfigurationIssueCode.MissingProductionProfile));
            Assert.That(release.Issues.Single().Field, Is.EqualTo("Profile"));
            var testing = GameConfigMapper.Map(fixture.Root, ConfigurationTarget.Testing);
            Assert.That(testing.Definition, Is.Null);
            Assert.That(testing.Issues.Single().Code, Is.EqualTo(ConfigurationIssueCode.MissingReference));
        }

        [TestCase("_simulation", "Simulation")]
        [TestCase("_time", "Time")]
        [TestCase("_needs", "Needs")]
        [TestCase("_modifiers", "Modifiers")]
        public void RejectsMissingRequiredReferences(string property, string field)
        {
            using var fixture = new ConfigurationFixture();
            ConfigurationFixture.Edit(fixture.Profile, s => s.FindProperty(property).objectReferenceValue = null);

            AssertIssue(fixture, ConfigurationIssueCode.MissingReference, field, "BalanceProfileSO Test Fixture");
        }

        [TestCase("_rulesVersion", "RulesVersion", "")]
        [TestCase("_rulesVersion", "RulesVersion", "v0.1")]
        [TestCase("_contentVersion", "ContentVersion", " 1.2")]
        [TestCase("_balanceVersion", "BalanceVersion", "1.2.3.4")]
        [TestCase("_balanceVersion", "BalanceVersion", "1.999999999999999")]
        public void RejectsInvalidVersions(string property, string field, string value)
        {
            using var fixture = new ConfigurationFixture();
            var source = property == "_balanceVersion" ? (UnityEngine.ScriptableObject)fixture.Profile : fixture.Root;
            ConfigurationFixture.Edit(source, s => s.FindProperty(property).stringValue = value);

            AssertIssue(fixture, ConfigurationIssueCode.InvalidVersion, field);
        }

        [TestCase("")]
        [TestCase("test profile")]
        public void RejectsInvalidProfileIds(string id)
        {
            using var fixture = new ConfigurationFixture();
            ConfigurationFixture.Edit(fixture.Profile, s => s.FindProperty("_id").stringValue = id);

            AssertIssue(fixture, ConfigurationIssueCode.InvalidId, "Id");
        }

        [Test]
        public void RejectsUnknownProfileKind()
        {
            using var fixture = new ConfigurationFixture();
            ConfigurationFixture.Edit(fixture.Profile, s => s.FindProperty("_kind").intValue = 999);

            AssertIssue(fixture, ConfigurationIssueCode.UnknownEnumValue, "Kind");
        }

        [Test]
        public void RejectsIncompleteNeeds()
        {
            using var fixture = new ConfigurationFixture();
            ConfigurationFixture.Edit(fixture.Needs, s => s.FindProperty("_entries").arraySize = 7);

            AssertIssue(fixture, ConfigurationIssueCode.MissingNeed, "Entries");
        }

        [TestCase((int)NeedId.Energy, ConfigurationIssueCode.DuplicateNeed, "Entries[1].Id")]
        [TestCase((int)NeedId.Comfort, ConfigurationIssueCode.ComputedNeed, "Entries[0].Id")]
        [TestCase(999, ConfigurationIssueCode.UnknownEnumValue, "Entries[0].Id")]
        public void RejectsDuplicateComputedAndUnknownNeeds(int id, ConfigurationIssueCode code, string field)
        {
            using var fixture = new ConfigurationFixture();
            fixture.ChangeNeed(0, row => row.FindPropertyRelative("_id").intValue = id);

            AssertIssue(fixture, code, field);
        }

        [TestCase("_maximum", "Maximum", "0", ConfigurationIssueCode.InvalidRange)]
        [TestCase("_initialValue", "InitialValue", "100.001", ConfigurationIssueCode.InitialValueOutOfRange)]
        [TestCase("_initialValue", "InitialValue", "-0.001", ConfigurationIssueCode.InitialValueOutOfRange)]
        [TestCase("_initialValue", "InitialValue", "12.3456", ConfigurationIssueCode.InvalidPrecision)]
        [TestCase("_initialValue", "InitialValue", "12,345", ConfigurationIssueCode.InvalidNumber)]
        [TestCase("_initialValue", "InitialValue", "1e1", ConfigurationIssueCode.InvalidNumber)]
        [TestCase("_initialValue", "InitialValue", "1.2\n", ConfigurationIssueCode.InvalidNumber)]
        [TestCase("_initialValue", "InitialValue", "10000000000", ConfigurationIssueCode.NumericOverflow)]
        [TestCase("_baseRatePerHour", "BaseRatePerHour", "-9999999999999999999999999", ConfigurationIssueCode.NumericOverflow)]
        public void RejectsInvalidNeedNumbers(string property, string field, string value, ConfigurationIssueCode code)
        {
            using var fixture = new ConfigurationFixture();
            fixture.ChangeNeed(0, row => row.FindPropertyRelative(property).stringValue = value);

            AssertIssue(fixture, code, "Entries[0]." + field, "NeedsConfigSO Test Fixture");
        }

        [TestCase("100.001", "Thresholds[0]", ConfigurationIssueCode.ThresholdOutOfRange)]
        [TestCase("5.000", "Thresholds[1]", ConfigurationIssueCode.DuplicateThreshold)]
        public void RejectsOutOfRangeAndDuplicateThresholds(string first, string field, ConfigurationIssueCode code)
        {
            using var fixture = new ConfigurationFixture();
            fixture.SetThresholds(first, "5");

            AssertIssue(fixture, code, "Entries[0]." + field);
        }

        [TestCase("", (int)NeedId.Energy, "1", "Id", ConfigurationIssueCode.InvalidId)]
        [TestCase("test.satiety", (int)NeedId.Energy, "1", "Id", ConfigurationIssueCode.DuplicateId)]
        [TestCase("test.energy", (int)NeedId.Comfort, "1", "Target", ConfigurationIssueCode.ComputedNeed)]
        [TestCase("test.energy", 100, "1", "Target", ConfigurationIssueCode.UnknownEnumValue)]
        [TestCase("test.energy", (int)NeedId.Energy, "-0.000001", "Coefficient", ConfigurationIssueCode.InvalidCoefficient)]
        [TestCase("test.energy", (int)NeedId.Energy, "0.0000001", "Coefficient", ConfigurationIssueCode.InvalidPrecision)]
        [TestCase("test.energy", (int)NeedId.Energy, "1,25", "Coefficient", ConfigurationIssueCode.InvalidNumber)]
        [TestCase("test.energy", (int)NeedId.Energy, "9223372036854.775808", "Coefficient", ConfigurationIssueCode.NumericOverflow)]
        public void RejectsInvalidMultiplierData(string id, int target, string value, string field, ConfigurationIssueCode code)
        {
            using var fixture = new ConfigurationFixture();
            fixture.SetModifier(1, id, (NeedId)target, value);

            AssertIssue(fixture, code, "RateMultipliers[1]." + field);
        }

        [TestCase("_visualDayMinutes", 0L, "VisualDayMinutes", ConfigurationIssueCode.InvalidDuration)]
        [TestCase("_biologicalDayHours", -1L, "BiologicalDayHours", ConfigurationIssueCode.InvalidDuration)]
        [TestCase("_scheduleChangeIntervalDays", long.MaxValue, "ScheduleChangeIntervalDays", ConfigurationIssueCode.NumericOverflow)]
        [TestCase("_maxNightHours", 5L, "MaxNightHours", ConfigurationIssueCode.InvalidNightWindow)]
        [TestCase("_maxNightHours", 25L, "MaxNightHours", ConfigurationIssueCode.InvalidNightWindow)]
        public void RejectsInvalidTimePeriods(string property, long value, string field, ConfigurationIssueCode code)
        {
            using var fixture = new ConfigurationFixture();
            ConfigurationFixture.Edit(fixture.Time, s => s.FindProperty(property).longValue = value);

            AssertIssue(fixture, code, field);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsNonPositiveEventLimit(int value)
        {
            using var fixture = new ConfigurationFixture();
            ConfigurationFixture.Edit(fixture.Simulation, s => s.FindProperty("_maxEventsPerTimestamp").intValue = value);

            AssertIssue(fixture, ConfigurationIssueCode.InvalidEventLimit, "MaxEventsPerTimestamp");
        }

        private static void AssertIssue(ConfigurationFixture fixture, ConfigurationIssueCode code, string field, string asset = null)
        {
            foreach (ConfigurationTarget target in new[] { ConfigurationTarget.Release, ConfigurationTarget.Testing })
            {
                var result = GameConfigMapper.Map(fixture.Root, target);
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Definition, Is.Null);
                var issue = result.Issues.SingleOrDefault(item => item.Code == code && item.Field == field);
                Assert.That(issue, Is.Not.Null, string.Join("\n", result.Issues));
                Assert.That(issue.Severity, Is.EqualTo(ConfigurationSeverity.Error));
                Assert.That(issue.Message, Is.Not.Empty);
                Assert.That(issue.Asset, Is.Not.Empty);
                if (asset != null)
                {
                    Assert.That(issue.Asset, Is.EqualTo(asset));
                }
            }
        }
    }
}
