using System;
using System.Linq;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Modifiers;
using NUnit.Framework;
using static IdlePet.Tests.EditMode.Domain.Needs.NeedSimulationTestData;

namespace IdlePet.Tests.EditMode.Domain.Simulation.Modifiers
{
    public sealed class NeedRateCalculatorTests
    {
        [TestCase(6000L, 1_250_000L, 1_000_000L, 7500L)]
        [TestCase(-6000L, 1_250_000L, 1_000_000L, -7500L)]
        [TestCase(6000L, 1_250_000L, 800_000L, 6000L)]
        [TestCase(-6000L, 1_250_000L, 800_000L, -6000L)]
        [TestCase(1L, 500_000L, 1_000_000L, 1L)]
        [TestCase(-1L, 500_000L, 1_000_000L, -1L)]
        [TestCase(1L, 500_000L, 500_000L, 0L)]
        [TestCase(-1L, 500_000L, 500_000L, 0L)]
        [TestCase(1L, 499_999L, 1_000_000L, 0L)]
        [TestCase(-1L, 499_999L, 1_000_000L, 0L)]
        [TestCase(1L, 500_001L, 1_000_000L, 1L)]
        [TestCase(-1L, 500_001L, 1_000_000L, -1L)]
        [TestCase(6000L, 0L, 1_000_000L, 0L)]
        [TestCase(0L, long.MaxValue, long.MaxValue, 0L)]
        public void RoundsTheExactProductOnceAndOnlyChangesTheTargetNeed(long baseRate, long first, long second, long expected)
        {
            var definitions = CreateNeedsDefinition(_ => baseRate);
            var config = Configuration(definitions, new[] { Modifier("fx.A", first), Modifier("fx.a", second) });

            var forward = NeedRateCalculator.Calculate(config, new[] { "fx.A", "fx.a" });
            var reversed = NeedRateCalculator.Calculate(config, new[] { "fx.a", "fx.A" });

            Assert.That(forward[NeedId.Satiety].MilliUnitsPerHour, Is.EqualTo(expected));
            Assert.That(forward.Values, Is.EqualTo(reversed.Values));
            Assert.That(forward.Values.Where(entry => entry.Key != NeedId.Satiety)
                .Select(entry => entry.Value.MilliUnitsPerHour), Is.All.EqualTo(baseRate));
            Assert.That(definitions.Entries[NeedId.Satiety].BaseRate.MilliUnitsPerHour, Is.EqualTo(baseRate));
        }

        [Test]
        public void EmptyActivationKeepsAllBaseRatesEvenWithAZeroMultiplierInTheCatalog()
        {
            var definitions = CreateNeedsDefinition(id => -(long)id);
            var config = Configuration(definitions, new[] { Modifier("inactive", 0) });

            var rates = NeedRateCalculator.Calculate(config, Array.Empty<string>());

            Assert.That(rates.Values.Select(entry => entry.Value), Is.EqualTo(definitions.Entries.Values.Select(entry => entry.BaseRate)));
        }

        [Test]
        public void OneActiveModifierLeavesOtherCatalogEntriesInactive()
        {
            var config = Configuration(CreateNeedsDefinition(_ => 6000), new[] { Modifier("active", 1_250_000), Modifier("inactive", 0) });

            Assert.That(NeedRateCalculator.Calculate(config, new[] { "active" })[NeedId.Satiety].MilliUnitsPerHour, Is.EqualTo(7500));
        }

        [TestCase("missing", "Unknown")]
        [TestCase("ZERO", "Unknown")]
        [TestCase("zero", "Duplicate")]
        [TestCase(null, "Unknown")]
        public void ValidatesEveryActivationEvenAfterAZeroFactor(string other, string message)
        {
            var config = Configuration(CreateNeedsDefinition(_ => 0), new[] { Modifier("zero", 0) });

            var error = Assert.Throws<ArgumentException>(() => NeedRateCalculator.Calculate(config, new[] { "zero", other }));

            Assert.That(error.Message, Does.StartWith(message));
        }

        [Test]
        public void RateModifierDefinitionRejectsNegativeCoefficients()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Modifier("negative", -1));
        }

        [TestCase(long.MaxValue)]
        [TestCase(long.MinValue)]
        public void HugeIntermediatesCanCancelBeforeTheFinalRateRangeCheck(long baseRate)
        {
            var factors = Enumerable.Range(0, 6).Select(i => Modifier("factor." + i, i < 3 ? 1_000_000_000_000L : 1L)).ToArray();
            var config = Configuration(CreateNeedsDefinition(_ => baseRate), factors);

            var rates = NeedRateCalculator.Calculate(config, factors.Reverse().Select(factor => factor.Id));

            Assert.That(rates[NeedId.Satiety].MilliUnitsPerHour, Is.EqualTo(baseRate));
        }

        [TestCase(long.MaxValue)]
        [TestCase(long.MinValue)]
        public void NonrepresentableFinalRateIsAnExplicitOverflow(long baseRate)
        {
            var config = Configuration(CreateNeedsDefinition(_ => baseRate), new[] { Modifier("large", 1_000_001) });

            var error = Assert.Throws<OverflowException>(() => NeedRateCalculator.Calculate(config, new[] { "large" }));

            Assert.That(error.Message, Does.Contain(nameof(NeedId.Satiety)));
        }
    }
}
