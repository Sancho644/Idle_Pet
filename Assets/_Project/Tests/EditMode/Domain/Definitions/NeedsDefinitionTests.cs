using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet.Needs;
using NUnit.Framework;

namespace IdlePet.Tests.EditMode.Domain.Definitions
{
    public sealed class NeedsDefinitionTests
    {
        [Test]
        public void KeepsValidatedEntriesAndIndependentVersions()
        {
            var definition = NeedTestData.CreateDefinition();
            NeedDefinition entry = definition.Entries[NeedId.Satiety];

            Assert.That(definition.RulesVersion, Is.EqualTo("test-rules"));
            Assert.That(definition.BalanceVersion, Is.EqualTo("test-balance"));
            Assert.That(definition.ContentVersion, Is.EqualTo("test-content"));
            Assert.That(definition.Entries, Has.Count.EqualTo(8));
            Assert.That(definition.Entries.ContainsKey(NeedId.Comfort), Is.False);
            Assert.That(entry.InitialValue, Is.EqualTo(NeedTestData.Value(1_700)));
            Assert.That(entry.BaseRate.MilliUnitsPerHour, Is.EqualTo(-230));
            Assert.That(entry.Thresholds, Is.EqualTo(new[] { NeedTestData.Value(600), NeedTestData.Value(10_000) }));
        }

        [TestCase(0)]
        [TestCase(7)]
        [TestCase(99)]
        public void RejectsIdsThatCannotIdentifyStoredNeeds(int id)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NeedDefinition(
                (NeedId)id, NeedTestData.Value(0), NeedTestData.Value(9), NeedTestData.Value(2),
                new NeedRate(0), Array.Empty<NeedValue>()));
        }

        [TestCase(10L, 10L)]
        [TestCase(11L, 10L)]
        public void RejectsEmptyOrReversedRanges(long minimum, long maximum)
        {
            Assert.Throws<ArgumentException>(() => new NeedDefinition(
                NeedId.Satiety, NeedTestData.Value(minimum), NeedTestData.Value(maximum), NeedTestData.Value(10),
                new NeedRate(0), Array.Empty<NeedValue>()));
        }

        [TestCase(199L, 3_599_999)]
        [TestCase(12_000L, 1)]
        public void RejectsInitialValuesOutsideExactRange(long milliUnits, int remainder)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSatiety(
                NeedTestData.Value(milliUnits, remainder), Array.Empty<NeedValue>()));
        }

        [TestCase(199L, 3_599_999)]
        [TestCase(12_000L, 1)]
        public void RejectsThresholdsOutsideExactRange(long milliUnits, int remainder)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSatiety(
                NeedTestData.Value(1_700), new[] { NeedTestData.Value(milliUnits, remainder) }));
        }

        [Test]
        public void RejectsMissingAndDuplicateThresholdData()
        {
            var threshold = NeedTestData.Value(500, 1);

            Assert.Throws<ArgumentNullException>(() => CreateSatiety(NeedTestData.Value(1_700), null));
            Assert.Throws<ArgumentException>(() => CreateSatiety(NeedTestData.Value(1_700), new[] { threshold, threshold }));
        }

        [Test]
        public void ReadsFullNumeratorAtBothRangesAndThresholds()
        {
            var lower = NeedTestData.Value(200);
            var upper = NeedTestData.Value(12_000);
            var threshold = NeedTestData.Value(600, 1);
            var initialValue = NeedTestData.Value(600, 2);
            var definition = CreateSatiety(initialValue, new[] { threshold, upper, lower });

            Assert.That(definition.Contains(lower), Is.True);
            Assert.That(definition.Contains(upper), Is.True);
            Assert.That(definition.Contains(NeedTestData.Value(12_000, 1)), Is.False);
            Assert.That(definition.Thresholds, Is.EqualTo(new[] { lower, threshold, upper }));
            Assert.That(definition.InitialValue.CompareTo(definition.Thresholds[1]), Is.GreaterThan(0));
        }

        [Test]
        public void TakesOwnershipOfDefinitionAndThresholdCollections()
        {
            var thresholds = new List<NeedValue> { NeedTestData.Value(1_000) };
            var satiety = CreateSatiety(NeedTestData.Value(1_700), thresholds);
            var entries = NeedTestData.CreateEntries();
            entries[0] = satiety;
            var definition = new NeedsDefinition("rules", "balance", "content", entries);

            thresholds[0] = NeedTestData.Value(1_100);
            entries[0] = null;

            Assert.That(definition.Entries[NeedId.Satiety], Is.SameAs(satiety));
            Assert.That(satiety.Thresholds[0], Is.EqualTo(NeedTestData.Value(1_000)));
            Assert.Throws<NotSupportedException>(() => ((IList<NeedValue>)satiety.Thresholds).Clear());
            Assert.Throws<NotSupportedException>(() => ((IDictionary<NeedId, NeedDefinition>)definition.Entries).Clear());
        }

        [Test]
        public void RejectsMissingAndDuplicateDefinitionEntries()
        {
            var entries = NeedTestData.CreateEntries();

            Assert.Throws<ArgumentNullException>(() => new NeedsDefinition("r", "b", "c", null));
            Assert.Throws<ArgumentException>(() => new NeedsDefinition("r", "b", "c", entries.Skip(1)));
            Assert.Throws<ArgumentException>(() => new NeedsDefinition("r", "b", "c", entries.Append(entries[0])));

            entries[0] = null;
            Assert.Throws<ArgumentException>(() => new NeedsDefinition("r", "b", "c", entries));
        }

        [TestCase("", "b", "c")]
        [TestCase("r", null, "c")]
        [TestCase("r", "b", " ")]
        public void RequiresEveryDefinitionVersion(string rules, string balance, string content)
        {
            Assert.Throws<ArgumentException>(() => new NeedsDefinition(rules, balance, content, NeedTestData.CreateEntries()));
        }

        private static NeedDefinition CreateSatiety(NeedValue initialValue, IEnumerable<NeedValue> thresholds)
        {
            return new NeedDefinition(
                NeedId.Satiety, NeedTestData.Value(200), NeedTestData.Value(12_000), initialValue, new NeedRate(0), thresholds);
        }
    }
}
