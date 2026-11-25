using System;

namespace IdlePet.Domain.Common.Numerics
{
    public readonly struct NeedCoefficient : IEquatable<NeedCoefficient>
    {
        public const int MillionthsPerUnit = 1_000_000;

        public NeedCoefficient(long millionths)
        {
            Millionths = millionths;
        }

        public long Millionths { get; }

        public bool Equals(NeedCoefficient other) => Millionths == other.Millionths;

        public override bool Equals(object obj) => obj is NeedCoefficient other && Equals(other);

        public override int GetHashCode() => Millionths.GetHashCode();
    }
}
