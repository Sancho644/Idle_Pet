using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using IdlePet.Authoring;
using IdlePet.Domain;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Time;
using IdlePet.Editor;
using IdlePet.Editor.BalanceLab;
using NUnit.Framework;
using UnityEditor;
using static IdlePet.Tests.EditMode.Application.Forecast.ForecastTestData;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;

namespace IdlePet.Tests.EditMode.Verification
{
    public sealed class LabReportPayloadTests
    {
        private const long EventTime = 10;
        private const string ExactText = "  Текст <&> \"quoted\"\t\n";

        [TestCase(null)]
        [TestCase("")]
        public void StatesWithTheSameOptionalPayloadTextAreStructurallyEqual(string text)
        {
            var first = LabReport.State("state", World(text));
            var second = LabReport.State("state", World(text));

            Assert.That(LabReport.Difference(first, second, ""), Is.Null);
        }

        [Test]
        public void MissingAndEmptyTextDifferInTheFutureQueue()
        {
            var missing = LabReport.State("state", World(null));
            var empty = LabReport.State("state", World(string.Empty));

            var difference = LabReport.Difference(missing, empty, "");

            Assert.That(difference, Does.StartWith("/state"));
            Assert.That(difference, Does.Contain("/queue").And.Contain("/event/@text: expected (absent)"));
            var reverse = LabReport.Difference(empty, missing, "");
            Assert.That(reverse, Does.Contain("/event/@text").And.EndWith("got (absent)"));
        }

        [Test]
        public void MissingAndEmptyTextDifferInTheTraceAfterTheQueueIsConsumed()
        {
            var engine = Engine(Config(), Rule("mark"));
            var missing = LabRunner.Project(engine, World(null), new[] { EventTime });
            var empty = LabRunner.Project(engine, World(string.Empty), new[] { EventTime });

            Assert.That(missing.Status, Is.EqualTo(SimulationStatus.Advanced));
            Assert.That(empty.Status, Is.EqualTo(SimulationStatus.Advanced));
            Assert.That(missing.Trace.Single().Request.Payload.Text, Is.Null);
            Assert.That(empty.Trace.Single().Request.Payload.Text, Is.Empty);
            Assert.That(LabReport.Difference(LabReport.State("state", missing.State),
                LabReport.State("state", empty.State), ""), Is.Null);
            var difference = LabReport.Difference(missing, empty);
            Assert.That(difference, Does.Contain("/trace").And.Contain("/event/@text: expected (absent)"));
        }

        [TestCase(null, "payload-null")]
        [TestCase("", "payload-empty")]
        [TestCase(ExactText, "payload-nonempty")]
        public void ExportRoundTripPreservesOptionalTextInTheQueueAndTrace(string text, string scenarioId)
        {
            var root = Production();
            var mapping = LabProfiles.Map(root);
            Assert.That(mapping.Succeeded, Is.True);
            var request = new LabRequest(root, LabScenario.Production, LabMode.AdvanceTo, 42, 0, EventTime, 1);
            var source = World(text);
            var engine = Engine(mapping.Definition, Rule("mark"));
            var projection = LabRunner.Project(engine, source, new[] { EventTime });
            var report = new LabResult(request, mapping, source, new[] { projection });

            var exported = RoundTrip(report);

            var queued = exported.Element("initial").Element("queue").Elements("event").Single();
            var processed = exported.Element("projection").Element("outcome").Element("trace").Elements("event").Single();
            foreach (var entry in new[] { queued, processed })
            {
                Assert.That(entry.Attribute("text") != null, Is.EqualTo(text != null));
                Assert.That((string)entry.Attribute("text"), Is.EqualTo(text));
            }
            TestContext.Out.WriteLine("IDLEPET_LAB " + new XElement("scenario", new XAttribute("id", scenarioId), exported)
                .ToString(SaveOptions.DisableFormatting));
        }

        [Test]
        public void ExportPreservesProductionThresholdNumbersNeedsAndDirectionsWithoutText()
        {
            var request = new LabRequest(Production(), LabScenario.Production, LabMode.Compare, 42, 0, 31_536_000_000L, 31);
            var report = LabRunner.Run(request);

            var exported = RoundTrip(report);

            Assert.That((bool?)exported.Element("comparison").Attribute("equal"), Is.True);
            var projections = exported.Elements("projection").ToArray();
            Assert.That(projections, Has.Length.EqualTo(4));
            foreach (var projection in projections)
            {
                var events = projection.Element("outcome").Element("trace").Elements("event").ToArray();
                Assert.That(events.Select(entry => (string)entry.Attribute("type")),
                    Is.EqualTo(Enumerable.Repeat(SimulationEngine.ThresholdEventTypeId, 3)));
                Assert.That(events.Select(entry => (string)entry.Attribute("need")),
                    Is.EqualTo(new[] { "Satiety", "Energy", "Hygiene" }));
                Assert.That(events.Select(entry => (long)entry.Attribute("threshold")),
                    Is.EqualTo(new[] { 36_000_000_000L, 18_000_000_000L, 36_000_000_000L }));
                Assert.That(events.Select(entry => (long)entry.Attribute("time")),
                    Is.EqualTo(new[] { 42_000_000L, 61_200_000L, 108_000_000L }));
                Assert.That(events.Select(entry => (string)entry.Attribute("direction")), Is.All.EqualTo("Falling"));
                Assert.That(events.Select(entry => entry.Attribute("text")), Is.All.Null);
            }
        }

        private static GameState World(string text)
        {
            var initial = new GameState(0, null, 42);
            var request = new EventRequest(new LogicalTime(EventTime), 10, "review.fixture", "mark",
                EventScope.World, null, new EventPayload(text));
            var queue = new EventQueueSnapshot(new[] { new ScheduledEvent(request, 0) }, 1);
            return GameState.Restore(0, null, initial.Rng, queue, Array.Empty<string>(), 0);
        }

        private static XElement RoundTrip(LabResult result)
        {
            using var stream = new MemoryStream();
            LabReport.Export(result).Save(stream);
            stream.Position = 0;
            return XElement.Load(stream);
        }

        private static GameConfigSO Production() =>
            AssetDatabase.LoadAssetAtPath<GameConfigSO>(GameConfigurationValidation.ProductionAssetPath);
    }
}
