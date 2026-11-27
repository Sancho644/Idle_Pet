using System;

namespace IdlePet.Domain.Common.Numerics
{
    public readonly struct NeedValue : IEquatable<NeedValue>, IComparable<NeedValue>
    {
        public const int MilliUnitsPerUnit = 1_000;

        public NeedValue(long milliUnits, NeedRemainder remainder)
        {
            // For negative values, form a nonpositive sum without underflowing before adding the remainder.
            ExactNumerator = milliUnits < 0 && remainder.Numerator != 0
                ? checked((milliUnits + 1) * NeedRemainder.Denominator - (NeedRemainder.Denominator - remainder.Numerator))
                : checked(milliUnits * NeedRemainder.Denominator + remainder.Numerator);

            MilliUnits = milliUnits;
            Remainder = remainder;
        }

        public long MilliUnits { get; }

        public NeedRemainder Remainder { get; }

        public long ExactNumerator { get; }

        public static NeedValue FromExactNumerator(long exactNumerator)
        {
            long milliUnits = Math.DivRem(exactNumerator, NeedRemainder.Denominator, out long remainder);
            if (remainder < 0)
            {
                milliUnits--;
                remainder += NeedRemainder.Denominator;
            }

            return new NeedValue(milliUnits, new NeedRemainder((int)remainder));
        }

        public bool Equals(NeedValue other) => ExactNumerator == other.ExactNumerator;

        public override bool Equals(object obj) => obj is NeedValue other && Equals(other);

        public override int GetHashCode() => ExactNumerator.GetHashCode();

        public int CompareTo(NeedValue other) => ExactNumerator.CompareTo(other.ExactNumerator);
    }
}
