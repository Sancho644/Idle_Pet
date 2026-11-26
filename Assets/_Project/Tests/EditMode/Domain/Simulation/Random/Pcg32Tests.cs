using System;
using System.Linq;
using IdlePet.Domain.Simulation.Random;
using NUnit.Framework;

namespace IdlePet.Tests.EditMode.Domain.Simulation.Random
{
    public sealed class Pcg32Tests
    {
        [Test]
        public void ReferenceSeedUsesTheTwoStepInitializationAndReportsAllSixOutputs()
        {
            var rng = Pcg32.Create(42, 54);
            Assert.That(rng.State, Is.EqualTo(0x185706b82c2e03f8UL));
            Assert.That(rng.SequenceId, Is.EqualTo(54UL));

            var actual = new uint[6];
            for (int i = 0; i < actual.Length; i++)
            {
                actual[i] = rng.NextUInt32();
                if (i == 2)
                {
                    Assert.That(rng.State, Is.EqualTo(0xf7079824c154bf23UL));
                }
            }

            Assert.That(actual, Is.EqualTo(new uint[]
            {
                0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e
            }));
        }

        [Test]
        public void RestorationFromTheLiteralStateContinuesWithoutReseeding()
        {
            var rng = Pcg32.Restore(54, 0xf7079824c154bf23UL);
            Assert.That(rng.State, Is.EqualTo(0xf7079824c154bf23UL));

            Assert.That(Draw(rng, 3), Is.EqualTo(new uint[] { 0x83d2f293, 0xbfa4784b, 0xcbed606e }));
            Assert.That(rng.State, Is.EqualTo(0xbeb6d0b73fdb974aUL));
        }

        // Additional vectors were calculated independently using arbitrary precision integer arithmetic.
        [TestCase(0UL, 0UL, 0x5851f42d4c957f2eUL, 0xe4c14788U, 0x379c6516U)]
        [TestCase(ulong.MaxValue, 0UL, 1UL, 0U, 0xe4c14788U)]
        [TestCase(0UL, 0x7fffffffffffffffUL, 0xa7ae0bd2b36a80d2UL, 0x44726f5cU, 0xcbd39f38U)]
        [TestCase(ulong.MaxValue, 0x7fffffffffffffffUL, 0x4f5c17a566d501a5UL, 0x2675c047U, 0x7779a837U)]
        public void ZeroAndMaximumSeedsAndSelectorsKeepAllBits(ulong seed, ulong selector, ulong initial, uint first, uint second)
        {
            checked
            {
                var rng = Pcg32.Create(seed, selector);

                Assert.That(rng.SequenceId, Is.EqualTo(selector));
                Assert.That(rng.State, Is.EqualTo(initial));
                Assert.That(rng.NextUInt32(), Is.EqualTo(first));
                Assert.That(rng.NextUInt32(), Is.EqualTo(second));
            }
        }

        [TestCase(0x8000000000000000UL)]
        [TestCase(ulong.MaxValue)]
        public void SelectorWithTheHighBitSetIsRejectedForBothEntryPoints(ulong selector)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Pcg32.Create(0, selector));
            Assert.Throws<ArgumentOutOfRangeException>(() => Pcg32.Restore(selector, 0));
        }

        [TestCase(0UL, 0U, 0x6dUL)]
        [TestCase(0x00123456789abcdeUL, 0x02468a5eU, 0xeec1062fcc1b5573UL)]
        [TestCase(0x08123456789abcdeUL, 0x0123652fU, 0x56c1062fcc1b5573UL)]
        [TestCase(0xf8123456789abcdeUL, 0x048294bcU, 0x86c1062fcc1b5573UL)]
        [TestCase(ulong.MaxValue, 0xfff00001U, 0xa7ae0bd2b36a8140UL)]
        public void OutputUsesOldStateAndHandlesRotationsZeroOneAndThirtyOne(ulong state, uint output, ulong nextState)
        {
            var rng = Pcg32.Restore(54, state);
            Assert.That(rng.State, Is.EqualTo(state));

            Assert.That(rng.NextUInt32(), Is.EqualTo(output));
            Assert.That(rng.State, Is.EqualTo(nextState));
        }

        [Test]
        public void SeedAndSelectorDetermineARepeatableSequence()
        {
            var expected = Draw(Pcg32.Create(42, 54), 32);

            Assert.That(Draw(Pcg32.Create(42, 54), 32), Is.EqualTo(expected));
            Assert.That(Draw(Pcg32.Create(43, 54), 32), Is.Not.EqualTo(expected));
            Assert.That(Draw(Pcg32.Create(42, 55), 32), Is.Not.EqualTo(expected));
        }

        private static uint[] Draw(Pcg32 rng, int count) =>
            Enumerable.Range(0, count).Select(_ => rng.NextUInt32()).ToArray();
    }
}
