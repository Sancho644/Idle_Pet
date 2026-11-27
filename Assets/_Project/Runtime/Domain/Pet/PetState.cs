using System;
using IdlePet.Domain.Common.Identifiers;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Time;

namespace IdlePet.Domain.Pet
{
    public sealed class PetState
    {
        public PetState(PetId id, NeedsState needs) : this(id, needs, null)
        {
        }

        public PetState(PetId id, NeedsState needs, LogicalTime? terminatedAt)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("A valid pet ID is required.", nameof(id));
            }

            Id = id;
            Needs = needs ?? throw new ArgumentNullException(nameof(needs));
            TerminatedAt = terminatedAt;
        }

        public PetId Id { get; }

        public NeedsState Needs { get; }

        public LogicalTime? TerminatedAt { get; }
    }
}
