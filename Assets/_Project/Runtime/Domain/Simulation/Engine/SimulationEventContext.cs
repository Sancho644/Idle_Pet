using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Domain.Common.Identifiers;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Modifiers;
using IdlePet.Domain.Simulation.Random;
using IdlePet.Domain.Simulation.Time;

namespace IdlePet.Domain.Simulation.Engine
{
    public sealed class SimulationEventContext
    {
        private readonly GameConfiguration _configuration;
        private readonly Action<EventRequest> _validateRequest;
        private readonly LogicalTime _time;
        private readonly RandomStreams _rng;
        private readonly SortedSet<string> _modifiers;
        private PetState _pet;
        private bool _open = true;
        private bool _modifiersChanged;

        internal SimulationEventContext(GameConfiguration configuration, Action<EventRequest> validateRequest,
            GameState checkpoint, LogicalTime time, PetState pet)
        {
            _configuration = configuration;
            _validateRequest = validateRequest;
            _time = time;
            _pet = pet;
            _rng = RandomStreams.Restore(checkpoint.Rng);
            _modifiers = new SortedSet<string>(checkpoint.ActiveModifierIds, StringComparer.Ordinal);
            Queue = new EventScheduler(checkpoint.EventQueue);
            ProcessedEvents = checkpoint.LogicalTimeMilliseconds == time.Milliseconds ? checkpoint.ProcessedEventsAtCursor : 0;
        }

        public LogicalTime Time
        {
            get
            {
                RequireOpen();
                return _time;
            }
        }

        public PetState Pet
        {
            get
            {
                RequireOpen();
                return _pet;
            }
        }

        internal EventScheduler Queue { get; }

        internal ScheduledEvent CurrentEvent { get; set; }

        internal int ProcessedEvents { get; set; }

        public long Schedule(EventRequest request)
        {
            RequireOpen();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            _validateRequest(request);

            if (request.Timestamp.CompareTo(_time) < 0)
            {
                throw new SimulationRuleException(SimulationFaultCode.PastEvent, "Cannot schedule an event in the past.");
            }

            if (request.Scope == EventScope.PetLife)
            {
                RequireLivingPet(request.PetId.Value);
            }

            return Queue.Register(_time, request).Sequence;
        }

        public bool Cancel(long sequence)
        {
            RequireOpen();
            return Queue.Cancel(sequence);
        }

        public void SetNeed(PetId petId, NeedId id, NeedValue value)
        {
            RequireOpen();
            RequireLivingPet(petId);
            if (!_configuration.Needs.Entries.TryGetValue(id, out NeedDefinition definition))
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "A stored need is required.");
            }

            definition.ValidateValue(value, nameof(value));
            var values = _pet.Needs.Values.Select(pair => new KeyValuePair<NeedId, NeedValue>(pair.Key, pair.Key == id ? value : pair.Value));
            var updated = NeedsState.Restore(_configuration.Needs, values);
            _pet = new PetState(_pet.Id, updated);
        }

        public bool IsModifierActive(string id)
        {
            RequireOpen();
            RequireKnownModifier(id);
            return _modifiers.Contains(id);
        }

        public bool SetModifierActive(string id, bool active)
        {
            RequireOpen();
            RequireKnownModifier(id);
            bool changed = active ? _modifiers.Add(id) : _modifiers.Remove(id);
            _modifiersChanged |= changed;
            return changed;
        }

        public uint NextUInt32(RandomStreamId stream)
        {
            RequireOpen();
            return _rng.NextUInt32(stream);
        }

        public uint NextBounded(RandomStreamId stream, uint bound)
        {
            RequireOpen();
            return _rng.NextBounded(stream, bound);
        }

        public bool TerminatePet(PetId id)
        {
            RequireOpen();
            if (_pet == null || !_pet.Id.Equals(id))
            {
                throw new SimulationRuleException(SimulationFaultCode.InvalidPet, "The terminal transition must target the active pet.");
            }

            if (_pet.TerminatedAt.HasValue)
            {
                return false;
            }

            _pet = new PetState(_pet.Id, _pet.Needs, _time);
            Queue.CancelPetLife(id);
            return true;
        }

        internal GameState Commit()
        {
            if (_modifiersChanged)
            {
                NeedRateCalculator.Calculate(_configuration, _modifiers);
            }

            return GameState.Restore(_time.Milliseconds, _pet, _rng.CaptureSnapshot(), Queue.Capture(), _modifiers, ProcessedEvents);
        }

        internal void Close() => _open = false;

        private void RequireOpen()
        {
            if (!_open)
            {
                throw new InvalidOperationException("The simulation transaction has ended.");
            }
        }

        private void RequireKnownModifier(string id)
        {
            if (id == null || !_configuration.RateModifiers.ContainsKey(id))
            {
                throw new ArgumentException("Unknown rate modifier.", nameof(id));
            }
        }

        private void RequireLivingPet(PetId id)
        {
            if (_pet == null || _pet.TerminatedAt.HasValue || !_pet.Id.Equals(id))
            {
                throw new SimulationRuleException(SimulationFaultCode.InvalidPet, "The operation must target the living active pet.");
            }
        }
    }
}
