namespace KlyonHubPlatform.Domain.Core.Enums
{
    public enum EmailConfigKey
    {
        SmtpServer,
        SmtpPort,
        SmtpUsername,
        SmtpPassword,
        FromEmail,
        FromName,
        EnableSsl,
        ReplyToEmail
    }
    public static class EmailConfigKeyExtensions
    {
        private static readonly IReadOnlyDictionary<EmailConfigKey, string> Keys = new Dictionary<EmailConfigKey, string>
        {
            { EmailConfigKey.SmtpServer, "Email.SmtpServer" },
            { EmailConfigKey.SmtpPort, "Email.SmtpPort" },
            { EmailConfigKey.SmtpUsername, "Email.SmtpUsername" },
            { EmailConfigKey.SmtpPassword, "Email.SmtpPassword" },
            { EmailConfigKey.FromEmail, "Email.FromEmail" },
            { EmailConfigKey.FromName, "Email.FromName" },
            { EmailConfigKey.EnableSsl, "Email.EnableSsl" },
            { EmailConfigKey.ReplyToEmail, "Email.ReplyToEmail" }
        };
        public static string ToConfigKey(this EmailConfigKey key) => Keys[key];
    }
}