using System;
using IdlePet.Domain.Common.Identifiers;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Pet.Needs;
using IdlePet.Domain.Simulation.Engine;
using IdlePet.Domain.Simulation.Events;
using IdlePet.Domain.Simulation.Random;

namespace IdlePet.Domain.Simulation.Rules
{
    // A closed set of transaction operations for technical scenarios. No caller-supplied code is stored.
    internal sealed class SimulationRuleEffect
    {
        private enum Operation { Schedule, Cancel, SetNeed, SetModifier, Draw, DrawBounded, Terminate, Fail }

        private readonly Operation _operation;
        private readonly EventRequest _request;
        private readonly long _sequence;
        private readonly PetId _pet;
        private readonly NeedId _need;
        private readonly NeedValue _value;
        private readonly string _text;
        private readonly bool _active;
        private readonly RandomStreamId _stream;
        private readonly uint _bound;

        private SimulationRuleEffect(Operation operation, EventRequest request = null, long sequence = 0,
            PetId pet = default, NeedId need = default, NeedValue value = default, string text = null,
            bool active = false, RandomStreamId stream = default, uint bound = 0)
        {
            _operation = operation;
            _request = request;
            _sequence = sequence;
            _pet = pet;
            _need = need;
            _value = value;
            _text = text;
            _active = active;
            _stream = stream;
            _bound = bound;
        }

        internal static SimulationRuleEffect Schedule(EventRequest request) =>
            new SimulationRuleEffect(Operation.Schedule, request: request ?? throw new ArgumentNullException(nameof(request)));

        internal static SimulationRuleEffect Cancel(long sequence) =>
            new SimulationRuleEffect(Operation.Cancel, sequence: sequence);

        internal static SimulationRuleEffect SetNeed(PetId pet, NeedId need, NeedValue value) =>
            new SimulationRuleEffect(Operation.SetNeed, pet: pet, need: need, value: value);

        internal static SimulationRuleEffect SetModifier(string id, bool active) =>
            new SimulationRuleEffect(Operation.SetModifier, text: id, active: active);

        internal static SimulationRuleEffect Draw(RandomStreamId stream) =>
            new SimulationRuleEffect(Operation.Draw, stream: stream);

        internal static SimulationRuleEffect DrawBounded(RandomStreamId stream, uint bound) =>
            new SimulationRuleEffect(Operation.DrawBounded, stream: stream, bound: bound);

        internal static SimulationRuleEffect Terminate(PetId pet) =>
            new SimulationRuleEffect(Operation.Terminate, pet: pet);

        internal static SimulationRuleEffect Fail(string reason) =>
            new SimulationRuleEffect(Operation.Fail, text: reason);

        internal void Apply(SimulationEventContext context)
        {
            switch (_operation)
            {
                case Operation.Schedule: context.Schedule(_request); break;
                case Operation.Cancel: context.Cancel(_sequence); break;
                case Operation.SetNeed: context.SetNeed(_pet, _need, _value); break;
                case Operation.SetModifier: context.SetModifierActive(_text, _active); break;
                case Operation.Draw: context.NextUInt32(_stream); break;
                case Operation.DrawBounded: context.NextBounded(_stream, _bound); break;
                case Operation.Terminate: context.TerminatePet(_pet); break;
                case Operation.Fail: throw new SimulationRuleException(_text);
                default: throw new InvalidOperationException("Unknown simulation rule operation.");
            }
        }
    }
}
