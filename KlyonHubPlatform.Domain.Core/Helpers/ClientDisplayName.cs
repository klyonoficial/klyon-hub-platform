namespace KlyonHubPlatform.Domain.Core.Helpers
{
    /// <summary>
    /// Nome exibido para o cliente: nunca o prefixo do e-mail/login quando o nome real ainda não foi preenchido.
    /// </summary>
    public static class ClientDisplayName
    {
        public static string? EmailLocalPart(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;
            var trimmed = email.Trim();
            var at = trimmed.IndexOf('@');
            return at > 0 ? trimmed[..at] : trimmed;
        }

        public static bool IsPlaceholderName(string? name, string? email)
        {
            if (string.IsNullOrWhiteSpace(name))
                return true;

            var trimmed = name.Trim();
            if (string.IsNullOrWhiteSpace(email))
                return false;

            var emailTrim = email.Trim();
            if (string.Equals(trimmed, emailTrim, StringComparison.OrdinalIgnoreCase))
                return true;

            var local = EmailLocalPart(emailTrim);
            if (string.IsNullOrEmpty(local))
                return false;

            if (string.Equals(trimmed, local, StringComparison.OrdinalIgnoreCase))
                return true;

            if (trimmed.Contains(' ', StringComparison.Ordinal))
                return false;

            if (local.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                var extra = local[trimmed.Length..];
                return extra.Length > 0 && extra.All(char.IsDigit);
            }

            if (trimmed.StartsWith(local, StringComparison.OrdinalIgnoreCase))
            {
                var extra = trimmed[local.Length..];
                return extra.Length > 0 && extra.All(char.IsDigit);
            }

            return false;
        }

        public static string? NullIfPlaceholder(string? name, string? email) =>
            IsPlaceholderName(name, email) ? null : name?.Trim();

        /// <summary>
        /// Nome real quando existir; caso contrário o e-mail completo (nunca o login truncado).
        /// </summary>
        public static string Resolve(string? name, string? email)
        {
            var real = NullIfPlaceholder(name, email);
            if (!string.IsNullOrWhiteSpace(real))
                return real;
            return string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim();
        }
    }
}
