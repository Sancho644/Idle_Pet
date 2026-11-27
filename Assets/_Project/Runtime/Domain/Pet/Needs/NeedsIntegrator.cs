using System;
using System.Collections.Generic;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Simulation.Modifiers;
using IdlePet.Domain.Simulation.Time;

namespace IdlePet.Domain.Pet.Needs
{
    public static class NeedsIntegrator
    {
        public static NeedsState Integrate(NeedsState current, NeedsDefinition definition, EffectiveNeedRates rates, LogicalDuration duration)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (rates == null)
            {
                throw new ArgumentNullException(nameof(rates));
            }

            var values = new List<KeyValuePair<NeedId, NeedValue>>(definition.Entries.Count);
            foreach (NeedDefinition entry in definition.Entries.Values)
            {
                var value = NeedIntegrator.Integrate(current[entry.Id], rates[entry.Id], duration, entry);
                values.Add(new KeyValuePair<NeedId, NeedValue>(entry.Id, value));
            }

            return NeedsState.Restore(definition, values);
        }
    }
}
