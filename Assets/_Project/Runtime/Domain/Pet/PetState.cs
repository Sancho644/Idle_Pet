using System;
using IdlePet.Domain.Common.Identifiers;
using IdlePet.Domain.Pet.Needs;

namespace IdlePet.Domain.Pet
{
    public sealed class PetState
    {
        public PetState(PetId id, NeedsState needs)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("A valid pet ID is required.", nameof(id));
            }

            Id = id;
            Needs = needs ?? throw new ArgumentNullException(nameof(needs));
        }

        public PetId Id { get; }

        public NeedsState Needs { get; }
    }
}
