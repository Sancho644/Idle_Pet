using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using IdlePet.Application.Forecast;
using IdlePet.Domain;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Rules;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;

namespace IdlePet.Tests.EditMode.Application.Forecast
{
    internal static class ForecastTestData
    {
        internal static SimulationEngine Engine(GameConfiguration config, params (string, SimulationRuleEffect[])[] rules) =>
            new SimulationEngine(config, new SimulationRuleSet(rules));

        internal static (string, SimulationRuleEffect[]) Rule(string id, params SimulationRuleEffect[] effects) => (id, effects);

        internal static void Matches(SimulationResult actual, ForecastResult forecast)
        {
            Assert.That(forecast.Status, Is.EqualTo(actual.Status));
            Assert.That(forecast.Origin, Is.EqualTo(actual.Origin));
            Assert.That(forecast.Target, Is.EqualTo(actual.Target));
            Assert.That(forecast.Reached, Is.EqualTo(actual.Reached));
            Assert.That(forecast.CommittedTransactions, Is.EqualTo(actual.CommittedTransactions));
            Assert.That(forecast.ThresholdBoundaries, Is.EqualTo(actual.ThresholdBoundaries));
            SameState(actual.State, forecast.State);
            SameTrace(actual.Trace, forecast.Trace);
            Assert.That(XNode.DeepEquals(Diagnostic(actual.Diagnostic), Diagnostic(forecast.Diagnostic)), Is.True);
        }

        internal static void Report(string name, params XElement[] evidence)
        {
            TestContext.Out.WriteLine("IDLEPET_REVIEW " +
                new XElement("scenario", new XAttribute("name", name), evidence).ToString(SaveOptions.DisableFormatting));
        }

        internal static XElement ForecastRecord(string label, ForecastResult forecast) => new XElement(label,
            new XAttribute("status", forecast.Status), new XAttribute("originMs", forecast.Origin.Milliseconds),
            new XAttribute("targetMs", forecast.Target.Milliseconds),
            forecast.Reached.HasValue ? new XAttribute("reachedMs", forecast.Reached.Value.Milliseconds) : null,
            new XAttribute("commits", forecast.CommittedTransactions),
            new XAttribute("thresholdBoundaries", forecast.ThresholdBoundaries),
            forecast.State == null ? null : State("state", forecast.State),
            Trace("trace", forecast.Trace), Diagnostic(forecast.Diagnostic));

        internal static XElement Trace(string label, IEnumerable<ScheduledEvent> events) => new XElement(label, events.Select(Record));

        private static XElement Diagnostic(SimulationDiagnostic diagnostic) => diagnostic == null ? null : new XElement("diagnostic",
            new XAttribute("code", diagnostic.Code), new XAttribute("timeMs", diagnostic.Timestamp.Milliseconds),
            new XAttribute("inputStateFailure", diagnostic.IsInputStateFailure),
            diagnostic.Limit.HasValue ? new XAttribute("limit", diagnostic.Limit.Value) : null,
            diagnostic.Event == null ? null : Record(diagnostic.Event), new XElement("reason", diagnostic.Reason));
    }
}
