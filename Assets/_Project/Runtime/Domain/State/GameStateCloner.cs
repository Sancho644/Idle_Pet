using System;

namespace IdlePet.Domain.State
{
    public static class GameStateCloner
    {
        public static GameState Clone(GameState source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            // The pet, RNG snapshot and queue are immutable. Each transaction restores its own working copies.
            return GameState.Restore(source.LogicalTimeMilliseconds, source.ActivePet, source.Rng, source.EventQueue,
                source.ActiveModifierIds, source.ProcessedEventsAtCursor);
        }
    }
}
