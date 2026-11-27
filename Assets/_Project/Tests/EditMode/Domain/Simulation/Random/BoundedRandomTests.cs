using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Domain.Simulation.Random;
using NUnit.Framework;

namespace IdlePet.Tests.EditMode.Domain.Simulation.Random
{
    public sealed class BoundedRandomTests
    {
        [Test]
        public void ApprovedSequenceRejectsZeroAndFiveAndAcceptsSixAfterThreeRawReads()
        {
            var values = new Queue<uint>(new uint[] { 0, 5, 6 });
            int reads = 0;

            uint output = Pcg32.SampleBounded(10, () =>
            {
                reads++;
                return values.Dequeue();
            });

            Assert.That(output, Is.EqualTo(6));
            Assert.That(reads, Is.EqualTo(3));
            Assert.That(values, Is.Empty);
        }

        [TestCase(1U, 0U)]
        [TestCase(2U, 0U)]
        [TestCase(10U, 6U)]
        [TestCase(16U, 0U)]
        [TestCase(100U, 96U)]
        [TestCase(0x80000000U, 0U)]
        [TestCase(0x80000001U, 0x7fffffffU)]
        [TestCase(uint.MaxValue, 1U)]
        public void UInt32AcceptanceBoundaryAndSupportedBoundsAreExact(uint bound, uint threshold)
        {
            int reads = 0;
            var atThreshold = Pcg32.SampleBounded(bound, () =>
            {
                Assert.That(++reads, Is.EqualTo(1), "The threshold itself must be accepted.");
                return threshold;
            });
            Assert.That(atThreshold, Is.EqualTo(threshold));
            Assert.That(reads, Is.EqualTo(1));

            if (threshold != 0)
            {
                var values = new Queue<uint>(new[] { threshold - 1, threshold, uint.MaxValue });
                Assert.That(Pcg32.SampleBounded(bound, values.Dequeue), Is.EqualTo(threshold));
                Assert.That(values.Count, Is.EqualTo(1));
            }

            var rng = Pcg32.Create(42, 54);
            for (int i = 0; i < 256; i++)
            {
                Assert.That(rng.NextBounded(bound), Is.LessThan(bound));
            }
        }

        [TestCase(16, 1)]
        [TestCase(16, 3)]
        [TestCase(16, 6)]
        [TestCase(256, 10)]
        [TestCase(256, 129)]
        [TestCase(256, 255)]
        public void SmallerModelHasExactlyEqualNumbersOfPreimagesForEveryResult(int size, int bound)
        {
            int threshold = size % bound;
            var preimages = new int[bound];
            for (int raw = threshold; raw < size; raw++)
            {
                preimages[raw % bound]++;
            }

            Assert.That(preimages, Is.EqualTo(Enumerable.Repeat(size / bound, bound)));
        }

        [Test]
        public void ZeroBoundIsRejectedWithoutInvokingSourceOrChangingState()
        {
            var rng = Pcg32.Create(42, 54);
            ulong state = rng.State;
            int reads = 0;

            Assert.Throws<ArgumentOutOfRangeException>(() => Pcg32.SampleBounded(0, () => (uint)++reads));
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextBounded(0));

            Assert.That(reads, Is.Zero);
            Assert.That(rng.State, Is.EqualTo(state));
        }

        [Test]
        public void BoundOneConsumesExactlyOneRawValue()
        {
            var bounded = Pcg32.Create(42, 54);
            var raw = Pcg32.Create(42, 54);
            raw.NextUInt32();

            Assert.That(bounded.NextBounded(1), Is.Zero);
            Assert.That(bounded.State, Is.EqualTo(raw.State));
            Assert.That(bounded.NextUInt32(), Is.EqualTo(raw.NextUInt32()));
        }

        [Test]
        public void SnapshotAfterARealRejectionContinuesAtTheStateAfterBothDraws()
        {
            var world = RandomStreams.Create(42);
            var raw = RandomStreams.Create(42);
            Assert.That(raw.NextUInt32(RandomStreamId.Birth), Is.EqualTo(0x4df1ccf9U));
            Assert.That(raw.NextUInt32(RandomStreamId.Birth), Is.EqualTo(0xe5838752U));

            Assert.That(world.NextBounded(RandomStreamId.Birth, 0x80000001), Is.EqualTo(0x65838751U));
            var snapshot = world.CaptureSnapshot();
            RandomSnapshotTests.AssertSame(raw.CaptureSnapshot(), snapshot);
            var resumed = RandomStreams.Restore(snapshot);
            Assert.That(resumed.NextBounded(RandomStreamId.Birth, 0x80000001),
                Is.EqualTo(world.NextBounded(RandomStreamId.Birth, 0x80000001)));
            RandomSnapshotTests.AssertSame(world.CaptureSnapshot(), resumed.CaptureSnapshot());
        }
    }
}
