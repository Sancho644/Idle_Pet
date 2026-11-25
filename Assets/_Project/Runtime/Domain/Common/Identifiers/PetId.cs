using System;

namespace IdlePet.Domain.Common.Identifiers
{
    public readonly struct PetId : IEquatable<PetId>, IComparable<PetId>
    {
        private readonly string _value;

        public PetId(string value)
        {
            if (string.IsNullOrEmpty(value) || !IsAsciiLetterOrDigit(value[0]))
            {
                throw new ArgumentException("A pet ID must start with an ASCII letter or digit.", nameof(value));
            }

            foreach (char character in value)
            {
                if (!IsAsciiLetterOrDigit(character) && character != '-' && character != '_' && character != '.')
                {
                    throw new ArgumentException("A pet ID may contain only ASCII letters, digits, '-', '_' or '.'.", nameof(value));
                }
            }

            _value = value;
        }

        public bool IsValid => _value != null;

        public string Value => _value ?? throw new InvalidOperationException("The pet ID is uninitialized.");

        public bool Equals(PetId other) => StringComparer.Ordinal.Equals(_value, other._value);

        public override bool Equals(object obj) => obj is PetId other && Equals(other);

        public override int GetHashCode() => IsValid ? StringComparer.Ordinal.GetHashCode(_value) : 0;

        public int CompareTo(PetId other) => StringComparer.Ordinal.Compare(_value, other._value);

        private static bool IsAsciiLetterOrDigit(char character)
        {
            return (character >= 'a' && character <= 'z')
                || (character >= 'A' && character <= 'Z')
                || (character >= '0' && character <= '9');
        }
    }
}
