using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IdlePet.Domain.Pet.Needs;

namespace IdlePet.Domain.Definitions
{
    public sealed class NeedsDefinition
    {
        public NeedsDefinition(
            string rulesVersion,
            string balanceVersion,
            string contentVersion,
            IEnumerable<NeedDefinition> entries)
        {
            RulesVersion = RequireVersion(rulesVersion, nameof(rulesVersion));
            BalanceVersion = RequireVersion(balanceVersion, nameof(balanceVersion));
            ContentVersion = RequireVersion(contentVersion, nameof(contentVersion));

            var byId = new SortedDictionary<NeedId, NeedDefinition>();
            foreach (NeedDefinition entry in entries ?? throw new ArgumentNullException(nameof(entries)))
            {
                if (entry == null)
                {
                    throw new ArgumentException("A need definition entry is missing.", nameof(entries));
                }

                byId.Add(entry.Id, entry);
            }

            foreach (NeedId id in Enum.GetValues(typeof(NeedId)))
            {
                if (id != NeedId.Comfort && !byId.ContainsKey(id))
                {
                    throw new ArgumentException($"Missing definition for {id}.", nameof(entries));
                }
            }

            Entries = new ReadOnlyDictionary<NeedId, NeedDefinition>(byId);
        }

        public string RulesVersion { get; }

        public string BalanceVersion { get; }

        public string ContentVersion { get; }

        public IReadOnlyDictionary<NeedId, NeedDefinition> Entries { get; }

        private static string RequireVersion(string version, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("A configuration version is required.", parameterName);
            }

            return version;
        }
    }
}
