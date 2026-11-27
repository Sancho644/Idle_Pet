using System;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Pet.Needs;

namespace IdlePet.Domain.Definitions
{
    public sealed class RateModifierDefinition
    {
        public RateModifierDefinition(string id, NeedId target, NeedCoefficient coefficient)
        {
            if (!ConfigurationId.IsValid(id))
            {
                throw new ArgumentException("A stable modifier ID is required.", nameof(id));
            }

            if (!Enum.IsDefined(typeof(NeedId), target) || target == NeedId.Comfort)
            {
                throw new ArgumentOutOfRangeException(nameof(target), "A rate modifier must target a stored need.");
            }

            if (coefficient.Millionths < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(coefficient), "A rate multiplier must be non-negative.");
            }

            Id = id;
            Target = target;
            Coefficient = coefficient;
        }

        public string Id { get; }

        public NeedId Target { get; }

        public NeedCoefficient Coefficient { get; }
    }
}
