using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Random;

namespace IdlePet.Domain
{
    public sealed class GameState
    {
        public GameState(long logicalTimeMilliseconds, PetState activePet, ulong worldSeed)
            : this(logicalTimeMilliseconds, activePet, RandomStreams.Create(worldSeed).CaptureSnapshot(),
                EventQueueSnapshot.Empty, Array.Empty<string>(), 0)
        {
        }

        private GameState(long logicalTimeMilliseconds, PetState activePet, RandomStreamsSnapshot rng,
            EventQueueSnapshot eventQueue, IEnumerable<string> activeModifierIds, int processedEventsAtCursor)
        {
            if (logicalTimeMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(logicalTimeMilliseconds));
            }

            if (activePet?.TerminatedAt?.Milliseconds > logicalTimeMilliseconds)
            {
                throw new ArgumentException("The terminal time cannot be ahead of the processed cursor.", nameof(activePet));
            }

            Rng = rng ?? throw new ArgumentNullException(nameof(rng));
            EventQueue = eventQueue ?? throw new ArgumentNullException(nameof(eventQueue));
            if (processedEventsAtCursor < 0 || processedEventsAtCursor > eventQueue.NextSequence)
            {
                throw new ArgumentOutOfRangeException(nameof(processedEventsAtCursor));
            }

            foreach (var entry in eventQueue.Events)
            {
                var request = entry.Request;
                if (request.Timestamp.Milliseconds <= logicalTimeMilliseconds)
                {
                    throw new ArgumentException("A stable queue may contain only events after its cursor.", nameof(eventQueue));
                }

                if (request.Scope == EventScope.PetLife &&
                    (activePet == null || activePet.TerminatedAt.HasValue || !activePet.Id.Equals(request.PetId.Value)))
                {
                    throw new ArgumentException("A queued pet life event must belong to the living active pet.", nameof(eventQueue));
                }
            }

            var modifiers = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string id in activeModifierIds ?? throw new ArgumentNullException(nameof(activeModifierIds)))
            {
                if (!ConfigurationId.IsValid(id) || !modifiers.Add(id))
                {
                    throw new ArgumentException("An active modifier ID must be valid and unique.", nameof(activeModifierIds));
                }
            }

            LogicalTimeMilliseconds = logicalTimeMilliseconds;
            ActivePet = activePet;
            ActiveModifierIds = Array.AsReadOnly(modifiers.ToArray());
            ProcessedEventsAtCursor = processedEventsAtCursor;
        }

        public long LogicalTimeMilliseconds { get; }

        public PetState ActivePet { get; }

        public RandomStreamsSnapshot Rng { get; }

        public EventQueueSnapshot EventQueue { get; }

        public IReadOnlyList<string> ActiveModifierIds { get; }

        public int ProcessedEventsAtCursor { get; }

        public static GameState Restore(long logicalTimeMilliseconds, PetState activePet, RandomStreamsSnapshot rng,
            EventQueueSnapshot eventQueue, IEnumerable<string> activeModifierIds, int processedEventsAtCursor)
        {
            return new GameState(logicalTimeMilliseconds, activePet, rng, eventQueue, activeModifierIds, processedEventsAtCursor);
        }
    }
}
