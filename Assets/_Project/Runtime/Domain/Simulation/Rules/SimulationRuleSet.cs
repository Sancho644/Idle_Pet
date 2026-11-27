using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Simulation.Engine;

namespace IdlePet.Domain.Simulation.Rules
{
    internal sealed class SimulationRuleSet
    {
        private readonly Dictionary<string, SimulationRuleEffect[]> _effects;

        internal static SimulationRuleSet Empty { get; } =
            new SimulationRuleSet(Array.Empty<(string, SimulationRuleEffect[])>());

        internal SimulationRuleSet(IEnumerable<(string TypeId, SimulationRuleEffect[] Effects)> rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            _effects = new Dictionary<string, SimulationRuleEffect[]>(StringComparer.Ordinal);
            foreach (var (typeId, effects) in rules)
            {
                if (!ConfigurationId.IsValid(typeId) || typeId == SimulationEngine.ThresholdEventTypeId ||
                    effects == null || effects.Any(effect => effect == null))
                {
                    throw new ArgumentException("A technical rule requires a stable, nonreserved event ID and defined effects.", nameof(rules));
                }

                _effects.Add(typeId, (SimulationRuleEffect[])effects.Clone());
            }
        }

        internal bool Contains(string typeId) => _effects.ContainsKey(typeId);

        internal void Apply(string typeId, SimulationEventContext context)
        {
            if (_effects.TryGetValue(typeId, out var effects))
            {
                foreach (var effect in effects)
                {
                    effect.Apply(context);
                }
            }
        }
    }
}
