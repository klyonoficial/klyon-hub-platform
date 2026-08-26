using KlyonHubPlatform.Application.Commands;
using KlyonHubPlatform.Application.Constants;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Application.Services
{
    public class AdminApprovalService : IAdminApprovalService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IAddressPricingService _addressPricingService;

        public AdminApprovalService(
            ApplicationDbContext context,
            IAuditService auditService,
            IAddressPricingService addressPricingService)
        {
            _context = context;
            _auditService = auditService;
            _addressPricingService = addressPricingService;
        }

        private static DateTime NowUtc => DateTime.UtcNow;

        public async Task<AdminCommandResult> ApproveCardPriceChangeAsync(int requestId, int userId, CancellationToken cancellationToken = default)
        {
            var request = await _context.CardPriceChangeRequests
                .Include(r => r.CoworkingAddress)
                .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
            if (request == null)
                return Fail(AdminResultMessages.CardPriceChangeRequestNotFound, redirectToDashboard: true);
            if (request.Status != CapacityApprovalStatus.Pending)
                return Fail(AdminResultMessages.RequestAlreadyProcessed, redirectCoworkingId: request.CoworkingAddress.CoworkingId);

            var address = request.CoworkingAddress;
            var now = NowUtc;

            if (request.RequestedBasePrice.HasValue)
                address.BasePrice = request.RequestedBasePrice.Value;

            if (request.RequestedCardMonthlySurchargePercent.HasValue)
                address.CardMonthlySurchargePercent = request.RequestedCardMonthlySurchargePercent.Value;

            _addressPricingService.SyncDerivedCardPrices(address);

            // Mantém compatibilidade com solicitações legadas que ainda enviam preços de cartão explícitos.
            if (request.RequestedCardPrice12x.HasValue)
                address.CardPrice12x = request.RequestedCardPrice12x;

            address.CardPricesApprovalStatus = CapacityApprovalStatus.Approved;
            address.UpdatedAt = now;
            request.Status = CapacityApprovalStatus.Approved;
            request.ReviewedBy = userId;
            request.ReviewedAt = now;
            request.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogActionAsync(userId, "CardPriceChangeApproved", "CardPriceChangeRequest", requestId,
                $"{{\"Status\":\"Pending\"}}",
                $"{{\"BasePrice\":{request.RequestedBasePrice?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"},\"CardMonthlySurchargePercent\":{request.RequestedCardMonthlySurchargePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"},\"CardPrice1x\":{address.CardPrice1x?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"},\"CardPriceMonthly\":{address.CardPriceMonthly?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"},\"Status\":\"Approved\"}}", cancellationToken: cancellationToken);

            var newValuesCoworking = $"{{\"BasePrice\":{address.BasePrice.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"CardMonthlySurchargePercent\":{address.CardMonthlySurchargePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"},\"CardPrice1x\":{address.CardPrice1x?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"},\"CardPriceMonthly\":{address.CardPriceMonthly?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"},\"Status\":\"Approved\"}}";
            await _auditService.LogActionAsync(userId, "CardPricesApproved", "Coworking", address.CoworkingId, "{\"Status\":\"Pending\"}", newValuesCoworking, cancellationToken: cancellationToken);

            var result = Ok(AdminResultMessages.CardPricesApprovedSuccess, address.CoworkingId);
            result.SuccessMessage = AdminResultMessages.PrecosNoCartaoAprovados;
            return result;
        }

        public async Task<AdminCommandResult> RejectCardPriceChangeAsync(int requestId, string rejectionReason, int userId, CancellationToken cancellationToken = default)
        {
            var request = await _context.CardPriceChangeRequests
                .Include(r => r.CoworkingAddress)
                .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
            if (request == null)
                return Fail(AdminResultMessages.SolicitacaoAlteracaoPrecosCartaoNaoEncontrada, redirectToDashboard: true);
            if (request.Status != CapacityApprovalStatus.Pending)
                return Fail(AdminResultMessages.EstaSolicitacaoJaFoiProcessada, redirectCoworkingId: request.CoworkingAddress.CoworkingId);

            var address = request.CoworkingAddress;
            var now = NowUtc;
            address.CardPricesApprovalStatus = CapacityApprovalStatus.Rejected;
            address.UpdatedAt = now;
            request.Status = CapacityApprovalStatus.Rejected;
            request.RejectionReason = rejectionReason.Trim();
            request.ReviewedBy = userId;
            request.ReviewedAt = now;
            request.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogActionAsync(userId, "CardPriceChangeRejected", "CardPriceChangeRequest", requestId,
                $"{{\"Status\":\"Pending\"}}",
                $"{{\"Status\":\"Rejected\",\"RejectionReason\":{System.Text.Json.JsonSerializer.Serialize(rejectionReason)}}}", cancellationToken: cancellationToken);

            var rejectedNewValues = $"{{\"Status\":\"Rejected\",\"RejectionReason\":{System.Text.Json.JsonSerializer.Serialize(rejectionReason)}}}";
            await _auditService.LogActionAsync(userId, "CardPricesRejected", "Coworking", address.CoworkingId, "{\"Status\":\"Pending\"}", rejectedNewValues, cancellationToken: cancellationToken);

            var resultReject = Ok(AdminResultMessages.CardPricesRejected, address.CoworkingId);
            resultReject.SuccessMessage = AdminResultMessages.PrecosNoCartaoReprovados;
            return resultReject;
        }

        private static AdminCommandResult Ok(string successMessage, int coworkingId) => new AdminCommandResult
        {
            Success = true,
            SuccessMessage = successMessage,
            RedirectCoworkingId = coworkingId
        };

        private static AdminCommandResult Fail(string errorMessage, int? redirectCoworkingId = null, bool redirectToDashboard = false) => new AdminCommandResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            RedirectCoworkingId = redirectCoworkingId,
            RedirectToDashboard = redirectToDashboard
        };
    }
}
