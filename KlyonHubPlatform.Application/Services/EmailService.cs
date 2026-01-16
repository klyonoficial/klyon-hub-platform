using KlyonHubPlatform.Application.Interfaces;
using System.Net;
using System.Net.Mail;
using System.Text;
using KlyonHubPlatform.Domain.Interfaces;

namespace KlyonHubPlatform.Application.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true)
        {
            try
            {
                var emailSettings = _configuration.GetSection("EmailSettings");
                var smtpServer = emailSettings["SmtpServer"];
                var smtpPort = int.Parse(emailSettings["SmtpPort"] ?? "587");
                var smtpUsername = emailSettings["SmtpUsername"];
                var smtpPassword = emailSettings["SmtpPassword"];
                var fromEmail = emailSettings["FromEmail"];
                var fromName = emailSettings["FromName"];
                var enableSsl = bool.Parse(emailSettings["EnableSsl"] ?? "true");

                if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
                {
                    _logger.LogWarning("Configurações de email não encontradas. Email não será enviado.");
                    return false;
                }

                using (var client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.EnableSsl = enableSsl;
                    client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);

                    var message = new MailMessage
                    {
                        From = new MailAddress(fromEmail ?? smtpUsername, fromName ?? "Klyon Endereço Fiscal"),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = isHtml,
                        BodyEncoding = Encoding.UTF8,
                        SubjectEncoding = Encoding.UTF8
                    };

                    message.To.Add(to);

                    await client.SendMailAsync(message);
                    _logger.LogInformation($"Email enviado com sucesso para {to}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao enviar email para {to}");
                return false;
            }
        }

        public async Task<bool> SendVerificationEmailAsync(string to, string name, string token)
        {
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://localhost:5001";
            var verificationUrl = $"{baseUrl}/Auth/VerifyEmail?token={token}";

            var subject = "Verifique seu email - Klyon Endereço Fiscal";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #051323; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #1a4d7a; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Klyon Endereço Fiscal</h1>
        </div>
        <div class='content'>
            <h2>Olá, {name}!</h2>
            <p>Obrigado por se cadastrar na plataforma Klyon Endereço Fiscal.</p>
            <p>Para ativar sua conta, clique no botão abaixo para verificar seu endereço de email:</p>
            <p style='text-align: center;'>
                <a href='{verificationUrl}' class='button'>Verificar Email</a>
            </p>
            <p>Ou copie e cole o link abaixo no seu navegador:</p>
            <p style='word-break: break-all; color: #1a4d7a;'>{verificationUrl}</p>
            <p>Se você não criou esta conta, ignore este email.</p>
            <p>Atenciosamente,<br>Equipe Klyon</p>
        </div>
        <div class='footer'>
            <p>Este é um email automático, por favor não responda.</p>
            <p>© {DateTime.Now.Year} Klyon Endereço Fiscal. Todos os direitos reservados.</p>
        </div>
    </div>
</body>
</html>";

            return await SendEmailAsync(to, subject, body, true);
        }

        public async Task<bool> SendPasswordResetEmailAsync(string to, string name, string token)
        {
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://localhost:5001";
            var resetUrl = $"{baseUrl}/Auth/ResetPassword?token={token}";

            var subject = "Redefinição de Senha - Klyon Endereço Fiscal";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #051323; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #1a4d7a; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
        .warning {{ background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 10px; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Klyon Endereço Fiscal</h1>
        </div>
        <div class='content'>
            <h2>Olá, {name}!</h2>
            <p>Recebemos uma solicitação para redefinir a senha da sua conta.</p>
            <p>Clique no botão abaixo para criar uma nova senha:</p>
            <p style='text-align: center;'>
                <a href='{resetUrl}' class='button'>Redefinir Senha</a>
            </p>
            <p>Ou copie e cole o link abaixo no seu navegador:</p>
            <p style='word-break: break-all; color: #1a4d7a;'>{resetUrl}</p>
            <div class='warning'>
                <strong>⚠️ Importante:</strong> Este link expira em 1 hora. Se você não solicitou a redefinição de senha, ignore este email.
            </div>
            <p>Atenciosamente,<br>Equipe Klyon</p>
        </div>
        <div class='footer'>
            <p>Este é um email automático, por favor não responda.</p>
            <p>© {DateTime.Now.Year} Klyon Endereço Fiscal. Todos os direitos reservados.</p>
        </div>
    </div>
</body>
</html>";

            return await SendEmailAsync(to, subject, body, true);
        }
    }
}
