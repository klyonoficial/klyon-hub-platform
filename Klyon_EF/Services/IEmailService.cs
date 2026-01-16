namespace KlyonEF.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true);
        Task<bool> SendVerificationEmailAsync(string to, string name, string token);
        Task<bool> SendPasswordResetEmailAsync(string to, string name, string token);
    }
}
