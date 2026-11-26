using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IdlePet.Domain.Definitions
{
    public sealed class GameConfiguration
    {
        public GameConfiguration(
            BalanceProfileKind profileKind,
            string profileId,
            SimulationDefinition simulation,
            TimeDefinition time,
            NeedsDefinition needs,
            IEnumerable<RateModifierDefinition> rateModifiers)
        {
            if (!Enum.IsDefined(typeof(BalanceProfileKind), profileKind))
            {
                throw new ArgumentOutOfRangeException(nameof(profileKind));
            }

            if (!ConfigurationId.IsValid(profileId))
            {
                throw new ArgumentException("A stable profile ID is required.", nameof(profileId));
            }

            ProfileKind = profileKind;
            ProfileId = profileId;
            Simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            Time = time ?? throw new ArgumentNullException(nameof(time));
            Needs = needs ?? throw new ArgumentNullException(nameof(needs));

            var byId = new SortedDictionary<string, RateModifierDefinition>(StringComparer.Ordinal);
            foreach (RateModifierDefinition modifier in rateModifiers ?? throw new ArgumentNullException(nameof(rateModifiers)))
            {
                if (modifier == null)
                {
                    throw new ArgumentException("A rate modifier is missing.", nameof(rateModifiers));
                }

                byId.Add(modifier.Id, modifier);
            }

            RateModifiers = new ReadOnlyDictionary<string, RateModifierDefinition>(byId);
        }

        public BalanceProfileKind ProfileKind { get; }

        public string ProfileId { get; }

        public SimulationDefinition Simulation { get; }

        public TimeDefinition Time { get; }

        public NeedsDefinition Needs { get; }

        public IReadOnlyDictionary<string, RateModifierDefinition> RateModifiers { get; }
    }
}
