using KlyonHubPlatform.Application.Interfaces;
using System.Net;
using System.Net.Mail;
using System.Text;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using Microsoft.Extensions.Logging;
namespace KlyonHubPlatform.Application.Services
{
    public class EmailService : IEmailService
    {
        private readonly ISystemConfigurationService _systemConfigService;
        private readonly IAppBaseUrlProvider _appBaseUrlProvider;
        private readonly ILogger<EmailService> _logger;
        public EmailService(ISystemConfigurationService systemConfigService, IAppBaseUrlProvider appBaseUrlProvider, ILogger<EmailService> logger)
        {
            _systemConfigService = systemConfigService;
            _appBaseUrlProvider = appBaseUrlProvider;
            _logger = logger;
        }
        public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true)
        {
            try
            {
                var smtpServer = await _systemConfigService.GetValueAsync(EmailConfigKey.SmtpServer.ToConfigKey());
                var smtpPortStr = await _systemConfigService.GetValueAsync(EmailConfigKey.SmtpPort.ToConfigKey()) ?? "587";
                var smtpUsername = await _systemConfigService.GetValueAsync(EmailConfigKey.SmtpUsername.ToConfigKey());
                var smtpPassword = await _systemConfigService.GetValueAsync(EmailConfigKey.SmtpPassword.ToConfigKey());
                var fromEmail = await _systemConfigService.GetValueAsync(EmailConfigKey.FromEmail.ToConfigKey());
                var fromName = await _systemConfigService.GetValueAsync(EmailConfigKey.FromName.ToConfigKey());
                var enableSslStr = await _systemConfigService.GetValueAsync(EmailConfigKey.EnableSsl.ToConfigKey()) ?? KlyonHubPlatform.Domain.Core.Constants.StringBooleanValue.True;
                var replyToEmail = await _systemConfigService.GetValueAsync(EmailConfigKey.ReplyToEmail.ToConfigKey());
                if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
                {
                    _logger.LogWarning("Configurações de email não encontradas. Configure em Configurações do Sistema > Email.*");
                    return false;
                }
                if (!int.TryParse(smtpPortStr, out var smtpPort))
                    smtpPort = 587;
                var enableSsl = KlyonHubPlatform.Domain.Core.Constants.StringBooleanValue.IsTrue(enableSslStr);
                using (var client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.EnableSsl = enableSsl;
                    client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
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
        public async Task<bool> SendVerificationEmailAsync(string to, string name, string token, string? returnUrl = null)
        {
            var baseUrl = await _appBaseUrlProvider.GetBaseUrlAsync();
            var verificationUrl = $"{baseUrl}/Auth/VerifyEmail?token={Uri.EscapeDataString(token)}";
            if (!string.IsNullOrEmpty(returnUrl))
                verificationUrl += "&returnUrl=" + Uri.EscapeDataString(returnUrl);
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
            var baseUrl = await _appBaseUrlProvider.GetBaseUrlAsync();
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
        public async Task<bool> SendClientWelcomeEmailAsync(string to, string name, string token, string serviceDescription = "Klyon")
        {
            var baseUrl = await _appBaseUrlProvider.GetBaseUrlAsync();
            var definePasswordUrl = $"{baseUrl}/Auth/ResetPassword?token={token}";
            var forgotUrl = $"{baseUrl}/Auth/ForgotPassword";
            var subject = $"Bem-vindo à Klyon! Defina sua senha de acesso";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #051323; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .header h1 {{ margin: 0; font-size: 22px; }}
        .content {{ padding: 28px; background-color: #f9f9f9; }}
        .button {{ display: inline-block; padding: 14px 28px; background-color: #1a4d7a; color: white !important; text-decoration: none; border-radius: 6px; margin: 20px 0; font-weight: bold; font-size: 15px; }}
        .info-box {{ background-color: #e8f4fd; border-left: 4px solid #1a4d7a; padding: 14px; margin: 16px 0; border-radius: 0 6px 6px 0; }}
        .warning {{ background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 12px; margin: 16px 0; border-radius: 0 6px 6px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #888; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Klyon — {serviceDescription}</h1>
        </div>
        <div class='content'>
            <h2>Olá, {name}!</h2>
            <p>Seu cadastro de <strong>{serviceDescription}</strong> foi criado com sucesso na plataforma Klyon.</p>
            <p>Para acessar sua conta e acompanhar seus contratos e pagamentos, você precisa <strong>definir sua senha de acesso</strong>. Clique no botão abaixo:</p>
            <p style='text-align: center;'>
                <a href='{definePasswordUrl}' class='button'>Definir minha senha</a>
            </p>
            <div class='warning'>
                <strong>⚠️ Atenção:</strong> Este link expira em <strong>72 horas</strong>. Defina sua senha o quanto antes.
            </div>
            <div class='info-box'>
                <strong>Como recuperar o acesso depois:</strong><br>
                Caso precise redefinir sua senha no futuro, acesse:<br>
                <a href='{forgotUrl}' style='color:#1a4d7a;'>{forgotUrl}</a><br>
                Você precisará informar o seu <strong>e-mail</strong> cadastrado e o seu <strong>CPF ou CNPJ</strong>.
            </div>
            <div class='info-box' style='margin-top:20px;'>
                <strong>💬 Ficou com dúvidas?</strong><br>
                Fale com a nossa equipe pelo WhatsApp:<br>
                <a href='https://wa.me/5527989030369' style='color:#1a4d7a; font-weight:bold;'>+55 (27) 98903-0369</a>
            </div>
            <p>Atenciosamente,<br><strong>Equipe Klyon</strong></p>
        </div>
        <div class='footer'>
            <p>Este é um e-mail automático, por favor não responda.</p>
            <p>© {DateTime.Now.Year} Klyon. Todos os direitos reservados.</p>
        </div>
    </div>
</body>
</html>";
            return await SendEmailAsync(to, subject, body, true);
        }

        public async Task<bool> SendContractExpiryNotificationAsync(string to, string clientName, string contractNumber, DateTime expiryDate, int daysRemaining)
        {
            var baseUrl = await _appBaseUrlProvider.GetBaseUrlAsync();
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
            <div class='{(daysRemaining <= 3 ? BootstrapBadgeCss.Danger : BootstrapBadgeCss.Warning)}'>
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
        public async Task<bool> SendReservationRequestToCoworkingAsync(string to, string coworkingName, string clientName, IEnumerable<(DateTime Date, TimeSpan? Start, TimeSpan? End, string RoomType)> items)
        {
            var baseUrl = await _appBaseUrlProvider.GetBaseUrlAsync();
            var reservationsUrl = $"{baseUrl}/Coworking/Reservations?status=Pending";
            string RoomTypeDisplay(string rt) => rt == "Shared" ? "Sala compartilhada" : rt == "Private" ? "Sala privativa" : rt == "Auditorium" ? "Auditório" : rt;
            var listHtml = string.Join("", items.Select(x =>
            {
                var hi = x.Start.HasValue ? x.Start.Value.ToString(@"hh\:mm") : "-";
                var he = x.End.HasValue ? x.End.Value.ToString(@"hh\:mm") : "-";
                return $"<li><strong>{x.Date:dd/MM/yyyy}</strong> das {hi} às {he} — {RoomTypeDisplay(x.RoomType)}</li>";
            }));
            var subject = "Solicitação de reserva de sala — aprovar ou reprovar";
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
        ul {{ margin: 10px 0; padding-left: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Klyon — Reserva de Salas</h1>
        </div>
        <div class='content'>
            <h2>Olá, {coworkingName}!</h2>
            <p>O cliente <strong>{clientName}</strong> fez uma solicitação de reserva de sala(s):</p>
            <ul>{listHtml}</ul>
            <p>É necessário <strong>aprovar ou reprovar</strong> cada solicitação. Acesse a área de reservas e responda em até <strong>24 horas</strong>.</p>
            <div class='warning'>
                <strong>⚠️ Atenção:</strong> Caso não haja resposta em 24 horas, as solicitações serão canceladas automaticamente.
            </div>
            <p style='text-align: center;'>
                <a href='{reservationsUrl}' class='button'>Ver reservas pendentes e responder</a>
            </p>
            <p>Atenciosamente,<br>Equipe Klyon</p>
        </div>
        <div class='footer'>
            <p>Este é um email automático. Não responda.</p>
        </div>
    </div>
</body>
</html>";
            return await SendEmailAsync(to, subject, body, true);
        }
        public async Task<bool> SendReservationRequestToClientAsync(string to, string clientName, IEnumerable<(DateTime Date, TimeSpan? Start, TimeSpan? End, string RoomType)> items)
        {
            string RoomTypeDisplay(string rt) => rt == "Shared" ? "Sala compartilhada" : rt == "Private" ? "Sala privativa" : rt == "Auditorium" ? "Auditório" : rt;
            var list = string.Join("; ", items.Select(x =>
            {
                var hi = x.Start.HasValue ? x.Start.Value.ToString(@"hh\:mm") : "-";
                var he = x.End.HasValue ? x.End.Value.ToString(@"hh\:mm") : "-";
                return $"{x.Date:dd/MM/yyyy} das {hi} às {he} ({RoomTypeDisplay(x.RoomType)})";
            }));
            var subject = "Solicitação de reserva enviada";
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
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
        .info {{ background-color: #d1ecf1; border-left: 4px solid #0c5460; padding: 15px; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Klyon — Reserva de Salas</h1>
        </div>
        <div class='content'>
            <h2>Olá, {clientName}!</h2>
            <p>Sua solicitação de reserva foi enviada com sucesso.</p>
            <p><strong>Reservas solicitadas:</strong> {list}</p>
            <div class='info'>
                <strong>Próximos passos:</strong> O coworking analisará sua solicitação. Você receberá o retorno em até <strong>24 horas</strong> (aprovação ou reprovação).
            </div>
            <p>Atenciosamente,<br>Equipe Klyon</p>
        </div>
        <div class='footer'>
            <p>Este é um email automático. Não responda.</p>
        </div>
    </div>
</body>
</html>";
            return await SendEmailAsync(to, subject, body, true);
        }
        public async Task<bool> SendClientCredentialsEmailAsync(string to, string name, string email, string password, string paymentDetailsUrl)
        {
            var subject = "Bem-vindo ao Klyon Endereço Fiscal - Suas Credenciais de Acesso";
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
        .credentials-box {{ background-color: #ffffff; border: 2px solid #1a4d7a; border-radius: 5px; padding: 15px; margin: 20px 0; }}
        .warning {{ background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 15px 0; }}
        .info-box {{ background-color: #d1ecf1; border-left: 4px solid #0c5460; padding: 15px; margin: 15px 0; }}
        .credential-item {{ margin: 10px 0; }}
        .credential-label {{ font-weight: bold; color: #1a4d7a; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Klyon Endereço Fiscal</h1>
        </div>
        <div class='content'>
            <h2>Olá, {name}!</h2>
            <p>Bem-vindo à plataforma Klyon Endereço Fiscal! Sua conta foi criada com sucesso.</p>
            <div class='credentials-box'>
                <h3 style='margin-top: 0; color: #1a4d7a;'>📋 Suas Credenciais de Acesso</h3>
                <div class='credential-item'>
                    <span class='credential-label'>Email:</span> {email}
                </div>
                <div class='credential-item'>
                    <span class='credential-label'>Senha Temporária:</span> {password}
                </div>
            </div>
            <div class='warning'>
                <strong>⚠️ Importante:</strong> Por segurança, você será solicitado a alterar sua senha no primeiro acesso. A senha padrão são as 4 primeiras letras do seu email.
            </div>
            <div class='info-box'>
                <strong>💳 Próximo Passo:</strong> Seu contrato de endereço fiscal foi criado e está aguardando pagamento. Clique no botão abaixo para acessar a página de pagamento e gerar o código PIX.
            </div>
            <p style='text-align: center;'>
                <a href='{paymentDetailsUrl}' class='button'>Acessar Página de Pagamento</a>
            </p>
            <p>Ou copie e cole o link abaixo no seu navegador:</p>
            <p style='word-break: break-all; color: #1a4d7a;'>{paymentDetailsUrl}</p>
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
        public async Task<bool> SendCorrespondenceArrivalNotificationAsync(string to, string clientName, string addressInfo, DateTime arrivalDate, string schedulePickupUrl)
        {
            var subject = "Sua correspondência chegou - Agende a retirada - Klyon Endereço Fiscal";
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
            <p>Informamos que <strong>sua correspondência chegou</strong> ao endereço fiscal.</p>
            <div class='info-box'>
                <p style='margin: 0;'><strong>Endereço:</strong> {addressInfo}</p>
                <p style='margin: 10px 0 0 0;'><strong>Data de chegada:</strong> {arrivalDate:dd/MM/yyyy}</p>
            </div>
            <p>Para retirar, é necessário <strong>agendar uma data e hora</strong>. Clique no botão abaixo para escolher quando deseja buscar sua correspondência.</p>
            <p style='text-align: center;'>
                <a href='{schedulePickupUrl}' class='button'>Agendar data e hora para retirada</a>
            </p>
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