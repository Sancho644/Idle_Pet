using System;
using System.Collections.Generic;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Pet.Needs;

namespace IdlePet.Domain.Definitions
{
    public sealed class NeedDefinition
    {
        public NeedDefinition(
            NeedId id,
            NeedValue minimum,
            NeedValue maximum,
            NeedValue initialValue,
            NeedRate baseRate,
            IEnumerable<NeedValue> thresholds)
        {
            if (!Enum.IsDefined(typeof(NeedId), id) || id == NeedId.Comfort)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "A stored need requires a valid ID.");
            }

            if (minimum.CompareTo(maximum) >= 0)
            {
                throw new ArgumentException("The maximum must exceed the minimum.", nameof(maximum));
            }

            Id = id;
            Minimum = minimum;
            Maximum = maximum;
            BaseRate = baseRate;

            ValidateValue(initialValue, nameof(initialValue));
            InitialValue = initialValue;

            var sortedThresholds = new SortedSet<NeedValue>();
            foreach (NeedValue threshold in thresholds ?? throw new ArgumentNullException(nameof(thresholds)))
            {
                ValidateValue(threshold, nameof(thresholds));
                if (!sortedThresholds.Add(threshold))
                {
                    throw new ArgumentException("Thresholds must be distinct.", nameof(thresholds));
                }
            }

            Thresholds = new List<NeedValue>(sortedThresholds).AsReadOnly();
        }

        public NeedId Id { get; }

        public NeedValue Minimum { get; }

        public NeedValue Maximum { get; }

        public NeedValue InitialValue { get; }

        public NeedRate BaseRate { get; }

        public IReadOnlyList<NeedValue> Thresholds { get; }

        public bool Contains(NeedValue value) => value.CompareTo(Minimum) >= 0 && value.CompareTo(Maximum) <= 0;

        internal void ValidateValue(NeedValue value, string parameterName)
        {
            if (!Contains(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value.ExactNumerator,
                    $"The exact numerator is outside the configured range for {Id}.");
            }
        }
    }
}
