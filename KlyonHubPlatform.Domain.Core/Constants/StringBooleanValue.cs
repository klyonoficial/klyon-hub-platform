namespace KlyonHubPlatform.Domain.Core.Constants
{
    public static class StringBooleanValue
    {
        public const string True = "true";
        public const string False = "false";
        public static bool IsTrue(string? value) =>
            !string.IsNullOrWhiteSpace(value) &&
            string.Equals(value.Trim(), True, System.StringComparison.OrdinalIgnoreCase);
        public static bool IsFalse(string? value) =>
            !string.IsNullOrWhiteSpace(value) &&
            string.Equals(value.Trim(), False, System.StringComparison.OrdinalIgnoreCase);
    }
}