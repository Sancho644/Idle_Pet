using System;
using System.Collections.Generic;
using System.Linq;

namespace IdlePet.Authoring.Mapping
{
    public sealed class ConfigurationIssue
    {
        public ConfigurationIssue(
            ConfigurationIssueCode code, ConfigurationSeverity severity, string asset, string field, string message)
            : this(code, severity, asset, field, message, Array.Empty<string>())
        {
        }

        public ConfigurationIssue(ConfigurationIssueCode code, ConfigurationSeverity severity, string asset,
            string field, string message, IEnumerable<string> relatedAssets)
        {
            Code = code;
            Severity = severity;
            Asset = asset;
            Field = field;
            Message = message;
            RelatedAssets = Array.AsReadOnly(relatedAssets.ToArray());
        }

        public ConfigurationIssueCode Code { get; }

        public ConfigurationSeverity Severity { get; }

        public string Asset { get; }

        public string Field { get; }

        public string Message { get; }

        public IReadOnlyList<string> RelatedAssets { get; }

        public override string ToString()
        {
            string location = RelatedAssets.Count == 0 ? Asset : Asset + " [" + string.Join(", ", RelatedAssets) + "]";
            return $"[{Severity} {Code}] {location} ({Field}): {Message}";
        }
    }
}
