using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Simulation.Time;

namespace IdlePet.Domain.Pet.Needs
{
    public static class NeedThresholdCalculator
    {
        public static IReadOnlyList<NeedThresholdCrossing> FindCrossings(
            NeedValue current, NeedRate rate, LogicalDuration duration, NeedDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition.ValidateValue(current, nameof(current));
            int sign = Math.Sign(rate.MilliUnitsPerHour);
            if (sign == 0 || duration.Milliseconds == 0)
            {
                return Array.Empty<NeedThresholdCrossing>();
            }

            var speed = BigInteger.Abs(rate.MilliUnitsPerHour);
            var direction = sign > 0 ? NeedThresholdDirection.Rising : NeedThresholdDirection.Falling;
            var thresholds = sign > 0 ? definition.Thresholds : definition.Thresholds.Reverse();
            var crossings = new List<NeedThresholdCrossing>();
            foreach (NeedValue threshold in thresholds)
            {
                BigInteger difference = (BigInteger)threshold.ExactNumerator - current.ExactNumerator;
                if (difference.Sign != sign)
                {
                    continue;
                }

                var elapsed = BigInteger.DivRem(BigInteger.Abs(difference), speed, out var remainder);
                if (!remainder.IsZero)
                {
                    elapsed++;
                }

                // Traversal follows movement, so every later threshold is at least as far away.
                if (elapsed > duration.Milliseconds)
                {
                    break;
                }

                crossings.Add(new NeedThresholdCrossing(definition.Id, threshold, direction, new LogicalDuration((long)elapsed)));
            }

            return crossings.AsReadOnly();
        }
    }
}
