using System;
using System.Collections.Generic;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet.Needs;

namespace IdlePet.Authoring.Mapping
{
    internal static class NeedsConfigMapper
    {
        internal static List<NeedDefinition> Map(NeedsConfigSO asset, ConfigurationDiagnostics diagnostics)
        {
            var definitions = new List<NeedDefinition>();
            var seen = new HashSet<NeedId>();
            for (int i = 0; i < asset.Entries.Count; i++)
            {
                string field = $"{nameof(asset.Entries)}[{i}]";
                NeedAuthoringData entry = asset.Entries[i];
                if (entry == null)
                {
                    diagnostics.Error(asset, field, ConfigurationIssueCode.MissingReference, "The need entry is missing.");
                    continue;
                }

                if (!Enum.IsDefined(typeof(NeedId), entry.Id))
                {
                    diagnostics.Error(asset, field + ".Id", ConfigurationIssueCode.UnknownEnumValue, "Unknown need ID.");
                    continue;
                }

                if (entry.Id == NeedId.Comfort)
                {
                    diagnostics.Error(asset, field + ".Id", ConfigurationIssueCode.ComputedNeed, "Comfort is computed.");
                    continue;
                }

                if (!seen.Add(entry.Id))
                {
                    diagnostics.Error(asset, field + ".Id", ConfigurationIssueCode.DuplicateNeed, $"Duplicate need {entry.Id}.");
                    continue;
                }

                MapEntry(asset, entry, field, diagnostics, definitions);
            }

            foreach (NeedId id in Enum.GetValues(typeof(NeedId)))
            {
                if (id != NeedId.Comfort && !seen.Contains(id))
                {
                    diagnostics.Error(asset, nameof(asset.Entries), ConfigurationIssueCode.MissingNeed, $"Missing need {id}.");
                }
            }

            return definitions;
        }

        private static void MapEntry(NeedsConfigSO asset, NeedAuthoringData entry, string field,
            ConfigurationDiagnostics diagnostics, List<NeedDefinition> definitions)
        {
            int issueCount = diagnostics.Count;
            bool minValid = diagnostics.TryReadNeedValue(asset, field + ".Minimum", entry.Minimum, out NeedValue minimum);
            bool maxValid = diagnostics.TryReadNeedValue(asset, field + ".Maximum", entry.Maximum, out NeedValue maximum);
            bool initialValid = diagnostics.TryReadNeedValue(asset, field + ".InitialValue", entry.InitialValue, out NeedValue initial);
            diagnostics.TryReadFixed(asset, field + ".BaseRatePerHour", entry.BaseRatePerHour, 3, NeedValue.MilliUnitsPerUnit, out long rate);

            bool rangeValid = minValid && maxValid;
            if (rangeValid && minimum.CompareTo(maximum) >= 0)
            {
                diagnostics.Error(asset, field + ".Maximum", ConfigurationIssueCode.InvalidRange, "The maximum must exceed the minimum.");
                rangeValid = false;
            }

            if (rangeValid && initialValid && IsOutside(initial, minimum, maximum))
            {
                diagnostics.Error(asset, field + ".InitialValue", ConfigurationIssueCode.InitialValueOutOfRange,
                    "The initial value is outside the configured range.");
            }

            var thresholds = new HashSet<NeedValue>();
            for (int i = 0; i < entry.Thresholds.Count; i++)
            {
                string thresholdField = $"{field}.Thresholds[{i}]";
                if (!diagnostics.TryReadNeedValue(asset, thresholdField, entry.Thresholds[i], out NeedValue threshold))
                {
                    continue;
                }

                if (rangeValid && IsOutside(threshold, minimum, maximum))
                {
                    diagnostics.Error(asset, thresholdField, ConfigurationIssueCode.ThresholdOutOfRange,
                        "The threshold is outside the configured range.");
                }

                if (!thresholds.Add(threshold))
                {
                    diagnostics.Error(asset, thresholdField, ConfigurationIssueCode.DuplicateThreshold, "The threshold is duplicated.");
                }
            }

            if (diagnostics.Count == issueCount)
            {
                definitions.Add(new NeedDefinition(entry.Id, minimum, maximum, initial, new NeedRate(rate), thresholds));
            }
        }

        private static bool IsOutside(NeedValue value, NeedValue minimum, NeedValue maximum)
        {
            return value.CompareTo(minimum) < 0 || value.CompareTo(maximum) > 0;
        }
    }
}
