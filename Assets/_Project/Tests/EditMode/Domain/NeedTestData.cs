using System.Collections.Generic;
using System.Linq;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet.Needs;

namespace IdlePet.Tests.EditMode.Domain
{
    internal static class NeedTestData
    {
        // Arbitrary EditMode fixture. Production balance will be supplied by authoring.
        public static NeedDefinition[] CreateEntries()
        {
            var storedIds = new[]
            {
                NeedId.Satiety, NeedId.Energy, NeedId.Hygiene, NeedId.Health,
                NeedId.Happiness, NeedId.Boredom, NeedId.Discipline, NeedId.Bond
            };

            return storedIds.Select((id, index) => new NeedDefinition(
                id, Value(200), Value(12_000), Value(1_700L + 75 * index),
                new NeedRate(-230), new[] { Value(600), Value(10_000) })).ToArray();
        }

        public static NeedsDefinition CreateDefinition()
        {
            return new NeedsDefinition("test-rules", "test-balance", "test-content", CreateEntries());
        }

        public static Dictionary<NeedId, NeedValue> CreateValues(NeedsDefinition definition)
        {
            return definition.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.InitialValue);
        }

        public static NeedValue Value(long milliUnits, int remainder = 0)
        {
            return new NeedValue(milliUnits, new NeedRemainder(remainder));
        }
    }
}
