using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet.Needs;

namespace IdlePet.Domain.Simulation.Modifiers
{
    public static class NeedRateCalculator
    {
        public static EffectiveNeedRates Calculate(GameConfiguration configuration, IEnumerable<string> activeModifierIds)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var active = new SortedDictionary<string, RateModifierDefinition>(StringComparer.Ordinal);
            foreach (string id in activeModifierIds ?? throw new ArgumentNullException(nameof(activeModifierIds)))
            {
                if (id == null || !configuration.RateModifiers.TryGetValue(id, out RateModifierDefinition modifier))
                {
                    throw new ArgumentException($"Unknown active rate modifier '{id}'.", nameof(activeModifierIds));
                }

                if (active.ContainsKey(id))
                {
                    throw new ArgumentException($"Duplicate active rate modifier '{id}'.", nameof(activeModifierIds));
                }

                active.Add(id, modifier);
            }

            var byNeed = active.Values.ToLookup(modifier => modifier.Target);
            var rates = configuration.Needs.Entries.Values.Select(need =>
                new KeyValuePair<NeedId, NeedRate>(need.Id, ApplyMultipliers(need, byNeed[need.Id])));
            return new EffectiveNeedRates(rates);
        }

        private static NeedRate ApplyMultipliers(NeedDefinition need, IEnumerable<RateModifierDefinition> modifiers)
        {
            // All activations have been validated before any arithmetic, including zero multipliers.
            BigInteger numerator = need.BaseRate.MilliUnitsPerHour;
            BigInteger denominator = BigInteger.One;
            foreach (RateModifierDefinition modifier in modifiers)
            {
                numerator *= modifier.Coefficient.Millionths;
                denominator *= NeedCoefficient.MillionthsPerUnit;
            }

            var rounded = BigInteger.DivRem(BigInteger.Abs(numerator), denominator, out var remainder);
            if (remainder * 2 >= denominator)
            {
                rounded++;
            }

            if (numerator.Sign < 0)
            {
                rounded = -rounded;
            }

            if (rounded < long.MinValue || rounded > long.MaxValue)
            {
                throw new OverflowException($"The effective rate for {need.Id} is outside the Int64 range.");
            }

            return new NeedRate((long)rounded);
        }
    }
}
