using System;
using System.Collections.Generic;
using System.Linq;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet.Needs;

namespace IdlePet.Tests.EditMode.Domain.Needs
{
    internal static class NeedSimulationTestData
    {
        internal static NeedDefinition Single(long min, long max, long initial, params long[] thresholds)
        {
            return new NeedDefinition(NeedId.Satiety, Exact(min), Exact(max), Exact(initial), default, thresholds.Select(Exact));
        }

        internal static NeedValue Exact(long numerator) => NeedValue.FromExactNumerator(numerator);

        internal static NeedsDefinition CreateNeedsDefinition(Func<NeedId, long> rates, long minimum = -3_000_000_000L,
            long maximum = 8_000_000_000L, Func<NeedId, long> start = null)
        {
            return new NeedsDefinition("test-rules", "test-balance", "test-content",
                NeedTestData.CreateEntries().Select(entry => new NeedDefinition(
                    entry.Id, Exact(minimum), Exact(maximum), Exact(start?.Invoke(entry.Id) ?? (long)entry.Id * 1000 + 17),
                    new NeedRate(rates(entry.Id)), Array.Empty<NeedValue>())));
        }

        internal static GameConfiguration Configuration(NeedsDefinition needs, IEnumerable<RateModifierDefinition> modifiers)
        {
            return new GameConfiguration(BalanceProfileKind.FastTesting, "test.rates",
                new SimulationDefinition(1), new TimeDefinition(100, 100, 1, 1, 1), needs, modifiers);
        }

        internal static RateModifierDefinition Modifier(string id, long coefficient, NeedId target = NeedId.Satiety)
        {
            return new RateModifierDefinition(id, target, new NeedCoefficient(coefficient));
        }
    }
}
