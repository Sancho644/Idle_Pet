using System;
using System.Numerics;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Simulation.Time;

namespace IdlePet.Domain.Pet.Needs
{
    public static class NeedIntegrator
    {
        public static NeedValue Integrate(NeedValue current, NeedRate rate, LogicalDuration duration, NeedDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition.ValidateValue(current, nameof(current));
            if (duration.Milliseconds == 0 || rate.MilliUnitsPerHour == 0)
            {
                return current;
            }

            // Widen before multiplication. A valid result can saturate after an arbitrarily large product.
            BigInteger numerator = current.ExactNumerator + (BigInteger)rate.MilliUnitsPerHour * duration.Milliseconds;
            if (numerator <= definition.Minimum.ExactNumerator)
            {
                return definition.Minimum;
            }

            if (numerator >= definition.Maximum.ExactNumerator)
            {
                return definition.Maximum;
            }

            return NeedValue.FromExactNumerator((long)numerator);
        }
    }
}
