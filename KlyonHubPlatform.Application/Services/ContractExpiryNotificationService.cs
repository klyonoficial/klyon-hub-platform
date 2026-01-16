using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KlyonHubPlatform.Application.Services
{
    public class ContractExpiryNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<ContractExpiryNotificationService> _logger;

        public ContractExpiryNotificationService(
            ApplicationDbContext context,
            IEmailService emailService,
            ILogger<ContractExpiryNotificationService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task CheckAndSendExpiryNotificationsAsync()
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var expiryThreshold = today.AddDays(10); // 10 dias antes do vencimento

                // Buscar contratos ativos que vencem nos próximos 10 dias
                var contractsToNotify = await _context.Contracts
                    .Include(c => c.Client)
                        .ThenInclude(cl => cl.User)
                    .Where(c => c.Status == ContractStatus.Active &&
                               c.ExpiryDate.HasValue &&
                               c.ExpiryDate.Value.Date <= expiryThreshold &&
                               c.ExpiryDate.Value.Date >= today)
                    .ToListAsync();

                foreach (var contract in contractsToNotify)
                {
                    if (!contract.ExpiryDate.HasValue)
                        continue;

                    var daysRemaining = (contract.ExpiryDate.Value.Date - today).Days;

                    // Verificar se já foi enviada notificação hoje
                    var lastNotification = contract.LastExpiryNotificationSent;
                    if (lastNotification.HasValue && lastNotification.Value.Date == today)
                    {
                        continue; // Já foi enviada notificação hoje
                    }

                    // Enviar email de notificação
                    var clientEmail = contract.Client?.User?.Email;
                    if (string.IsNullOrEmpty(clientEmail))
                    {
                        _logger.LogWarning($"Contrato {contract.ContractNumber} não possui email do cliente para notificação");
                        continue;
                    }

                    var clientName = contract.Client?.RazaoSocial ?? 
                                    contract.Client?.NomeFantasia ?? 
                                    contract.Client?.ContactName ?? 
                                    "Cliente";

                    var emailSent = await _emailService.SendContractExpiryNotificationAsync(
                        clientEmail,
                        clientName,
                        contract.ContractNumber,
                        contract.ExpiryDate.Value,
                        daysRemaining
                    );

                    if (emailSent)
                    {
                        // Atualizar data da última notificação
                        contract.LastExpiryNotificationSent = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Notificação de vencimento enviada para contrato {contract.ContractNumber} - {daysRemaining} dias restantes");
                    }
                    else
                    {
                        _logger.LogWarning($"Falha ao enviar notificação de vencimento para contrato {contract.ContractNumber}");
                    }
                }

                // Verificar contratos vencidos e revogar automaticamente
                var expiredContracts = await _context.Contracts
                    .Include(c => c.Client)
                    .Where(c => c.Status == ContractStatus.Active &&
                               c.ExpiryDate.HasValue &&
                               c.ExpiryDate.Value.Date < today)
                    .ToListAsync();

                foreach (var contract in expiredContracts)
                {
                    contract.Status = ContractStatus.Cancelled;
                    contract.CancelledAt = DateTime.UtcNow;
                    contract.CancellationReason = "Contrato vencido automaticamente - não houve renovação";
                    
                    // Liberar endereço
                    if (contract.CoworkingAddress != null)
                    {
                        contract.CoworkingAddress.CurrentContracts = Math.Max(0, contract.CoworkingAddress.CurrentContracts - 1);
                        if (contract.CoworkingAddress.CurrentContracts < contract.CoworkingAddress.MaxContracts)
                        {
                            contract.CoworkingAddress.IsAvailable = true;
                        }
                    }

                    _logger.LogInformation($"Contrato {contract.ContractNumber} revogado automaticamente por vencimento");
                }

                if (expiredContracts.Any())
                {
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar e enviar notificações de vencimento de contratos");
            }
        }
    }
}
