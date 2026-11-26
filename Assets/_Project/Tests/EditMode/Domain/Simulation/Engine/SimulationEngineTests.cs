using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Domain;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Random;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Domain.Simulation.Engine.SimulationTestData;

namespace IdlePet.Tests.EditMode.Domain.Simulation.Engine
{
    public sealed class SimulationEngineTests
    {
        [Test]
        public void LaterCreatedSameTimeEventCanTakePriorityBeforeAlreadyReadyWork()
        {
            var engine = CreateEngine(Config(),
                ("A", (context, _) =>
                {
                    context.Schedule(Event(context.Time.Milliseconds, "C", 5));
                    context.Schedule(Event(context.Time.Milliseconds, "mark", 30, "same", "first"));
                    context.Schedule(Event(context.Time.Milliseconds, "mark", 30, "same", "second"));
                }),
                ("B", (_, _) => { }), ("C", (_, _) => { }), ("mark", (_, _) => { }));
            var initial = engine.Initialize(At(0), null, 42, new[] { Event(10, "A", 10), Event(10, "B", 20) }).State;

            var result = engine.AdvanceTo(initial, At(10));

            Assert.That(result.Status, Is.EqualTo(SimulationStatus.Advanced));
            Assert.That(result.Trace.Select(e => e.Request.TypeId), Is.EqualTo(new[] { "A", "C", "B", "mark", "mark" }));
            Assert.That(result.Trace.Select(e => e.Sequence), Is.EqualTo(new long[] { 0, 2, 1, 3, 4 }));
            Assert.That(result.Trace.Skip(3).Select(e => e.Request.Payload.Text), Is.EqualTo(new[] { "first", "second" }));
            Assert.That(result.State.ProcessedEventsAtCursor, Is.EqualTo(5));
            Assert.That(result.State.EventQueue.Events, Is.Empty);
            Evidence("causal-order", initial, result);
        }

        [Test]
        public void InitializationHandlesTheLeftBoundaryOnceAndAdvanceIncludesOnlyTheRightBoundary()
        {
            var engine = CreateEngine(Config(), ("mark", (context, _) => context.NextUInt32(RandomStreamId.Birth)));
            var initialization = engine.Initialize(At(5), null, 42, new[] { Event(5), Event(10), Event(11) });
            Assert.That(initialization.Status, Is.EqualTo(SimulationStatus.Initialized));
            Assert.That(initialization.Trace.Select(e => e.Request.Timestamp.Milliseconds), Is.EqualTo(new[] { 5L }));
            var initial = Restore(initialization.State);
            var equal = engine.AdvanceTo(initial, At(5));
            var past = engine.AdvanceTo(initial, At(4));

            Assert.That(equal.Status, Is.EqualTo(SimulationStatus.NoChange));
            Assert.That(past.Status, Is.EqualTo(SimulationStatus.BackwardTarget));
            Assert.That(equal.State, Is.SameAs(initial));
            Assert.That(past.State, Is.SameAs(initial));
            Assert.That(equal.Trace.Concat(past.Trace), Is.Empty);

            var right = engine.AdvanceTo(initial, At(10));
            Assert.That(right.Trace.Select(e => e.Request.Timestamp.Milliseconds), Is.EqualTo(new[] { 10L }));
            Assert.That(right.State.EventQueue.Events.Single().Request.Timestamp.Milliseconds, Is.EqualTo(11));
            Assert.That(right.State.EventQueue.NextSequence, Is.EqualTo(3));
            var next = engine.AdvanceTo(Restore(right.State), At(11));
            Assert.That(next.Trace.Single().Sequence, Is.EqualTo(2));
            Assert.That(next.State.EventQueue.Events, Is.Empty);
        }

        [TestCase(31_536_000_000L)]
        [TestCase(long.MaxValue)]
        public void EmptyWorldAdvancesAcrossLongRepresentableIntervalsWithoutEffects(long target)
        {
            var engine = CreateEngine(Config());
            var original = new GameState(0, null, ulong.MaxValue);

            var result = engine.AdvanceTo(original, At(target));

            Assert.That(result.Status, Is.EqualTo(SimulationStatus.Advanced));
            Assert.That(result.Reached, Is.EqualTo(At(target)));
            Assert.That(result.State.ActivePet, Is.Null);
            Assert.That(result.State.EventQueue.NextSequence, Is.Zero);
            Assert.That(result.State.Rng.Streams, Is.EqualTo(original.Rng.Streams));
            Assert.That(result.Trace, Is.Empty);
            Assert.That(result.CommittedTransactions, Is.EqualTo(1));
        }

        [Test]
        public void CapturesEveryThresholdAtTheSameBoundaryBeforeIntegratingThePet()
        {
            var cfg = Config(rates: id => id == NeedId.Satiety ? 5 : id == NeedId.Energy ? 2 : 0,
                thresholds: id => id == NeedId.Satiety ? new[] { 101L, 104L, 105L, 109L } :
                    id == NeedId.Energy ? new[] { 102L } : Array.Empty<long>());
            var engine = CreateEngine(cfg, ("mark", (_, _) => { }));
            var initial = engine.Initialize(At(0), Living(cfg), 42, new[] { Event(1) }).State;
            var result = engine.AdvanceTo(initial, At(1));

            Assert.That(result.Trace.Select(e => e.Request.Payload.Threshold?.ExactNumerator),
                Is.EqualTo(new long?[] { 102, 101, 104, 105, null }));
            Assert.That(result.Trace.Select(e => e.Request.Payload.NeedId),
                Is.EqualTo(new NeedId?[] { NeedId.Energy, NeedId.Satiety, NeedId.Satiety, NeedId.Satiety, null }));
            Assert.That(result.ThresholdBoundaries, Is.EqualTo(1));
            Assert.That(result.State.ActivePet.Needs[NeedId.Satiety].ExactNumerator, Is.EqualTo(105));

            var next = engine.AdvanceTo(result.State, At(2));
            Assert.That(next.Trace.Single().Request.Payload.Threshold?.ExactNumerator, Is.EqualTo(109));
            Assert.That(engine.AdvanceTo(next.State, At(2)).Trace, Is.Empty);
        }

        [Test]
        public void SpeedChangesRecalculateFutureCrossingsWithoutLeavingPredictedEventsInTheQueue()
        {
            var cfg = Config(rates: id => id == NeedId.Satiety ? -1 : 0,
                thresholds: id => id == NeedId.Satiety ? new[] { 90L } : Array.Empty<long>(),
                modifiers: new[] { Scale("pause", 0) });
            var engine = CreateEngine(cfg, ("start", (c, _) => c.SetModifierActive("pause", true)),
                ("stop", (c, _) => c.SetModifierActive("pause", false)));
            var first = engine.Initialize(At(0), Living(cfg), 42, new[] { Event(3, "start"), Event(8, "stop") }).State;
            var paused = engine.AdvanceTo(first, At(4));

            Assert.That(paused.State.ActiveModifierIds, Is.EqualTo(new[] { "pause" }));
            Assert.That(paused.State.EventQueue.Events.Single().Request.TypeId, Is.EqualTo("stop"));
            Assert.That(paused.State.ActivePet.Needs[NeedId.Satiety].ExactNumerator, Is.EqualTo(97));
            var continued = engine.AdvanceTo(Restore(paused.State), At(20));
            var crossing = continued.Trace.Single(e => e.Request.TypeId == SimulationEngine.ThresholdEventTypeId);
            Assert.That(crossing.Request.Timestamp.Milliseconds, Is.EqualTo(15));
            Assert.That(continued.State.ActivePet.Needs[NeedId.Satiety].ExactNumerator, Is.EqualTo(85));
            Assert.That(continued.State.ActiveModifierIds, Is.Empty);
        }

        [Test]
        public void IntegratesThroughOpposingEventEffectAndSaturatesBothSignedRates()
        {
            var cfg = Config(rates: id => id == NeedId.Satiety ? -9 : id == NeedId.Energy ? 5 : 0);
            var engine = CreateEngine(cfg, ("set", (c, _) => c.SetNeed(Pet, NeedId.Satiety, Exact(301))));
            var initial = engine.Initialize(At(0), Living(cfg), 42, new[] { Event(2, "set", pet: Pet) }).State;
            var before = engine.AdvanceTo(initial, At(1));
            var jump = engine.AdvanceTo(before.State, At(2));
            var after = engine.AdvanceTo(jump.State, At(3));
            var bounds = engine.AdvanceTo(after.State, At(10_000));

            Assert.That(before.State.ActivePet.Needs[NeedId.Satiety].ExactNumerator, Is.EqualTo(91));
            Assert.That(jump.State.ActivePet.Needs[NeedId.Satiety].ExactNumerator, Is.EqualTo(301));
            Assert.That(after.State.ActivePet.Needs[NeedId.Satiety].ExactNumerator, Is.EqualTo(292));
            Assert.That(bounds.State.ActivePet.Needs[NeedId.Satiety].ExactNumerator, Is.EqualTo(-1000));
            Assert.That(bounds.State.ActivePet.Needs[NeedId.Energy].ExactNumerator, Is.EqualTo(1000));
        }

        [TestCase(60_000L)]
        [TestCase(3_600_000L)]
        [TestCase(28_800_000L)]
        [TestCase(86_400_000L)]
        [TestCase(604_800_000L)]
        [TestCase(31_536_000_000L)]
        public void MixedRulesAndAllEightNeedsAgreeAcrossPartitionsAndIntermediateRestore(long target)
        {
            var cfg = Config(rates: id => id == NeedId.Satiety ? -1 : id == NeedId.Energy ? 1 : 0,
                thresholds: id => id == NeedId.Satiety ? new[] { 96L, 91L, 84L } :
                    id == NeedId.Energy ? new[] { 104L, 121L } : Array.Empty<long>(),
                modifiers: new[] { Scale("double", 2_000_000) });
            var engine = CreateEngine(cfg, ("boost", (c, _) =>
            {
                c.SetModifierActive("double", true);
                c.Schedule(Event(c.Time.Milliseconds, "child", -3, pet: Pet));
            }), ("drop", (c, _) => c.SetModifierActive("double", false)),
                ("mark", (c, _) => c.NextUInt32(RandomStreamId.Digestion)),
                ("child", (c, _) =>
                {
                    c.NextUInt32(RandomStreamId.Birth);
                    c.NextBounded(RandomStreamId.Evolution, 100);
                    c.SetNeed(Pet, NeedId.Bond, Exact(111));
                }));
            var initial = engine.Initialize(At(0), Living(cfg), 42, new[] {
                Event(7, "boost", pet: Pet), Event(12, "drop"), Event(20),
                Event(long.MaxValue, text: "future")
            }).State;
            var direct = engine.AdvanceTo(initial, At(target));
            Assert.That(direct.Status, Is.EqualTo(SimulationStatus.Advanced));
            Assert.That(direct.Trace.Count(e => e.Request.TypeId == SimulationEngine.ThresholdEventTypeId), Is.EqualTo(5));
            Assert.That(direct.Trace.Any(e => e.Request.TypeId == "child"), Is.True);

            Compare(new[] { target / 4, target / 2, 3 * (target / 4), target });
            var irregular = Compare(new[] { 0L, 1, 3, 4, 4, 5, 6, 7, 8, 11, 12, 13, 19, 20, 21, target / 3, target - 1, target });
            if (target == 31_536_000_000L)
            {
                Evidence("partition-parity", initial, direct, irregular);
            }

            SimulationResult Compare(IEnumerable<long> times)
            {
                var state = initial;
                var trace = new List<ScheduledEvent>();
                int transactions = 0;
                int boundaries = 0;
                foreach (long time in times)
                {
                    var split = engine.AdvanceTo(state, At(time));
                    Assert.That(split.Status, Is.EqualTo(SimulationStatus.Advanced).Or.EqualTo(SimulationStatus.NoChange));
                    trace.AddRange(split.Trace);
                    transactions += split.CommittedTransactions;
                    boundaries += split.ThresholdBoundaries;
                    state = Restore(split.State);
                }

                SameState(direct.State, state);
                SameTrace(direct.Trace, trace);
                return new SimulationResult(SimulationStatus.Advanced, At(0), At(target), state, trace, transactions, boundaries);
            }
        }
    }
}
