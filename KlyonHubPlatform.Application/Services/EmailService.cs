using KlyonHubPlatform.Application.Interfaces;
using System.Net;
using System.Net.Mail;
using System.Text;
using KlyonHubPlatform.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace KlyonHubPlatform.Application.Services
{
    public class EmailService : IEmailService
    {
        private readonly ISystemConfigurationService _systemConfigService;
        private readonly ILogger<EmailService> _logger;

        public EmailService(ISystemConfigurationService systemConfigService, ILogger<EmailService> logger)
        {
            _systemConfigService = systemConfigService;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true)
        {
            try
            {
                var smtpServer = await _systemConfigService.GetValueAsync("Email.SmtpServer");
                var smtpPortStr = await _systemConfigService.GetValueAsync("Email.SmtpPort") ?? "587";
                var smtpUsername = await _systemConfigService.GetValueAsync("Email.SmtpUsername");
                var smtpPassword = await _systemConfigService.GetValueAsync("Email.SmtpPassword");
                var fromEmail = await _systemConfigService.GetValueAsync("Email.FromEmail");
                var fromName = await _systemConfigService.GetValueAsync("Email.FromName");
                var enableSslStr = await _systemConfigService.GetValueAsync("Email.EnableSsl") ?? "true";
                var replyToEmail = await _systemConfigService.GetValueAsync("Email.ReplyToEmail");

                if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
                {
                    _logger.LogWarning("Configurações de email não encontradas. Configure em Configurações do Sistema > Email.*");
                    return false;
                }

                if (!int.TryParse(smtpPortStr, out var smtpPort))
                    smtpPort = 587;

                var enableSsl = enableSslStr.ToLower() == "true";

                using (var client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.EnableSsl = enableSsl;
                    client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);

                    // Usar o email do domínio como remetente, mas autenticar com Gmail
                    var senderEmail = !string.IsNullOrEmpty(fromEmail) ? fromEmail : smtpUsername;
                    var senderName = !string.IsNullOrEmpty(fromName) ? fromName : "Klyon Endereço Fiscal";

                    var message = new MailMessage
                    {
                        From = new MailAddress(senderEmail, senderName),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = isHtml,
                        BodyEncoding = Encoding.UTF8,
                        SubjectEncoding = Encoding.UTF8
                    };

                    // Adicionar Reply-To se configurado
                    if (!string.IsNullOrEmpty(replyToEmail))
                    {
                        message.ReplyToList.Add(new MailAddress(replyToEmail));
                    }

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
            var baseUrl = await _systemConfigService.GetValueAsync("AppSettings.BaseUrl") ?? "https://localhost:5001";
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
        .button {{ display: inline-block; padding: 12px 24px; background-color: #1a4d7a; color: white !important; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
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
            var baseUrl = await _systemConfigService.GetValueAsync("AppSettings.BaseUrl") ?? "https://localhost:5001";
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
        .button {{ display: inline-block; padding: 12px 24px; background-color: #1a4d7a; color: white !important; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
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

        public async Task<bool> SendContractExpiryNotificationAsync(string to, string clientName, string contractNumber, DateTime expiryDate, int daysRemaining)
        {
            var baseUrl = await _systemConfigService.GetValueAsync("AppSettings.BaseUrl") ?? "https://localhost:5001";
            var paymentsUrl = $"{baseUrl}/Client/MyPayments";

            var subject = $"⚠️ Seu contrato de endereço fiscal vence em {daysRemaining} dia(s) - {contractNumber}";
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
        .button {{ display: inline-block; padding: 12px 24px; background-color: #1a4d7a; color: white !important; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
        .warning {{ background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 15px 0; }}
        .danger {{ background-color: #f8d7da; border-left: 4px solid #dc3545; padding: 15px; margin: 15px 0; }}
        .info-box {{ background-color: #d1ecf1; border-left: 4px solid #0c5460; padding: 15px; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Klyon Endereço Fiscal</h1>
        </div>
        <div class='content'>
            <h2>Olá, {clientName}!</h2>
            <div class='{(daysRemaining <= 3 ? "danger" : "warning")}'>
                <h3 style='margin-top: 0;'>⚠️ Atenção: Contrato Próximo do Vencimento</h3>
                <p><strong>Contrato:</strong> {contractNumber}</p>
                <p><strong>Data de Vencimento:</strong> {expiryDate:dd/MM/yyyy}</p>
                <p><strong>Dias Restantes:</strong> {daysRemaining} dia(s)</p>
            </div>
            <p>Seu contrato de endereço fiscal está próximo do vencimento. Para continuar utilizando o serviço, é necessário realizar o pagamento da renovação.</p>
            <div class='info-box'>
                <strong>📋 O que acontece se não renovar?</strong>
                <ul>
                    <li>O contrato será automaticamente revogado após o vencimento</li>
                    <li>Você perderá o direito de uso do endereço fiscal</li>
                    <li>Será necessário realizar um novo cadastro para continuar</li>
                </ul>
            </div>
            <p style='text-align: center;'>
                <a href='{paymentsUrl}' class='button'>Realizar Pagamento</a>
            </p>
            <p>Se você já realizou o pagamento, por favor ignore este email. Caso contrário, acesse sua área de cobranças para realizar o pagamento da renovação.</p>
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
