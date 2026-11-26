using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Domain.Simulation.Random;
using NUnit.Framework;

namespace IdlePet.Tests.EditMode.Domain.Simulation.Random
{
    public sealed class RandomSnapshotTests
    {
        [TestCase(0UL)]
        [TestCase(ulong.MaxValue)]
        public void SnapshotPreservesSeedVersionSelectorsAndEveryBitOfState(ulong seed)
        {
            ulong[] states = { 0, ulong.MaxValue, 0x8000000000000000UL, 0x123456789abcdef0UL, 1, 0xf7079824c154bf23UL };
            var ids = RandomStreamRegistry.OrderedIds;
            var inputs = ids.Select((id, i) => new RandomStreamSnapshot(id, (ulong)i + 1, states[i])).Reverse().ToList();

            var snapshot = new RandomStreamsSnapshot(seed, 1, inputs);
            var world = RandomStreams.Restore(snapshot);
            Assert.That(snapshot.WorldSeed, Is.EqualTo(seed));
            Assert.That(snapshot.RngAlgorithmVersion, Is.EqualTo(1));
            Assert.That(snapshot.Streams.Select(stream => stream.StreamId), Is.EqualTo(ids));
            Assert.That(snapshot.Streams.Select(stream => stream.SequenceId), Is.EqualTo(new ulong[] { 1, 2, 3, 4, 5, 6 }));
            Assert.That(snapshot.Streams.Select(stream => stream.State), Is.EqualTo(states));
            AssertSame(snapshot, world.CaptureSnapshot());

            foreach (var stream in snapshot.Streams)
            {
                uint expected = Pcg32.Restore(stream.SequenceId, stream.State).NextUInt32();
                Assert.That(world.NextUInt32(stream.StreamId), Is.EqualTo(expected));
            }
        }

        [Test]
        public void SnapshotCopiesInputsAndDoesNotExposeAMutableCollection()
        {
            var world = RandomStreams.Create(42);
            var original = world.CaptureSnapshot();
            var input = original.Streams.ToArray();
            var copy = new RandomStreamsSnapshot(original.WorldSeed, original.RngAlgorithmVersion, input);

            input[0] = default;
            world.NextBounded(RandomStreamId.Birth, 10);

            Assert.Throws<NotSupportedException>(() => ((IList<RandomStreamSnapshot>)copy.Streams).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<RandomStreamSnapshot>)copy.Streams)[0] = default);
            AssertSame(original, copy);
            Assert.That(world.CaptureStream(RandomStreamId.Birth).State, Is.Not.EqualTo(copy.Streams[0].State));
        }

        [Test]
        public void CopiesRestoredFromSameSnapshotOwnTheirGeneratorsIndependently()
        {
            var original = RandomStreams.Create(42);
            original.NextBounded(RandomStreamId.Birth, 0x80000001);
            original.NextUInt32(RandomStreamId.Behaviour);
            original.NextBounded(RandomStreamId.Lifecycle, 16);
            var baseSnapshot = original.CaptureSnapshot();
            var a = RandomStreams.Restore(baseSnapshot);
            var b = RandomStreams.Restore(baseSnapshot);

            uint value = a.NextBounded(RandomStreamId.Evolution, 100);

            AssertSame(baseSnapshot, original.CaptureSnapshot());
            AssertSame(baseSnapshot, b.CaptureSnapshot());
            Assert.That(a.CaptureStream(RandomStreamId.Evolution).State, Is.Not.EqualTo(b.CaptureStream(RandomStreamId.Evolution).State));
            Assert.That(b.NextBounded(RandomStreamId.Evolution, 100), Is.EqualTo(value));
            Assert.That(original.NextBounded(RandomStreamId.Evolution, 100), Is.EqualTo(value));
            AssertSame(a.CaptureSnapshot(), b.CaptureSnapshot());
            AssertSame(a.CaptureSnapshot(), original.CaptureSnapshot());
        }

        [Test]
        public void MixedOperationsAreDeterministicAfterAnIntermediateReorderedSnapshot()
        {
            var calls = new (RandomStreamId Id, uint Bound)[]
            {
                (RandomStreamId.Birth, 0x80000001), (RandomStreamId.Digestion, 0),
                (RandomStreamId.Behaviour, 10), (RandomStreamId.Lifecycle, 0),
                (RandomStreamId.Digestion, 16), (RandomStreamId.Illness, 0),
                (RandomStreamId.Evolution, 1), (RandomStreamId.Birth, uint.MaxValue),
                (RandomStreamId.Behaviour, 0), (RandomStreamId.Illness, 100),
                (RandomStreamId.Evolution, 0x80000001), (RandomStreamId.Lifecycle, 0),
            };
            var a = RandomStreams.Create(42);
            var b = RandomStreams.Create(42);
            for (int i = 0; i < calls.Length; i++)
            {
                var call = calls[i];
                Assert.That(Draw(a, call.Id, call.Bound), Is.EqualTo(Draw(b, call.Id, call.Bound)));
                var point = a.CaptureSnapshot();
                AssertSame(point, b.CaptureSnapshot());
                if (i == calls.Length / 2)
                {
                    var reversed = new RandomStreamsSnapshot(point.WorldSeed, point.RngAlgorithmVersion, point.Streams.Reverse());
                    b = RandomStreams.Restore(reversed);
                    AssertSame(point, b.CaptureSnapshot());
                }
            }
        }

        [TestCase(0)]
        [TestCase(2)]
        public void UnsupportedVersionsAreRejectedBeforeReadingTheirStreams(int version)
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => new RandomStreamsSnapshot(42, version, null));

            Assert.That(error.ParamName, Is.EqualTo("rngAlgorithmVersion"));
        }

        [Test]
        public void MissingCollectionSnapshotAndStreamAreRejected()
        {
            var streams = RandomStreams.Create(42).CaptureSnapshot().Streams;

            Assert.Throws<ArgumentNullException>(() => new RandomStreamsSnapshot(42, 1, null));
            Assert.Throws<ArgumentNullException>(() => RandomStreams.Restore(null));
            var missing = Assert.Throws<ArgumentException>(() => new RandomStreamsSnapshot(42, 1, streams.Take(5)));
            Assert.That(missing.Message, Does.Contain(nameof(RandomStreamId.Digestion)));
        }

        [Test]
        public void DuplicateStreamIsRejectedInsteadOfOverwritingState()
        {
            var streams = RandomStreams.Create(42).CaptureSnapshot().Streams;
            var duplicated = streams.Append(new RandomStreamSnapshot(RandomStreamId.Birth, 1, 0));

            var error = Assert.Throws<ArgumentException>(() => new RandomStreamsSnapshot(42, 1, duplicated));

            Assert.That(error.Message, Does.StartWith("Duplicate random stream"));
        }

        [TestCase(0, 0UL)]
        [TestCase(77, 77UL)]
        [TestCase(-1, 1UL)]
        public void UnknownAndDefaultStreamIdsAreRejected(int id, ulong sequence)
        {
            var streams = RandomStreams.Create(42).CaptureSnapshot().Streams.ToArray();
            streams[0] = new RandomStreamSnapshot((RandomStreamId)id, sequence, 0);

            Assert.Throws<ArgumentOutOfRangeException>(() => new RandomStreamsSnapshot(42, 1, streams));
            streams[0] = default;
            Assert.Throws<ArgumentOutOfRangeException>(() => new RandomStreamsSnapshot(42, 1, streams));
        }

        [TestCase(0UL)]
        [TestCase(54UL)]
        public void MismatchedSelectorIsRejected(ulong selector)
        {
            var streams = RandomStreams.Create(42).CaptureSnapshot().Streams.ToArray();
            streams[1] = new RandomStreamSnapshot(RandomStreamId.Behaviour, selector, 0);

            var error = Assert.Throws<ArgumentException>(() => new RandomStreamsSnapshot(42, 1, streams));

            Assert.That(error.Message, Does.Contain("must be 2"));
        }

        [Test]
        public void SelectorMustFitIn63Bits()
        {
            var streams = RandomStreams.Create(42).CaptureSnapshot().Streams.ToArray();
            streams[1] = new RandomStreamSnapshot(RandomStreamId.Behaviour, 0x8000000000000000UL, 0);

            Assert.Throws<ArgumentOutOfRangeException>(() => new RandomStreamsSnapshot(42, 1, streams));
        }

        [Test]
        public void DuplicateSelectorAcrossDifferentStreamIdsIsRejected()
        {
            var streams = RandomStreams.Create(42).CaptureSnapshot().Streams.ToArray();
            streams[1] = new RandomStreamSnapshot(RandomStreamId.Behaviour, 1, ulong.MaxValue);

            var error = Assert.Throws<ArgumentException>(() => new RandomStreamsSnapshot(42, 1, streams));

            Assert.That(error.Message, Does.StartWith("Duplicate random selector"));
        }

        internal static void AssertSame(RandomStreamsSnapshot expected, RandomStreamsSnapshot actual)
        {
            Assert.That(actual.WorldSeed, Is.EqualTo(expected.WorldSeed));
            Assert.That(actual.RngAlgorithmVersion, Is.EqualTo(expected.RngAlgorithmVersion));
            Assert.That(actual.Streams, Is.EqualTo(expected.Streams));
        }

        private static uint Draw(RandomStreams world, RandomStreamId id, uint bound) =>
            bound == 0 ? world.NextUInt32(id) : world.NextBounded(id, bound);
    }
}
