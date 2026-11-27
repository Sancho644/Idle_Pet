using System.Text.RegularExpressions;

namespace IdlePet.Domain.Definitions
{
    public static class ConfigurationId
    {
        public static bool IsValid(string value)
        {
            return value != null && Regex.IsMatch(value, @"\A[A-Za-z0-9][A-Za-z0-9._-]*\z");
        }
    }
}
