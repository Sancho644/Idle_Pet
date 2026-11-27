using System;
using System.Linq;
using System.Xml.Linq;
using IdlePet.Domain;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Random;

namespace IdlePet.Editor.BalanceLab
{
    internal static class LabReport
    {
        internal static XElement Export(LabResult result)
        {
            var request = result.Request;
            return new XElement("balanceSimulationLab", new XAttribute("formatVersion", 1),
                new XAttribute("scenarioId", request.Scenario), new XAttribute("mode", request.Mode),
                new XAttribute("worldSeed", request.Seed), new XAttribute("originMs", request.Origin.Milliseconds),
                new XAttribute("targetMs", request.Target.Milliseconds), new XAttribute("segments", request.Segments),
                new XAttribute("activateModifiers", request.ActivateModifiers),
                new XAttribute("status", result.Mapping.Succeeded ? "Mapped" : "InvalidConfiguration"),
                new XElement("configurationIssues", result.Mapping.Issues.Select(issue => new XElement("issue",
                    new XAttribute("code", issue.Code), new XAttribute("severity", issue.Severity),
                    new XAttribute("asset", issue.Asset ?? ""), new XAttribute("field", issue.Field ?? ""), issue.Message))),
                result.Definition == null ? null : Definitions(result.Definition),
                result.Source == null ? null : State("initial", result.Source),
                result.Projections.Select((projection, index) => new XElement("projection",
                    new XAttribute("strategy", result.Strategy(index)),
                    new XAttribute("elapsedMs", projection.ElapsedMs), new XAttribute("transactions", projection.Transactions),
                    new XAttribute("thresholdBoundaries", projection.ThresholdBoundaries), Authoritative(projection))),
                request.Mode == LabMode.Compare && result.Source != null ?
                    new XElement("comparison", new XAttribute("equal", result.Difference == null), result.Difference) : null);
        }

        internal static XElement Definitions(GameConfiguration definition)
        {
            var time = definition.Time;
            return new XElement("definitions", new XAttribute("profileId", definition.ProfileId),
                new XAttribute("kind", definition.ProfileKind), new XAttribute("rules", definition.Needs.RulesVersion),
                new XAttribute("balance", definition.Needs.BalanceVersion), new XAttribute("content", definition.Needs.ContentVersion),
                new XAttribute("rng", Pcg32.AlgorithmVersion), new XAttribute("maxEvents", definition.Simulation.MaxEventsPerTimestamp),
                new XElement("time", new XAttribute("visualDayMs", time.VisualDayMilliseconds),
                    new XAttribute("biologicalDayMs", time.BiologicalDayMilliseconds), new XAttribute("minNightMs", time.MinNightMilliseconds),
                    new XAttribute("maxNightMs", time.MaxNightMilliseconds), new XAttribute("changeIntervalMs", time.ScheduleChangeIntervalMilliseconds)),
                new XElement("needs", definition.Needs.Entries.Values.Select(need => new XElement("need",
                    new XAttribute("id", need.Id), new XAttribute("rate", need.BaseRate.MilliUnitsPerHour),
                    Number("min", need.Minimum), Number("max", need.Maximum), Number("initial", need.InitialValue),
                    need.Thresholds.Select(value => Number("threshold", value))))),
                new XElement("modifiers", definition.RateModifiers.Values.Select(modifier => new XElement("modifier",
                    new XAttribute("id", modifier.Id), new XAttribute("need", modifier.Target),
                    new XAttribute("coefficient", modifier.Coefficient.Millionths)))));
        }

        internal static XElement State(string name, GameState state) => new XElement(name,
            new XAttribute("present", state != null), state == null ? null : new object[]
            {
                new XAttribute("cursor", state.LogicalTimeMilliseconds), new XAttribute("nextSequence", state.EventQueue.NextSequence),
                new XAttribute("processedAtCursor", state.ProcessedEventsAtCursor),
                state.ActivePet == null ? null : new XElement("pet", new XAttribute("id", state.ActivePet.Id.Value),
                    state.ActivePet.TerminatedAt.HasValue ? new XAttribute("terminatedAt", state.ActivePet.TerminatedAt.Value.Milliseconds) : null,
                    state.ActivePet.Needs.Values.Select(need => new XElement("need", new XAttribute("id", need.Key), Number("value", need.Value)))),
                new XElement("rng", new XAttribute("version", state.Rng.RngAlgorithmVersion), new XAttribute("worldSeed", state.Rng.WorldSeed),
                    state.Rng.Streams.Select(stream => new XElement("stream", new XAttribute("id", stream.StreamId),
                        new XAttribute("selector", stream.SequenceId), new XAttribute("state", stream.State)))),
                new XElement("modifiers", state.ActiveModifierIds.Select(id => new XElement("id", id))),
                new XElement("queue", state.EventQueue.Events.Select(Event))
            });

        internal static XElement Authoritative(LabProjection output) => new XElement("outcome",
            new XAttribute("status", output.Status), new XAttribute("reached", output.State != null), State("state", output.State),
            new XElement("trace", output.Trace.Select(Event)), Diagnostic(output.Diagnostic));

        internal static XElement Diagnostic(SimulationDiagnostic diagnostic) => diagnostic == null ? null : new XElement("diagnostic",
            new XAttribute("code", diagnostic.Code), new XAttribute("timestamp", diagnostic.Timestamp.Milliseconds),
            new XAttribute("inputStateFailure", diagnostic.IsInputStateFailure),
            diagnostic.Limit.HasValue ? new XAttribute("limit", diagnostic.Limit.Value) : null,
            diagnostic.Event == null ? null : Event(diagnostic.Event), new XElement("reason", diagnostic.Reason));

        internal static string Difference(LabProjection expected, LabProjection actual) =>
            Difference(Authoritative(expected), Authoritative(actual), "");

        internal static string Difference(XElement expected, XElement actual, string path)
        {
            if (XNode.DeepEquals(expected, actual))
            {
                return null;
            }
            path += "/" + expected.Name;
            if (expected.Name != actual.Name)
            {
                return $"{path}: expected element {expected.Name}, got {actual.Name}";
            }
            foreach (var name in expected.Attributes().Select(a => a.Name).Union(actual.Attributes().Select(a => a.Name)))
            {
                string a = (string)expected.Attribute(name);
                string b = (string)actual.Attribute(name);
                if (!String.Equals(a, b, StringComparison.Ordinal))
                {
                    return $"{path}/@{name}: expected {a ?? "(absent)"}, got {b ?? "(absent)"}";
                }
            }
            var left = expected.Elements().ToArray();
            var right = actual.Elements().ToArray();
            for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
            {
                string diff = Difference(left[i], right[i], path + $"[{i}]");
                if (diff != null)
                {
                    return diff;
                }
            }
            if (left.Length != right.Length)
            {
                return $"{path}: expected {left.Length} entries, got {right.Length}";
            }
            return $"{path}: expected {expected.Value}, got {actual.Value}";
        }

        private static XElement Number(string name, NeedValue value) => new XElement(name,
            new XAttribute("milliUnits", value.MilliUnits), new XAttribute("remainder", value.Remainder.Numerator),
            new XAttribute("exactNumerator", value.ExactNumerator));

        private static XElement Event(ScheduledEvent e) => new XElement("event",
            new XAttribute("time", e.Request.Timestamp.Milliseconds), new XAttribute("priority", e.Request.Priority),
            new XAttribute("source", e.Request.SourceId), new XAttribute("type", e.Request.TypeId),
            new XAttribute("scope", e.Request.Scope), new XAttribute("sequence", e.Sequence),
            new XAttribute("pet", e.Request.PetId?.Value ?? ""),
            e.Request.Payload.Text is string text ? new XAttribute("text", text) : null,
            new XAttribute("need", e.Request.Payload.NeedId?.ToString() ?? ""),
            new XAttribute("threshold", e.Request.Payload.Threshold?.ExactNumerator.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? ""),
            new XAttribute("direction", e.Request.Payload.Direction?.ToString() ?? ""));
    }
}
