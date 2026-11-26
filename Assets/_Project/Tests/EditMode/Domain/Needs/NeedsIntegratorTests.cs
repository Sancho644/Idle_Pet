using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Domain;
using IdlePet.Domain.Common.Identifiers;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Pet;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Modifiers;
using IdlePet.Domain.Simulation.Time;
using IdlePet.Tests.EditMode.Application.Time;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Domain.Needs.NeedSimulationTestData;

namespace IdlePet.Tests.EditMode.Domain.Needs
{
    public sealed class NeedsIntegratorTests
    {
        [TestCase(60_000L)]
        [TestCase(3_600_000L)]
        [TestCase(28_800_000L)]
        [TestCase(86_400_000L)]
        [TestCase(604_800_000L)]
        [TestCase(31_536_000_000L)]
        public void FixedRatesGiveExactPartitionParityForAllEightNeeds(long duration)
        {
            var definition = CreateNeedsDefinition(id => ((long)id % 3 - 1) * 713);
            var initial = NeedsState.Create(definition);
            var rates = new EffectiveNeedRates(definition.Entries.Select(pair => KeyValuePair.Create(pair.Key, pair.Value.BaseRate)));
            var direct = Advance(initial, duration);
            var quarter = duration / 4;
            var samples = new[] { 0L, 1L, 2L, 0L, duration / 7, duration / 11, duration / 13 };
            var unequal = samples.Append(duration - samples.Sum());
            var irregular = Enumerable.Range(1, 17).Select(i => duration / (i * i + 2) / 4).ToArray();

            AssertSameValues(direct, Apply(initial, Enumerable.Repeat(quarter, 4)));
            AssertSameValues(direct, Apply(initial, unequal));
            AssertSameValues(direct, Apply(initial, irregular.Append(duration - irregular.Sum())));
            Assert.That(initial.Values.Values.Select(value => value.ExactNumerator),
                Is.EqualTo(definition.Entries.Values.Select(entry => entry.InitialValue.ExactNumerator)));

            NeedsState Advance(NeedsState state, long time) => NeedsIntegrator.Integrate(state, definition, rates, new LogicalDuration(time));
            NeedsState Apply(NeedsState state, IEnumerable<long> steps) => steps.Aggregate(state, Advance);
        }

        [Test]
        public void MatchingRateChangeBoundariesPreserveParityAtAndAroundSaturation()
        {
            var definition = CreateNeedsDefinition(_ => 0, -25, 26, _ => 1);
            var initial = NeedsState.Create(definition);
            var rising = new EffectiveNeedRates(definition.Entries.Keys.Select(id => KeyValuePair.Create(id, new NeedRate(5))));
            var falling = new EffectiveNeedRates(definition.Entries.Keys.Select(id => KeyValuePair.Create(id, new NeedRate(-3))));
            var direct = Apply(Apply(initial, rising, 15), falling, 12);
            var split = Apply(Apply(initial, rising, 0, 1, 3, 1, 1, 9), falling, 1, 1, 3, 0, 7);

            AssertSameValues(direct, split);
            Assert.That(direct.Values.Values.Select(value => value.ExactNumerator), Is.All.EqualTo(-10));

            NeedsState Apply(NeedsState state, EffectiveNeedRates rates, params long[] steps)
            {
                return steps.Aggregate(state, (value, time) => NeedsIntegrator.Integrate(value, definition, rates, new LogicalDuration(time)));
            }
        }

        [TestCase((int)NeedId.Comfort)]
        [TestCase(999)]
        public void EffectiveRatesRejectComputedOrUnknownNeeds(int id)
        {
            var entries = CreateNeedsDefinition(_ => 0).Entries.Values.Select(entry => KeyValuePair.Create(entry.Id, entry.BaseRate));

            Assert.Throws<ArgumentException>(() => new EffectiveNeedRates(entries.Append(KeyValuePair.Create((NeedId)id, new NeedRate(1)))));
        }

        [Test]
        public void EffectiveRatesAreCompleteSortedAndDefensivelyCopied()
        {
            var definition = CreateNeedsDefinition(_ => 2);
            var source = definition.Entries.Values.Reverse().Select(entry => KeyValuePair.Create(entry.Id, entry.BaseRate)).ToList();
            var rates = new EffectiveNeedRates(source);

            Assert.Throws<ArgumentException>(() => new EffectiveNeedRates(source.Skip(1)));
            Assert.Throws<ArgumentException>(() => new EffectiveNeedRates(source.Append(source[0])));
            source.Clear();
            Assert.That(rates.Values.Keys, Is.EqualTo(definition.Entries.Keys));
            Assert.That(rates.Values, Has.Count.EqualTo(8));
            Assert.Throws<NotSupportedException>(() => ((IDictionary<NeedId, NeedRate>)rates.Values).Clear());
        }

        [Test]
        public void IndependentStatesAndCursorAreUntouchedByIntegrationAndByReadingFakeTime()
        {
            var definition = CreateNeedsDefinition(_ => 2);
            var first = NeedsState.Create(definition);
            var second = NeedsState.Create(definition);
            var game = new GameState(1000, new PetState(new PetId("first"), first), 42UL);
            var clock = new FakeClock(new LogicalTime(1005));
            var rates = new EffectiveNeedRates(definition.Entries.Values.Select(entry => KeyValuePair.Create(entry.Id, entry.BaseRate)));

            Assert.That(SimulationClock.TryGetElapsed(SimulationClock.GetTime(game), clock.GetLogicalTime(), out var elapsed), Is.True);
            AssertSameValues(first, second);
            var computed = NeedsIntegrator.Integrate(first, definition, rates, elapsed);

            Assert.That(game.LogicalTimeMilliseconds, Is.EqualTo(1000));
            Assert.That(game.ActivePet.Needs, Is.SameAs(first));
            AssertSameValues(first, second);
            Assert.That(computed[NeedId.Satiety].ExactNumerator, Is.EqualTo(first[NeedId.Satiety].ExactNumerator + 10));
            Assert.Throws<NotSupportedException>(() => ((IDictionary<NeedId, NeedValue>)computed.Values).Clear());
            Assert.That(first.Values, Has.Count.EqualTo(8));
        }

        [Test]
        public void ErrorForALaterNeedLeavesTheEntireSourceSnapshotUntouched()
        {
            var originalDefinition = CreateNeedsDefinition(_ => 2);
            var original = NeedsState.Create(originalDefinition);
            var saved = original.Values.ToDictionary(pair => pair.Key, pair => pair.Value);
            var narrower = CreateNeedsDefinition(_ => 2, 0, 8_080, _ => 15);
            var rates = new EffectiveNeedRates(narrower.Entries.Values.Select(entry => KeyValuePair.Create(entry.Id, entry.BaseRate)));

            var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
                NeedsIntegrator.Integrate(original, narrower, rates, new LogicalDuration(11)));

            Assert.That(error.Message, Does.Contain(nameof(NeedId.Bond)));
            Assert.That(original.Values, Is.EqualTo(saved));
        }

        private static void AssertSameValues(NeedsState expected, NeedsState actual)
        {
            Assert.That(actual.Values.Keys, Is.EqualTo(expected.Values.Keys));
            foreach (var pair in expected.Values)
            {
                NeedValue value = actual[pair.Key];
                Assert.That(value.MilliUnits, Is.EqualTo(pair.Value.MilliUnits), pair.Key.ToString());
                Assert.That(value.Remainder, Is.EqualTo(pair.Value.Remainder), pair.Key.ToString());
                Assert.That(value.ExactNumerator, Is.EqualTo(pair.Value.ExactNumerator), pair.Key.ToString());
            }
        }
    }
}
