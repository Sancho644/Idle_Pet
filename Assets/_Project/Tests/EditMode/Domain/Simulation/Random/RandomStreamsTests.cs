using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IdlePet.Domain;
using IdlePet.Domain.Simulation.Random;
using NUnit.Framework;

namespace IdlePet.Tests.EditMode.Domain.Simulation.Random
{
    public sealed class RandomStreamsTests
    {
        [Test]
        public void RegistryUsesTheSixExplicitUniqueStableSelectors()
        {
            var expected = new[]
            {
                RandomStreamId.Birth, RandomStreamId.Behaviour, RandomStreamId.Illness,
                RandomStreamId.Evolution, RandomStreamId.Lifecycle, RandomStreamId.Digestion
            };

            Assert.That(RandomStreamRegistry.OrderedIds, Is.EqualTo(expected));
            Assert.That(expected.Select(RandomStreamRegistry.GetSequenceId), Is.EqualTo(new ulong[] { 1, 2, 3, 4, 5, 6 }));
            Assert.Throws<NotSupportedException>(() => ((IList<RandomStreamId>)RandomStreamRegistry.OrderedIds).Clear());
        }

        [TestCase(RandomStreamId.Birth, 1UL, 0x4df1ccf9U, 0xe5838752U)]
        [TestCase(RandomStreamId.Behaviour, 2UL, 0xff85ecc9U, 0x4de4d2f6U)]
        [TestCase(RandomStreamId.Illness, 3UL, 0x5f0a808fU, 0x3a3913faU)]
        [TestCase(RandomStreamId.Evolution, 4UL, 0x0c4aa14eU, 0xd9894a7bU)]
        [TestCase(RandomStreamId.Lifecycle, 5UL, 0xd38f79f5U, 0x513ea01dU)]
        [TestCase(RandomStreamId.Digestion, 6UL, 0x66d2f76aU, 0xb30b37f0U)]
        public void WorldSeed42ProducesTheReferencePairs(RandomStreamId stream, ulong selector, uint first, uint second)
        {
            var world = RandomStreams.Create(42);
            Assert.That(world.CaptureStream(stream).SequenceId, Is.EqualTo(selector));

            Assert.That(world.NextUInt32(stream), Is.EqualTo(first));
            Assert.That(world.NextUInt32(stream), Is.EqualTo(second));
        }

        [Test]
        public void ReferencePairsSurviveInterleavingAndAnAdditionalTestGeneratorUnderAnotherCulture()
        {
            var world = RandomStreams.Create(42);
            var expected = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                var additional = Pcg32.Create(42, 73);

                Assert.That(world.NextUInt32(RandomStreamId.Birth), Is.EqualTo(0x4df1ccf9U));
                additional.NextBounded(10);
                world.NextUInt32(RandomStreamId.Illness);
                Assert.That(world.NextUInt32(RandomStreamId.Behaviour), Is.EqualTo(0xff85ecc9U));
                world.NextBounded(RandomStreamId.Evolution, 0x80000001);
                Assert.That(world.NextUInt32(RandomStreamId.Behaviour), Is.EqualTo(0x4de4d2f6U));
                additional.NextUInt32();
                world.NextUInt32(RandomStreamId.Digestion);
                world.NextUInt32(RandomStreamId.Lifecycle);
                Assert.That(world.NextUInt32(RandomStreamId.Birth), Is.EqualTo(0xe5838752U));
                Assert.That(world.CaptureSnapshot().Streams, Has.Count.EqualTo(6));
            }
            finally
            {
                CultureInfo.CurrentCulture = expected;
            }
        }

        [Test]
        public void ConsumingEachStreamLeavesEveryOtherStreamUntouched()
        {
            foreach (RandomStreamId active in RandomStreamRegistry.OrderedIds)
            {
                var world = RandomStreams.Create(42);
                var independent = RandomStreams.Create(42);
                var initial = world.CaptureSnapshot();
                foreach (uint bound in new uint[] { 10, 0x80000001, uint.MaxValue })
                {
                    world.NextUInt32(active);
                    world.NextBounded(active, bound);
                }

                Assert.That(world.CaptureStream(active).State,
                    Is.Not.EqualTo(independent.CaptureStream(active).State));
                foreach (var other in initial.Streams.Where(stream => stream.StreamId != active))
                {
                    Assert.That(world.CaptureStream(other.StreamId), Is.EqualTo(other));
                }

                RandomSnapshotTests.AssertSame(initial, independent.CaptureSnapshot());
            }
        }

        [Test]
        public void ReverseInitializationOfIndividualGeneratorsMatchesTheWorldStreams()
        {
            const ulong seed = 42;
            var world = RandomStreams.Create(seed);
            foreach (RandomStreamId id in RandomStreamRegistry.OrderedIds.Reverse())
            {
                var independent = Pcg32.Create(seed, RandomStreamRegistry.GetSequenceId(id));
                Assert.That(independent.State, Is.EqualTo(world.CaptureStream(id).State));
                Assert.That(independent.NextUInt32(), Is.EqualTo(world.NextUInt32(id)));
                Assert.That(independent.NextUInt32(), Is.EqualTo(world.NextUInt32(id)));
            }
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(7)]
        public void UnknownIdIsRejectedAndDoesNotAdvanceAnyStream(int value)
        {
            var world = RandomStreams.Create(42);
            var id = (RandomStreamId)value;
            var before = world.CaptureSnapshot();

            Assert.Throws<ArgumentOutOfRangeException>(() => RandomStreamRegistry.GetSequenceId(id));
            Assert.Throws<ArgumentOutOfRangeException>(() => world.NextUInt32(id));
            Assert.Throws<ArgumentOutOfRangeException>(() => world.NextBounded(id, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => world.CaptureStream(id));
            Assert.Throws<ArgumentOutOfRangeException>(() => world.NextBounded(RandomStreamId.Birth, 0));

            RandomSnapshotTests.AssertSame(before, world.CaptureSnapshot());
        }

        [Test]
        public void ReadingRngAndItsMetadataDoesNotChangeGameState()
        {
            var state = new GameState(123, null);
            var world = RandomStreams.Create(42);
            var snapshot = world.CaptureSnapshot();

            Assert.That(world.WorldSeed, Is.EqualTo(42UL));
            Assert.That(world.RngAlgorithmVersion, Is.EqualTo(1));
            world.CaptureStream(RandomStreamId.Birth);
            RandomSnapshotTests.AssertSame(snapshot, world.CaptureSnapshot());
            world.NextUInt32(RandomStreamId.Birth);

            Assert.That(state.LogicalTimeMilliseconds, Is.EqualTo(123));
            Assert.That(state.ActivePet, Is.Null);
        }
    }
}
