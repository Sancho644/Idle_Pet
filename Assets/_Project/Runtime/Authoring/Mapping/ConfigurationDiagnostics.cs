using System;
using System.Collections.Generic;
using System.Globalization;
using IdlePet.Domain.Common.Numerics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdlePet.Authoring.Mapping
{
    internal sealed class ConfigurationDiagnostics
    {
        private readonly List<ConfigurationIssue> _issues = new List<ConfigurationIssue>();
        private readonly Func<Object, string> _assetPath;

        internal ConfigurationDiagnostics(Func<Object, string> assetPath)
        {
            _assetPath = assetPath;
        }

        internal int Count => _issues.Count;

        internal IReadOnlyList<ConfigurationIssue> Issues => _issues.AsReadOnly();

        internal void Error(Object asset, string field, ConfigurationIssueCode code, string message)
        {
            string location = asset == null ? nameof(GameConfigSO) : _assetPath?.Invoke(asset);
            if (string.IsNullOrEmpty(location))
            {
                location = string.IsNullOrEmpty(asset.name) ? asset.GetType().Name : asset.name;
            }

            _issues.Add(new ConfigurationIssue(code, ConfigurationSeverity.Error, location, field, message));
        }

        internal bool RequireReference(ScriptableObject value, Object owner, string field,
            ConfigurationIssueCode code = ConfigurationIssueCode.MissingReference)
        {
            if (value != null)
            {
                return true;
            }

            Error(owner, field, code, "The required configuration asset is missing.");
            return false;
        }

        internal void CheckVersion(Object asset, string field, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                string[] components = value.Split('.');
                if (components.Length == 2 || components.Length == 3)
                {
                    bool valid = true;
                    foreach (string component in components)
                    {
                        if (!int.TryParse(component, NumberStyles.None, CultureInfo.InvariantCulture, out _)
                            || component.Contains("\0"))
                        {
                            valid = false;
                            break;
                        }
                    }

                    if (valid)
                    {
                        return;
                    }
                }
            }

            Error(asset, field, ConfigurationIssueCode.InvalidVersion, "Use a numeric major.minor or major.minor.patch version.");
        }

        internal bool TryReadFixed(Object asset, string field, string text, int precision, int scale, out long value)
        {
            var error = FixedPointText.Parse(text, precision, scale, out value);
            if (error == null)
            {
                return true;
            }

            string message;
            switch (error.Value)
            {
                case ConfigurationIssueCode.InvalidPrecision:
                    message = $"Use no more than {precision} fractional digits.";
                    break;
                case ConfigurationIssueCode.NumericOverflow:
                    message = "The scaled number is outside the supported integer range.";
                    break;
                default:
                    message = "Use a signed decimal number with a dot and no whitespace or grouping.";
                    break;
            }

            Error(asset, field, error.Value, message);
            return false;
        }

        internal bool TryReadNeedValue(Object asset, string field, string text, out NeedValue value)
        {
            value = default;
            if (!TryReadFixed(asset, field, text, 3, NeedValue.MilliUnitsPerUnit, out long scaled))
            {
                return false;
            }

            try
            {
                value = new NeedValue(scaled, default);
                return true;
            }
            catch (OverflowException)
            {
                Error(asset, field, ConfigurationIssueCode.NumericOverflow, "The exact need numerator overflowed.");
                return false;
            }
        }

        internal long ReadDuration(Object asset, string field, long amount, long millisecondsPerUnit)
        {
            if (amount <= 0)
            {
                Error(asset, field, ConfigurationIssueCode.InvalidDuration, "The duration must be positive.");
                return 0;
            }

            try
            {
                return checked(amount * millisecondsPerUnit);
            }
            catch (OverflowException)
            {
                Error(asset, field, ConfigurationIssueCode.NumericOverflow, "The conversion to milliseconds overflowed.");
                return 0;
            }
        }
    }
}
