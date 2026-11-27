using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace IdlePet.Authoring.Mapping
{
    internal static class FixedPointText
    {
        private const NumberStyles Styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

        internal static ConfigurationIssueCode? Parse(string text, int precision, int scale, out long scaled)
        {
            scaled = 0;
            if (text == null || !Regex.IsMatch(text, @"\A-?[0-9]+(?:\.[0-9]+)?\z"))
            {
                return ConfigurationIssueCode.InvalidNumber;
            }

            int decimalPoint = text.IndexOf('.');
            if (decimalPoint >= 0 && text.Length - decimalPoint - 1 > precision)
            {
                return ConfigurationIssueCode.InvalidPrecision;
            }

            if (!decimal.TryParse(text, Styles, CultureInfo.InvariantCulture, out decimal number))
            {
                return ConfigurationIssueCode.NumericOverflow;
            }

            try
            {
                scaled = checked((long)(number * scale));
                return null;
            }
            catch (OverflowException)
            {
                return ConfigurationIssueCode.NumericOverflow;
            }
        }
    }
}
