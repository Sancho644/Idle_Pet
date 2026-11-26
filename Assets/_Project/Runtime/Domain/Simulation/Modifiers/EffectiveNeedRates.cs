using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Pet.Needs;

namespace IdlePet.Domain.Simulation.Modifiers
{
    public sealed class EffectiveNeedRates
    {
        public EffectiveNeedRates(IEnumerable<KeyValuePair<NeedId, NeedRate>> rates)
        {
            var byId = new SortedDictionary<NeedId, NeedRate>();
            foreach (var entry in rates ?? throw new ArgumentNullException(nameof(rates)))
            {
                if (!Enum.IsDefined(typeof(NeedId), entry.Key) || entry.Key == NeedId.Comfort)
                {
                    throw new ArgumentException($"Unexpected rate for {entry.Key}.", nameof(rates));
                }

                byId.Add(entry.Key, entry.Value);
            }

            foreach (NeedId id in Enum.GetValues(typeof(NeedId)))
            {
                if (id != NeedId.Comfort && !byId.ContainsKey(id))
                {
                    throw new ArgumentException($"Missing rate for {id}.", nameof(rates));
                }
            }

            Values = new ReadOnlyDictionary<NeedId, NeedRate>(byId);
        }

        public IReadOnlyDictionary<NeedId, NeedRate> Values { get; }

        public NeedRate this[NeedId id] => Values[id];
    }
}
