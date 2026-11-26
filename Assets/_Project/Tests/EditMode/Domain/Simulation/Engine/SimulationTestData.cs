using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using IdlePet.Domain;
using IdlePet.Domain.Common.Identifiers;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Commands;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Time;
using NUnit.Framework;

namespace IdlePet.Tests.EditMode.Domain.Simulation.Engine
{
    internal static class SimulationTestData
    {
        internal static readonly PetId Pet = new PetId("pet.test");

        internal static LogicalTime At(long time) => new LogicalTime(time);

        internal static NeedValue Exact(long value) => NeedValue.FromExactNumerator(value);

        internal static GameConfiguration Config(int limit = 32, Func<NeedId, long> rates = null,
            Func<NeedId, long[]> thresholds = null, IEnumerable<RateModifierDefinition> modifiers = null)
        {
            var needs = new NeedsDefinition("rules", "balance", "content",
                NeedTestData.CreateEntries().Select(entry => new NeedDefinition(entry.Id, Exact(-1000), Exact(1000), Exact(100),
                    new NeedRate(rates?.Invoke(entry.Id) ?? 0),
                    (thresholds?.Invoke(entry.Id) ?? Array.Empty<long>()).Select(Exact))));
            return new GameConfiguration(BalanceProfileKind.FastTesting, "test.engine", new SimulationDefinition(limit),
                new TimeDefinition(100, 100, 1, 1, 1), needs, modifiers ?? Array.Empty<RateModifierDefinition>());
        }

        internal static RateModifierDefinition Scale(string id, long millionths) =>
            new RateModifierDefinition(id, NeedId.Satiety, new NeedCoefficient(millionths));

        internal static PetState Living(GameConfiguration config) => new PetState(Pet, NeedsState.Create(config.Needs));

        internal static SimulationEngine CreateEngine(GameConfiguration config,
            params (string Id, Action<SimulationEventContext, ScheduledEvent> Handle)[] rules) =>
            new SimulationEngine(config, rules.Select(rule => KeyValuePair.Create(rule.Id, rule.Handle)));

        internal static EventRequest Event(long time, string type = "mark", int priority = 10, string source = "tech",
            string text = null, PetId? pet = null) =>
            new EventRequest(At(time), priority, source, type, pet.HasValue ? EventScope.PetLife : EventScope.World,
                pet, new EventPayload(text));

        internal static GameState Restore(GameState state) => GameState.Restore(state.LogicalTimeMilliseconds, state.ActivePet,
            state.Rng, new EventQueueSnapshot(state.EventQueue.Events.Reverse(), state.EventQueue.NextSequence),
            state.ActiveModifierIds.Reverse(), state.ProcessedEventsAtCursor);

        internal static void SameState(GameState expected, GameState actual)
        {
            var left = State("state", expected);
            var right = State("state", actual);
            Assert.That(XNode.DeepEquals(left, right), Is.True, $"Expected {left}, actual {right}");
        }

        internal static void SameTrace(IEnumerable<ScheduledEvent> expected, IEnumerable<ScheduledEvent> actual)
        {
            var left = new XElement("trace", expected.Select(Record));
            var right = new XElement("trace", actual.Select(Record));
            Assert.That(XNode.DeepEquals(left, right), Is.True, $"Expected {left}, actual {right}");
        }

        internal static void Evidence(string name, GameState before, SimulationResult after, SimulationResult comparison = null)
        {
            WriteEvidence(name, State("initial", before), after, comparison);
        }

        internal static void InitializationEvidence(string name, ulong seed, PetState pet, IEnumerable<EventRequest> requests,
            SimulationResult after, SimulationResult comparison = null)
        {
            var input = new XElement("initialization-input", new XAttribute("timeMs", after.Origin.Milliseconds),
                new XAttribute("seedHex", seed.ToString("x16")), new XAttribute("pet", pet?.Id.Value ?? ""),
                Needs(pet), new XElement("requests", requests.Select(request => Record(request))));
            WriteEvidence(name, input, after, comparison);
        }

        internal static void AssertInitializationFailed(SimulationResult result)
        {
            Assert.That(result.Status, Is.EqualTo(SimulationStatus.InitializationFailed));
            Assert.That(result.State, Is.Null);
            Assert.That(result.Reached, Is.Null);
            Assert.That(result.Trace, Is.Empty);
            Assert.That(result.CommittedTransactions, Is.Zero);
            Assert.That(result.ThresholdBoundaries, Is.Zero);
        }

        private static void WriteEvidence(string name, XElement input, SimulationResult after, SimulationResult comparison)
        {
            var report = new XElement("scenario", new XAttribute("name", name), input, Result("result", after));
            if (comparison != null)
            {
                report.Add(Result("comparison", comparison));
            }

            TestContext.Out.WriteLine("IDLEPET_REVIEW " + report.ToString(SaveOptions.DisableFormatting));
        }

        private static XElement Result(string label, SimulationResult result) => new XElement(label,
            new XAttribute("status", result.Status), new XAttribute("targetMs", result.Target.Milliseconds),
            result.Reached.HasValue ? new XAttribute("reachedMs", result.Reached.Value.Milliseconds) : null,
            new XAttribute("commits", result.CommittedTransactions), new XAttribute("thresholdBoundaries", result.ThresholdBoundaries),
            result.State == null ? null : State("state", result.State), new XElement("trace", result.Trace.Select(Record)),
            result.Diagnostic == null ? null : new XElement("diagnostic", new XAttribute("code", result.Diagnostic.Code),
                new XAttribute("timeMs", result.Diagnostic.Timestamp.Milliseconds),
                result.Diagnostic.Limit.HasValue ? new XAttribute("limit", result.Diagnostic.Limit.Value) : null,
                result.Diagnostic.Event == null ? null : Record(result.Diagnostic.Event)));

        private static XElement State(string label, GameState state) => new XElement(label,
            new XAttribute("timeMs", state.LogicalTimeMilliseconds), new XAttribute("nextSequence", state.EventQueue.NextSequence),
            new XAttribute("eventsAtCursor", state.ProcessedEventsAtCursor),
            new XAttribute("pet", state.ActivePet?.Id.Value ?? ""),
            new XAttribute("terminatedAt", state.ActivePet?.TerminatedAt?.Milliseconds.ToString() ?? ""),
            Needs(state.ActivePet),
            new XElement("modifiers", state.ActiveModifierIds.Select(id => new XElement("modifier", new XAttribute("id", id)))),
            new XElement("rng", new XAttribute("seedHex", state.Rng.WorldSeed.ToString("x16")),
                new XAttribute("version", state.Rng.RngAlgorithmVersion),
                state.Rng.Streams.Select(stream => new XElement("stream", new XAttribute("id", stream.StreamId),
                    new XAttribute("selectorHex", stream.SequenceId.ToString("x16")), new XAttribute("stateHex", stream.State.ToString("x16"))))),
            new XElement("queue", state.EventQueue.Events.Select(Record)));

        private static XElement Needs(PetState pet) => pet == null ? null : new XElement("needs",
            pet.Needs.Values.Select(pair => new XElement("need", new XAttribute("id", pair.Key),
                new XAttribute("milliUnits", pair.Value.MilliUnits), new XAttribute("remainder", pair.Value.Remainder.Numerator),
                new XAttribute("exact", pair.Value.ExactNumerator))));

        private static XElement Record(ScheduledEvent entry) => Record(entry.Request, entry.Sequence);

        private static XElement Record(EventRequest request, long? sequence = null) =>
            new XElement("event", new XAttribute("timestampMs", request.Timestamp.Milliseconds),
                new XAttribute("priority", request.Priority), new XAttribute("source", request.SourceId),
                sequence.HasValue ? new XAttribute("sequence", sequence.Value) : null, new XAttribute("type", request.TypeId),
                new XAttribute("scope", request.Scope), new XAttribute("pet", request.PetId?.Value ?? ""),
                new XAttribute("text", request.Payload.Text ?? ""),
                new XAttribute("need", request.Payload.NeedId?.ToString() ?? ""),
                new XAttribute("thresholdExact", request.Payload.Threshold?.ExactNumerator.ToString() ?? ""),
                new XAttribute("direction", request.Payload.Direction?.ToString() ?? ""));

        internal sealed class Command : ISimulationCommand
        {
            private readonly Func<GameState, string> _validate;
            private readonly Action<SimulationEventContext> _apply;

            internal Command(Func<GameState, string> validate, Action<SimulationEventContext> apply)
            {
                _validate = validate;
                _apply = apply;
            }

            public string Validate(GameState state) => _validate(state);

            public void Apply(SimulationEventContext context) => _apply(context);
        }
    }
}
