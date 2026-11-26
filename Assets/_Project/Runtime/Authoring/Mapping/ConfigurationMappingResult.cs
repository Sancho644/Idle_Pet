using System.Collections.Generic;
using IdlePet.Domain.Definitions;

namespace IdlePet.Authoring.Mapping
{
    public sealed class ConfigurationMappingResult
    {
        internal ConfigurationMappingResult(GameConfiguration definition, IReadOnlyList<ConfigurationIssue> issues)
        {
            Definition = definition;
            Issues = issues;
        }

        public GameConfiguration Definition { get; }

        public IReadOnlyList<ConfigurationIssue> Issues { get; }

        public bool Succeeded => Definition != null;
    }
}
