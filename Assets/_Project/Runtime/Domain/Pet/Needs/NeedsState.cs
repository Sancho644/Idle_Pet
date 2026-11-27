using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Definitions;

namespace IdlePet.Domain.Pet.Needs
{
    public sealed class NeedsState
    {
        private NeedsState(SortedDictionary<NeedId, NeedValue> values)
        {
            Values = new ReadOnlyDictionary<NeedId, NeedValue>(values);
        }

        public IReadOnlyDictionary<NeedId, NeedValue> Values { get; }

        public NeedValue this[NeedId id] => Values[id];

        public static NeedsState Create(NeedsDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var values = new SortedDictionary<NeedId, NeedValue>();
            foreach (NeedDefinition entry in definition.Entries.Values)
            {
                values.Add(entry.Id, entry.InitialValue);
            }

            return new NeedsState(values);
        }

        public static NeedsState Restore(NeedsDefinition definition, IEnumerable<KeyValuePair<NeedId, NeedValue>> values)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var restored = new SortedDictionary<NeedId, NeedValue>();
            foreach (KeyValuePair<NeedId, NeedValue> pair in values ?? throw new ArgumentNullException(nameof(values)))
            {
                if (!definition.Entries.TryGetValue(pair.Key, out NeedDefinition entry))
                {
                    throw new ArgumentException($"Unexpected stored need ID: {pair.Key}.", nameof(values));
                }

                entry.ValidateValue(pair.Value, nameof(values));
                restored.Add(pair.Key, pair.Value);
            }

            if (restored.Count != definition.Entries.Count)
            {
                throw new ArgumentException("The snapshot must contain every stored need.", nameof(values));
            }

            return new NeedsState(restored);
        }
    }
}
