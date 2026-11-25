using System;
using IdlePet.Domain.Pet;

namespace IdlePet.Domain
{
    public sealed class GameState
    {
        public GameState(long logicalTimeMilliseconds, PetState activePet)
        {
            if (logicalTimeMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(logicalTimeMilliseconds));
            }

            LogicalTimeMilliseconds = logicalTimeMilliseconds;
            ActivePet = activePet;
        }

        public long LogicalTimeMilliseconds { get; }

        public PetState ActivePet { get; }
    }
}
