using System;

namespace IdlePet.Domain.Common.Numerics
{
    public readonly struct NeedRemainder : IEquatable<NeedRemainder>
    {
        public const int Denominator = 3_600_000;

        public NeedRemainder(int numerator)
        {
            if (numerator < 0 || numerator >= Denominator)
            {
                throw new ArgumentOutOfRangeException(nameof(numerator), numerator, "The remainder must be canonical.");
            }

            Numerator = numerator;
        }

        public int Numerator { get; }

        public bool Equals(NeedRemainder other) => Numerator == other.Numerator;

        public override bool Equals(object obj) => obj is NeedRemainder other && Equals(other);

        public override int GetHashCode() => Numerator;
    }
}
