namespace KlyonHubPlatform.Domain.Core.Enums
{
    public enum D4SignEmbedMethodAuth
    {
        Email,
        Sms
    }
    public static class D4SignEmbedMethodAuthExtensions
    {
        public static string ToApiValue(this D4SignEmbedMethodAuth value)
        {
            return value switch
            {
                D4SignEmbedMethodAuth.Email => "email",
                D4SignEmbedMethodAuth.Sms => "sms",
                _ => "email"
            };
        }
    }
}