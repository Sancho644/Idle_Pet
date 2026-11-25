using System;

namespace IdlePet.Domain.Common.Numerics
{
    public readonly struct NeedRate : IEquatable<NeedRate>
    {
        public NeedRate(long milliUnitsPerHour)
        {
            MilliUnitsPerHour = milliUnitsPerHour;
        }

        public long MilliUnitsPerHour { get; }

        public bool Equals(NeedRate other) => MilliUnitsPerHour == other.MilliUnitsPerHour;

        public override bool Equals(object obj) => obj is NeedRate other && Equals(other);

        public override int GetHashCode() => MilliUnitsPerHour.GetHashCode();
    }
}
