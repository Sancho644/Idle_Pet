using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Domain;
using IdlePet.Domain.Common.Identifiers;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Pet;
using IdlePet.Domain.Pet.Needs;
using NUnit.Framework;

namespace IdlePet.Tests.EditMode.Domain.State
{
    public sealed class GameStateTests
    {
        [Test]
        public void CreatesPetFromExplicitIdAndConfiguredInitialValues()
        {
            var definition = NeedTestData.CreateDefinition();
            var id = new PetId("pet.test-1");
            var game = new GameState(4_294_967_296L, new PetState(id, NeedsState.Create(definition)));

            Assert.That(game.LogicalTimeMilliseconds, Is.EqualTo(4_294_967_296L));
            Assert.That(game.ActivePet.Id, Is.EqualTo(id));
            Assert.That(game.ActivePet.Needs.Values.Count, Is.EqualTo(8));
            Assert.That(game.ActivePet.Needs.Values.ContainsKey(NeedId.Comfort), Is.False);
            foreach (var entry in definition.Entries)
            {
                Assert.That(game.ActivePet.Needs[entry.Key], Is.EqualTo(entry.Value.InitialValue), entry.Key.ToString());
            }
        }

        [Test]
        public void RestoresFromSnapshotPartsWithoutLosingPrecision()
        {
            var definition = NeedTestData.CreateDefinition();
            var fromSave = NeedTestData.CreateValues(definition);
            fromSave[NeedId.Satiety] = NeedTestData.Value(1_234, 3_599_999);
            fromSave[NeedId.Energy] = NeedTestData.Value(4_321, 1);
            var original = new GameState(long.MaxValue, new PetState(new PetId("pet.snapshot"), NeedsState.Restore(definition, fromSave)));

            var copiedValues = original.ActivePet.Needs.Values.Select(entry =>
                new KeyValuePair<NeedId, NeedValue>(
                    (NeedId)(int)entry.Key,
                    new NeedValue(entry.Value.MilliUnits, new NeedRemainder(entry.Value.Remainder.Numerator))));
            var restored = new GameState(original.LogicalTimeMilliseconds,
                new PetState(new PetId(original.ActivePet.Id.Value), NeedsState.Restore(definition, copiedValues)));

            Assert.That(restored.LogicalTimeMilliseconds, Is.EqualTo(long.MaxValue));
            Assert.That(restored.ActivePet.Id, Is.EqualTo(original.ActivePet.Id));
            Assert.That(restored.ActivePet.Needs.Values, Is.EqualTo(original.ActivePet.Needs.Values));
            Assert.That(restored.ActivePet.Needs[NeedId.Satiety].Remainder.Numerator, Is.EqualTo(3_599_999));
            Assert.That(restored.ActivePet.Needs[NeedId.Energy].Remainder.Numerator, Is.EqualTo(1));
        }

        [Test]
        public void EmptyGameRetainsCursorWithoutCreatingPet()
        {
            var game = new GameState(0, null);

            Assert.That(game.LogicalTimeMilliseconds, Is.Zero);
            Assert.That(game.ActivePet, Is.Null);
        }

        [Test]
        public void RejectsNegativeCursorAndInvalidPet()
        {
            var needs = NeedsState.Create(NeedTestData.CreateDefinition());

            Assert.Throws<ArgumentOutOfRangeException>(() => new GameState(-1, null));
            Assert.Throws<ArgumentException>(() => new PetState(default, needs));
            Assert.Throws<ArgumentNullException>(() => new PetState(new PetId("pet"), null));
        }

        [Test]
        public void GamesUsingSharedDefinitionsOwnIndependentNeeds()
        {
            var definition = NeedTestData.CreateDefinition();
            var first = new GameState(12, new PetState(new PetId("pet.1"), NeedsState.Create(definition)));
            var source = NeedTestData.CreateValues(definition);
            var restoredSatiety = NeedTestData.Value(2_000, 1);
            source[NeedId.Satiety] = restoredSatiety;
            var second = new GameState(20, new PetState(new PetId("pet.2"), NeedsState.Restore(definition, source)));

            source[NeedId.Satiety] = NeedTestData.Value(5_000);

            Assert.That(second.ActivePet.Needs.Values, Is.Not.SameAs(first.ActivePet.Needs.Values));
            Assert.That(second.ActivePet.Needs[NeedId.Satiety], Is.EqualTo(restoredSatiety));
            Assert.That(first.ActivePet.Needs[NeedId.Satiety], Is.EqualTo(NeedTestData.Value(1_700)));
            Assert.That(definition.Entries[NeedId.Satiety].InitialValue, Is.EqualTo(NeedTestData.Value(1_700)));
            Assert.Throws<NotSupportedException>(() => ((IDictionary<NeedId, NeedValue>)second.ActivePet.Needs.Values).Clear());
        }

        [TestCase(200L)]
        [TestCase(12_000L)]
        public void RestoresExactScaleBoundaries(long milliUnits)
        {
            var definition = NeedTestData.CreateDefinition();
            var source = NeedTestData.CreateValues(definition);
            var boundary = NeedTestData.Value(milliUnits);
            source[NeedId.Satiety] = boundary;

            var restored = NeedsState.Restore(definition, source);

            Assert.That(restored[NeedId.Satiety], Is.EqualTo(boundary));
        }

        [TestCase(199L, 3_599_999)]
        [TestCase(12_000L, 1)]
        public void RejectsSnapshotOutsideScaleEvenByOneRemainderUnit(long milliUnits, int remainder)
        {
            var definition = NeedTestData.CreateDefinition();
            var source = NeedTestData.CreateValues(definition);
            source[NeedId.Satiety] = NeedTestData.Value(milliUnits, remainder);

            Assert.Throws<ArgumentOutOfRangeException>(() => NeedsState.Restore(definition, source));
        }

        [TestCase(0)]
        [TestCase(7)]
        [TestCase(99)]
        public void RejectsUnknownAndComputedNeedsInRestoredState(int id)
        {
            var definition = NeedTestData.CreateDefinition();
            var source = NeedTestData.CreateValues(definition);
            source.Add((NeedId)id, NeedTestData.Value(800));

            Assert.Throws<ArgumentException>(() => NeedsState.Restore(definition, source));
        }

        [Test]
        public void RejectsMissingDuplicateAndUndefinedStateData()
        {
            var definition = NeedTestData.CreateDefinition();
            var source = NeedTestData.CreateValues(definition);

            Assert.Throws<ArgumentException>(() => NeedsState.Restore(definition, source.Skip(1)));
            Assert.Throws<ArgumentException>(() => NeedsState.Restore(definition, source.Append(source.First())));
            Assert.Throws<ArgumentNullException>(() => NeedsState.Restore(definition, null));
            Assert.Throws<ArgumentNullException>(() => NeedsState.Restore(null, source));
            Assert.Throws<ArgumentNullException>(() => NeedsState.Create(null));
        }
    }
}
