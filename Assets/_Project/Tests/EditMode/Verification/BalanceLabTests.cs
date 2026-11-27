using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using IdlePet.Authoring;
using IdlePet.Authoring.Mapping;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Time;
using IdlePet.Editor;
using IdlePet.Editor.BalanceLab;
using NUnit.Framework;
using IdlePet.Tests.EditMode.Authoring;
using UnityEditor;

namespace IdlePet.Tests.EditMode.Verification
{
    public sealed class BalanceLabTests
    {
        [TestCase(0, 80, 90, 100, 10)]
        [TestCase(1, 74, 85, 97, 15)]
        [TestCase(4, 56, 70, 88, 30)]
        [TestCase(8, 32, 50, 76, 50)]
        [TestCase(12, 8, 30, 64, 70)]
        [TestCase(24, 0, 0, 28, 100)]
        public void ProductionMatchesIndependentNeedValues(int hours, int satiety, int energy, int hygiene, int boredom)
        {
            var result = LabRunner.Run(Request(LabScenario.Production, LabMode.Compare, hours * 3_600_000L));
            Assert.That(result.Mapping.Succeeded, Is.True);
            Assert.That(result.Difference, Is.Null);
            var state = result.Projections[0].State;
            Assert.That(state.LogicalTimeMilliseconds, Is.EqualTo(hours * 3_600_000L));
            Assert.That(state.ActivePet.Needs[NeedId.Satiety].MilliUnits, Is.EqualTo(satiety * 1000));
            Assert.That(state.ActivePet.Needs[NeedId.Energy].MilliUnits, Is.EqualTo(energy * 1000));
            Assert.That(state.ActivePet.Needs[NeedId.Hygiene].MilliUnits, Is.EqualTo(hygiene * 1000));
            Assert.That(state.ActivePet.Needs[NeedId.Boredom].MilliUnits, Is.EqualTo(boredom * 1000));
            foreach (var id in new[] { NeedId.Health, NeedId.Happiness, NeedId.Discipline, NeedId.Bond })
            {
                Assert.That(state.ActivePet.Needs[id], Is.EqualTo(result.Source.ActivePet.Needs[id]));
            }
            if (hours == 24)
            {
                Report("production-24h", result);
            }
        }

        [Test]
        public void Production365DaysKeepsTheExpectedThresholdsAndReachesTheEntireInterval()
        {
            var result = LabRunner.Run(Request(LabScenario.Production, LabMode.Compare, 31_536_000_000L));
            Assert.That(result.Mapping.Succeeded, Is.True);
            Assert.That(result.Difference, Is.Null);
            Assert.That(result.Projections[0].State.LogicalTimeMilliseconds, Is.EqualTo(31_536_000_000L));
            Assert.That(result.Projections[0].Trace.Select(e => (e.Request.Payload.NeedId.Value, e.Request.Timestamp.Milliseconds)),
                Is.EqualTo(new[] { (NeedId.Satiety, 42_000_000L), (NeedId.Energy, 61_200_000L), (NeedId.Hygiene, 108_000_000L) }));
            Report("production-365d", result);
        }

        [TestCase((int)LabScenario.FastThresholds, BalanceProfileKind.FastTesting, "fast-testing")]
        [TestCase((int)LabScenario.StressLong, BalanceProfileKind.StressTesting, "stress-testing")]
        public void TemporaryProfilesAreMappedAndLeaveNoObjectsOrAssets(int scenario, BalanceProfileKind kind, string name)
        {
            Assert.That(Production().Profile, Is.Not.Null);
            var before = File.ReadAllBytes(GameConfigurationValidation.ProductionAssetPath);
            var assets = AssetDatabase.FindAssets("t:BalanceProfileSO");
            var live = UnityEngine.Resources.FindObjectsOfTypeAll<BalanceProfileSO>().Select(p => p.GetEntityId()).ToArray();
            var result = LabRunner.Run(Request((LabScenario)scenario, LabMode.Compare, 31_536_000_000, ulong.MaxValue, activate: true));

            Assert.That(result.Mapping.Succeeded, Is.True, string.Join("\n", result.Mapping.Issues));
            Assert.That(result.Definition.ProfileKind, Is.EqualTo(kind));
            Assert.That(result.Projections[0].Status, Is.EqualTo(SimulationStatus.Advanced));
            Assert.That(result.Source.ActiveModifierIds.Count, Is.EqualTo(kind == BalanceProfileKind.StressTesting ? 32 : 2));
            Assert.That(result.Difference, Is.Null);
            Assert.That(File.ReadAllBytes(GameConfigurationValidation.ProductionAssetPath), Is.EqualTo(before));
            Assert.That(AssetDatabase.FindAssets("t:BalanceProfileSO"), Is.EquivalentTo(assets));
            Assert.That(UnityEngine.Resources.FindObjectsOfTypeAll<BalanceProfileSO>().Select(p => p.GetEntityId()),
                Is.EquivalentTo(live));
            Report(name, result);
        }

        [TestCase((int)LabMode.AdvanceTo)]
        [TestCase((int)LabMode.PartitionedAdvanceTo)]
        [TestCase((int)LabMode.Forecast)]
        [TestCase((int)LabMode.Compare)]
        public void ModesAreReproducibleAndEquivalentToDirectEngine(int mode)
        {
            var request = Request(LabScenario.Production, (LabMode)mode, 60_000, 0);
            var first = LabRunner.Run(request);
            LabRunner.Run(Request(LabScenario.Production, LabMode.Forecast, 28_800_000, 1));
            var again = LabRunner.Run(request);
            var direct = new SimulationEngine(first.Definition).AdvanceTo(first.Source, request.Target);
            Assert.That(first.Mapping.Succeeded && again.Mapping.Succeeded, Is.True);
            Assert.That(first.Difference, Is.Null);
            foreach (var projection in first.Projections)
            {
                Assert.That(XNode.DeepEquals(LabReport.State("state", projection.State), LabReport.State("state", direct.State)), Is.True);
            }
            Assert.That(LabReport.Difference(first.Projections[0], again.Projections[0]), Is.Null);
            Assert.That(first.Source, Is.Not.SameAs(again.Source));
        }

        [Test]
        public void InputAndExportPreserveFullWidthIntegersIndependentOfCulture()
        {
            var request = LabRequest.Parse(Production(), LabScenario.Production, LabMode.Compare, "18446744073709551615",
                "9223372036854775807", "0", 7, false);
            var result = LabRunner.Run(request);
            var culture = CultureInfo.CurrentCulture;
            XElement original = LabReport.Export(result);
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                Assert.That(XNode.DeepEquals(original, LabReport.Export(result)), Is.True);
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
            Assert.That((string)original.Attribute("worldSeed"), Is.EqualTo("18446744073709551615"));
            Assert.That((string)original.Attribute("targetMs"), Is.EqualTo("9223372036854775807"));
            Assert.That(result.Difference, Is.Null);
            Assert.That(result.Projections.All(p => p.Status == SimulationStatus.NoChange), Is.True);
            Assert.That(TestSeed("bad"), Throws.TypeOf<ArgumentException>());
            Assert.That(TestSeed("18446744073709551616"), Throws.TypeOf<ArgumentException>());
            Assert.That(() => new LabRequest(Production(), LabScenario.Production, LabMode.Compare, 42, long.MaxValue, 1, 7),
                Throws.TypeOf<OverflowException>());
            Assert.That(() => new LabRequest(Production(), LabScenario.Production, LabMode.Compare, 42, 0, 1, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Report("lab-export", result);
        }

        [Test]
        public void MapperFailureIsAnExportableDiagnosticWithNoRun()
        {
            var result = LabRunner.Run(new LabRequest(null, LabScenario.SelectedProfile, LabMode.Compare, 42, 0, 60_000, 7));
            Assert.That(result.Mapping.Succeeded, Is.False);
            Assert.That(result.Mapping.Issues.Select(issue => issue.Code.ToString()), Has.Member("MissingRoot"));
            Assert.That(result.Projections, Is.Empty);
            Report("invalid-configuration", result);
        }

        [Test]
        public void RemappedInitialBoundsAndPeriodsAffectOnlyNewSelectedProfileRuns()
        {
            using var fixture = new ConfigurationFixture(BalanceProfileKind.FastTesting);
            var request = new LabRequest(fixture.Root, LabScenario.SelectedProfile, LabMode.Compare, 42, 0, 36_000_000, 7);
            var original = LabRunner.Run(request);
            Assert.That(original.Mapping.Succeeded && original.Difference == null, Is.True);
            Assert.That(GameConfigMapper.Map(fixture.Root, ConfigurationTarget.Release).Succeeded, Is.False);
            fixture.ChangeNeed(0, need =>
            {
                need.FindPropertyRelative("_minimum").stringValue = "10";
                need.FindPropertyRelative("_maximum").stringValue = "20";
                need.FindPropertyRelative("_initialValue").stringValue = "14.125";
            });
            ConfigurationFixture.Edit(fixture.Time, time =>
            {
                time.FindProperty("_visualDayMinutes").longValue = 5;
                time.FindProperty("_biologicalDayHours").longValue = 12;
            });
            ConfigurationFixture.Edit(fixture.Root, root => root.FindProperty("_rulesVersion").stringValue = "1.3");

            var updated = LabRunner.Run(request);
            Assert.That(updated.Mapping.Succeeded && updated.Difference == null, Is.True);
            Assert.That(updated.Source.ActivePet.Needs[NeedId.Satiety].MilliUnits, Is.EqualTo(14_125));
            Assert.That(updated.Projections[0].State.ActivePet.Needs[NeedId.Satiety].MilliUnits, Is.EqualTo(10_000));
            Assert.That(updated.Definition.Needs.Entries[NeedId.Satiety].Maximum.MilliUnits, Is.EqualTo(20_000));
            var world = new WorldClock(updated.Definition.Time, default).Project(request.Target);
            var circadian = new CircadianClock(updated.Definition.Time, default).Project(request.Target);
            Assert.That(world.CycleIndex, Is.EqualTo(120));
            Assert.That(world.ElapsedInCycleMilliseconds, Is.Zero);
            Assert.That(circadian.CycleIndex, Is.Zero);
            Assert.That(circadian.ElapsedInCycleMilliseconds, Is.EqualTo(36_000_000));
            Assert.That(updated.Definition.Needs.RulesVersion, Is.EqualTo("1.3"));
            Assert.That(original.Definition.Needs.RulesVersion, Is.EqualTo("1.2"));
            Assert.That(original.Source.ActivePet.Needs[NeedId.Satiety].MilliUnits, Is.EqualTo(12_345));
            Assert.That(original.Projections[0].State.ActivePet.Needs[NeedId.Satiety].MilliUnits, Is.Zero);
        }

        private static TestDelegate TestSeed(string seed) =>
            () => LabRequest.Parse(Production(), LabScenario.Production, LabMode.Compare, seed, "0", "0", 7, false);

        private static LabRequest Request(LabScenario scenario, LabMode mode, long duration, ulong seed = 42, bool activate = false)
        {
            return new LabRequest(Production(), scenario, mode, seed, 0, duration, 31, activate);
        }

        private static GameConfigSO Production() => AssetDatabase.LoadAssetAtPath<GameConfigSO>(GameConfigurationValidation.ProductionAssetPath);

        private static void Report(string id, LabResult result) => TestContext.Out.WriteLine("IDLEPET_LAB " +
            new XElement("scenario", new XAttribute("id", id), LabReport.Export(result)).ToString(SaveOptions.DisableFormatting));
    }
}
