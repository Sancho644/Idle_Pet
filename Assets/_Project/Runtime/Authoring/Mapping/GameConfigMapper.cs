using System;
using System.Collections.Generic;
using IdlePet.Domain.Common.Numerics;
using IdlePet.Domain.Definitions;
using IdlePet.Domain.Pet.Needs;
using Object = UnityEngine.Object;

namespace IdlePet.Authoring.Mapping
{
    public static class GameConfigMapper
    {
        public static ConfigurationMappingResult Map(GameConfigSO root, ConfigurationTarget target,
            Func<Object, string> assetPath = null)
        {
            var diagnostics = new ConfigurationDiagnostics(assetPath);
            if (!Enum.IsDefined(typeof(ConfigurationTarget), target))
            {
                diagnostics.Error(root, nameof(target), ConfigurationIssueCode.UnknownEnumValue, "Unknown configuration target.");
            }

            if (!diagnostics.RequireReference(root, null, nameof(root), ConfigurationIssueCode.MissingRoot))
            {
                return new ConfigurationMappingResult(null, diagnostics.Issues);
            }

            diagnostics.CheckVersion(root, nameof(root.RulesVersion), root.RulesVersion);
            diagnostics.CheckVersion(root, nameof(root.ContentVersion), root.ContentVersion);

            BalanceProfileSO profile = root.Profile;
            ConfigurationIssueCode missingProfile = target == ConfigurationTarget.Release
                ? ConfigurationIssueCode.MissingProductionProfile : ConfigurationIssueCode.MissingReference;
            if (!diagnostics.RequireReference(profile, root, nameof(root.Profile), missingProfile))
            {
                return new ConfigurationMappingResult(null, diagnostics.Issues);
            }

            ValidateProfile(profile, target, diagnostics);
            bool hasSimulation = diagnostics.RequireReference(profile.Simulation, profile, nameof(profile.Simulation));
            bool hasTime = diagnostics.RequireReference(profile.Time, profile, nameof(profile.Time));
            bool hasNeeds = diagnostics.RequireReference(profile.Needs, profile, nameof(profile.Needs));
            bool hasModifiers = diagnostics.RequireReference(profile.Modifiers, profile, nameof(profile.Modifiers));
            var simulation = hasSimulation ? MapSimulation(profile.Simulation, diagnostics) : null;
            var time = hasTime ? MapTime(profile.Time, diagnostics) : null;
            var needs = hasNeeds ? NeedsConfigMapper.Map(profile.Needs, diagnostics) : null;
            var modifiers = hasModifiers ? MapModifiers(profile.Modifiers, diagnostics) : null;

            if (diagnostics.Count != 0)
            {
                return new ConfigurationMappingResult(null, diagnostics.Issues);
            }

            var needsDefinition = new NeedsDefinition(root.RulesVersion, profile.BalanceVersion, root.ContentVersion, needs);
            var definition = new GameConfiguration(profile.Kind, profile.Id, simulation, time, needsDefinition, modifiers);
            return new ConfigurationMappingResult(definition, diagnostics.Issues);
        }

        private static void ValidateProfile(BalanceProfileSO profile, ConfigurationTarget target, ConfigurationDiagnostics diagnostics)
        {
            diagnostics.CheckVersion(profile, nameof(profile.BalanceVersion), profile.BalanceVersion);
            if (!ConfigurationId.IsValid(profile.Id))
            {
                diagnostics.Error(profile, nameof(profile.Id), ConfigurationIssueCode.InvalidId, "A stable profile ID is required.");
            }

            if (!Enum.IsDefined(typeof(BalanceProfileKind), profile.Kind))
            {
                diagnostics.Error(profile, nameof(profile.Kind), ConfigurationIssueCode.UnknownEnumValue, "Unknown balance profile.");
            }
            else if (target == ConfigurationTarget.Release && profile.Kind != BalanceProfileKind.Production)
            {
                diagnostics.Error(profile, nameof(profile.Kind), ConfigurationIssueCode.NonProductionProfile,
                    "The Release profile must be Production.");
            }
        }

        private static SimulationDefinition MapSimulation(SimulationConfigSO asset, ConfigurationDiagnostics diagnostics)
        {
            if (asset.MaxEventsPerTimestamp <= 0)
            {
                diagnostics.Error(asset, nameof(asset.MaxEventsPerTimestamp), ConfigurationIssueCode.InvalidEventLimit,
                    "The maximum number of events per timestamp must be positive.");
                return null;
            }

            return new SimulationDefinition(asset.MaxEventsPerTimestamp);
        }

        private static TimeDefinition MapTime(TimeConfigSO asset, ConfigurationDiagnostics diagnostics)
        {
            const long minuteMilliseconds = 60_000;
            const long hourMilliseconds = NeedRemainder.Denominator;
            const long dayMilliseconds = 24 * hourMilliseconds;

            int issueCount = diagnostics.Count;
            long visualDay = diagnostics.ReadDuration(asset, nameof(asset.VisualDayMinutes), asset.VisualDayMinutes, minuteMilliseconds);
            long biologicalDay = diagnostics.ReadDuration(asset, nameof(asset.BiologicalDayHours), asset.BiologicalDayHours, hourMilliseconds);
            long minNight = diagnostics.ReadDuration(asset, nameof(asset.MinNightHours), asset.MinNightHours, hourMilliseconds);
            long maxNight = diagnostics.ReadDuration(asset, nameof(asset.MaxNightHours), asset.MaxNightHours, hourMilliseconds);
            long changeInterval = diagnostics.ReadDuration(asset, nameof(asset.ScheduleChangeIntervalDays),
                asset.ScheduleChangeIntervalDays, dayMilliseconds);

            if (diagnostics.Count != issueCount)
            {
                return null;
            }

            if (minNight > maxNight || maxNight > biologicalDay)
            {
                diagnostics.Error(asset, nameof(asset.MaxNightHours), ConfigurationIssueCode.InvalidNightWindow,
                    "The night window must be ordered and fit within a biological day.");
                return null;
            }

            return new TimeDefinition(visualDay, biologicalDay, minNight, maxNight, changeInterval);
        }

        private static List<RateModifierDefinition> MapModifiers(ModifierConfigSO asset, ConfigurationDiagnostics diagnostics)
        {
            var modifiers = new List<RateModifierDefinition>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < asset.RateMultipliers.Count; i++)
            {
                string field = $"{nameof(asset.RateMultipliers)}[{i}]";
                RateModifierAuthoringData entry = asset.RateMultipliers[i];
                if (entry == null)
                {
                    diagnostics.Error(asset, field, ConfigurationIssueCode.MissingReference, "The modifier entry is missing.");
                    continue;
                }

                int issueCount = diagnostics.Count;
                if (!ConfigurationId.IsValid(entry.Id))
                {
                    diagnostics.Error(asset, field + ".Id", ConfigurationIssueCode.InvalidId, "A stable modifier ID is required.");
                }
                else if (!seen.Add(entry.Id))
                {
                    diagnostics.Error(asset, field + ".Id", ConfigurationIssueCode.DuplicateId, $"Duplicate modifier {entry.Id}.");
                }

                if (!Enum.IsDefined(typeof(NeedId), entry.Target))
                {
                    diagnostics.Error(asset, field + ".Target", ConfigurationIssueCode.UnknownEnumValue, "Unknown need ID.");
                }
                else if (entry.Target == NeedId.Comfort)
                {
                    diagnostics.Error(asset, field + ".Target", ConfigurationIssueCode.ComputedNeed, "Comfort has no base rate.");
                }

                bool parsed = diagnostics.TryReadFixed(asset, field + ".Coefficient", entry.Coefficient, 6,
                    NeedCoefficient.MillionthsPerUnit, out long coefficient);
                if (parsed && coefficient < 0)
                {
                    diagnostics.Error(asset, field + ".Coefficient", ConfigurationIssueCode.InvalidCoefficient,
                        "A rate multiplier must be non-negative.");
                }

                if (diagnostics.Count == issueCount)
                {
                    modifiers.Add(new RateModifierDefinition(entry.Id, entry.Target, new NeedCoefficient(coefficient)));
                }
            }

            return modifiers;
        }
    }
}
