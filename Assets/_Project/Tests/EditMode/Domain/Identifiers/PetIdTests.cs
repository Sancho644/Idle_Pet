using System;
using System.Globalization;
using IdlePet.Domain.Common.Identifiers;
using NUnit.Framework;

namespace IdlePet.Tests.EditMode.Domain.Identifiers
{
    public sealed class PetIdTests
    {
        [Test]
        public void StableTextRoundTripsWithValueEqualityAndEqualHashes()
        {
            var original = new PetId("pet.I_2-7");
            var restored = new PetId(original.Value);

            Assert.That(restored.IsValid, Is.True);
            Assert.That(restored.Value, Is.EqualTo("pet.I_2-7"));
            Assert.That(restored, Is.EqualTo(original));
            Assert.That(restored.GetHashCode(), Is.EqualTo(original.GetHashCode()));
            Assert.That(restored, Is.Not.EqualTo(new PetId("pet.I_2-8")));
        }

        [Test]
        public void ComparisonAndEqualityAreOrdinalInDifferentCultures()
        {
            var upper = new PetId("I");
            var lower = new PetId("i");
            var dottedName = new PetId("pet.v1");
            var current = CultureInfo.CurrentCulture;
            try
            {
                foreach (string cultureName in new[] { "en-US", "tr-TR" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

                    Assert.That(upper.CompareTo(lower), Is.LessThan(0));
                    Assert.That(lower.CompareTo(upper), Is.GreaterThan(0));
                    Assert.That(upper.Equals(lower), Is.False);
                    Assert.That(dottedName.CompareTo(new PetId("pet.v1")), Is.Zero);
                    Assert.That(dottedName.Equals(new PetId("pet.v1")), Is.True);
                    Assert.That(dottedName.GetHashCode(), Is.EqualTo(new PetId("pet.v1").GetHashCode()));
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = current;
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" pet")]
        [TestCase("pet ")]
        [TestCase("pet/1")]
        [TestCase("pet\n1")]
        [TestCase("pet\u0130")]
        [TestCase(".pet")]
        public void RejectsInvalidStableText(string value)
        {
            Assert.Throws<ArgumentException>(() => new PetId(value));
        }

        [Test]
        public void DefaultIdCannotSupplyStableText()
        {
            PetId id = default;

            Assert.That(id.IsValid, Is.False);
            Assert.That(id.Equals(new PetId("pet")), Is.False);
            Assert.Throws<InvalidOperationException>(() => _ = id.Value);
        }
    }
}
