using System;
using IdlePet.Domain.Common.Identifiers;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Simulation.Time;

namespace IdlePet.Domain.Simulation.Events
{
    public sealed class EventRequest
    {
        public EventRequest(LogicalTime timestamp, int priority, string sourceId, string typeId,
            EventScope scope, PetId? petId, EventPayload payload = default)
        {
            if (!ConfigurationId.IsValid(sourceId))
            {
                throw new ArgumentException("A stable event source ID is required.", nameof(sourceId));
            }

            if (!ConfigurationId.IsValid(typeId))
            {
                throw new ArgumentException("A stable event type ID is required.", nameof(typeId));
            }

            if (scope != EventScope.World && scope != EventScope.PetLife)
            {
                throw new ArgumentOutOfRangeException(nameof(scope));
            }

            if ((scope == EventScope.PetLife && (!petId.HasValue || !petId.Value.IsValid)) ||
                (scope == EventScope.World && petId.HasValue))
            {
                throw new ArgumentException("A pet ID is required only for a pet life event.", nameof(petId));
            }

            Timestamp = timestamp;
            Priority = priority;
            SourceId = sourceId;
            TypeId = typeId;
            Scope = scope;
            PetId = petId;
            Payload = payload;
        }

        public LogicalTime Timestamp { get; }

        public int Priority { get; }

        public string SourceId { get; }

        public string TypeId { get; }

        public EventScope Scope { get; }

        public PetId? PetId { get; }

        public EventPayload Payload { get; }
    }
}
