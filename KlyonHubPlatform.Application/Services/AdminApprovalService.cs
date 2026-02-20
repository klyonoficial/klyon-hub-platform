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

        public AdminApprovalService(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        private static DateTime NowUtc => DateTime.UtcNow;

        public async Task<AdminCommandResult> ApproveCapacityChangeAsync(int requestId, int userId, CancellationToken cancellationToken = default)
        {
            var request = await _context.CapacityChangeRequests
                .Include(r => r.CoworkingAddress)
                .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
            if (request == null)
                return Fail(AdminResultMessages.CapacityChangeRequestNotFound, redirectToDashboard: true);
            if (request.Status != CapacityApprovalStatus.Pending)
                return Fail(AdminResultMessages.RequestAlreadyProcessed, redirectCoworkingId: request.CoworkingAddress.CoworkingId);

            var address = request.CoworkingAddress;
            var now = NowUtc;
            address.ApprovedFiscalCapacity = request.RequestedCapacity;
            address.RequestedFiscalCapacity = request.RequestedCapacity;
            address.CapacityApprovalStatus = CapacityApprovalStatus.Approved;
            address.UpdatedAt = now;
            request.Status = CapacityApprovalStatus.Approved;
            request.ReviewedBy = userId;
            request.ReviewedAt = now;
            request.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogActionAsync(userId, "CapacityChangeApproved", "CapacityChangeRequest", requestId,
                $"{{\"PreviousCapacity\":{request.PreviousCapacity},\"RequestedCapacity\":{request.RequestedCapacity},\"Status\":\"Pending\"}}",
                $"{{\"ApprovedCapacity\":{request.RequestedCapacity},\"Status\":\"Approved\"}}", cancellationToken: cancellationToken);

            return Ok(AdminResultMessages.FiscalCapacityApproved(request.RequestedCapacity), address.CoworkingId);
        }

        public async Task<AdminCommandResult> RejectCapacityChangeAsync(int requestId, string rejectionReason, int userId, CancellationToken cancellationToken = default)
        {
            var request = await _context.CapacityChangeRequests
                .Include(r => r.CoworkingAddress)
                .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
            if (request == null)
                return Fail(AdminResultMessages.CapacityChangeRequestNotFound, redirectToDashboard: true);
            if (request.Status != CapacityApprovalStatus.Pending)
                return Fail(AdminResultMessages.RequestAlreadyProcessed, redirectCoworkingId: request.CoworkingAddress.CoworkingId);

            var address = request.CoworkingAddress;
            var now = NowUtc;
            address.CapacityApprovalStatus = CapacityApprovalStatus.Rejected;
            address.RequestedFiscalCapacity = request.PreviousCapacity;
            address.UpdatedAt = now;
            request.Status = CapacityApprovalStatus.Rejected;
            request.RejectionReason = rejectionReason.Trim();
            request.ReviewedBy = userId;
            request.ReviewedAt = now;
            request.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogActionAsync(userId, "CapacityChangeRejected", "CapacityChangeRequest", requestId,
                $"{{\"PreviousCapacity\":{request.PreviousCapacity},\"RequestedCapacity\":{request.RequestedCapacity},\"Status\":\"Pending\"}}",
                $"{{\"Status\":\"Rejected\",\"RejectionReason\":{System.Text.Json.JsonSerializer.Serialize(rejectionReason)}}}", cancellationToken: cancellationToken);

            return Ok(AdminResultMessages.CapacityChangeRejected, address.CoworkingId);
        }

        public async Task<AdminCommandResult> ApproveMaxContractsChangeAsync(int requestId, int userId, CancellationToken cancellationToken = default)
        {
            var request = await _context.MaxContractsChangeRequests
                .Include(r => r.CoworkingAddress)
                .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
            if (request == null)
                return Fail(AdminResultMessages.MaxQuantityChangeRequestNotFound, redirectToDashboard: true);
            if (request.Status != CapacityApprovalStatus.Pending)
                return Fail(AdminResultMessages.RequestAlreadyProcessed, redirectCoworkingId: request.CoworkingAddress.CoworkingId);

            var address = request.CoworkingAddress;
            var now = NowUtc;
            address.MaxContracts = request.RequestedMaxContracts;
            address.MaxContractsApprovalStatus = CapacityApprovalStatus.Approved;
            address.UpdatedAt = now;
            request.Status = CapacityApprovalStatus.Approved;
            request.ReviewedBy = userId;
            request.ReviewedAt = now;
            request.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogActionAsync(userId, "MaxContractsChangeApproved", "MaxContractsChangeRequest", requestId,
                $"{{\"PreviousMaxContracts\":{request.PreviousMaxContracts},\"RequestedMaxContracts\":{request.RequestedMaxContracts},\"Status\":\"Pending\"}}",
                $"{{\"ApprovedMaxContracts\":{request.RequestedMaxContracts},\"Status\":\"Approved\"}}", cancellationToken: cancellationToken);

            return Ok(AdminResultMessages.MaxContractsApproved(request.RequestedMaxContracts), address.CoworkingId);
        }

        public async Task<AdminCommandResult> RejectMaxContractsChangeAsync(int requestId, string rejectionReason, int userId, CancellationToken cancellationToken = default)
        {
            var request = await _context.MaxContractsChangeRequests
                .Include(r => r.CoworkingAddress)
                .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
            if (request == null)
                return Fail(AdminResultMessages.MaxQuantityChangeRequestNotFound, redirectToDashboard: true);
            if (request.Status != CapacityApprovalStatus.Pending)
                return Fail(AdminResultMessages.RequestAlreadyProcessed, redirectCoworkingId: request.CoworkingAddress.CoworkingId);

            var address = request.CoworkingAddress;
            var now = NowUtc;
            address.MaxContractsApprovalStatus = CapacityApprovalStatus.Rejected;
            address.UpdatedAt = now;
            request.Status = CapacityApprovalStatus.Rejected;
            request.RejectionReason = rejectionReason.Trim();
            request.ReviewedBy = userId;
            request.ReviewedAt = now;
            request.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogActionAsync(userId, "MaxContractsChangeRejected", "MaxContractsChangeRequest", requestId,
                $"{{\"PreviousMaxContracts\":{request.PreviousMaxContracts},\"RequestedMaxContracts\":{request.RequestedMaxContracts},\"Status\":\"Pending\"}}",
                $"{{\"Status\":\"Rejected\",\"RejectionReason\":{System.Text.Json.JsonSerializer.Serialize(rejectionReason)}}}", cancellationToken: cancellationToken);

            return Ok(AdminResultMessages.MaxQuantityChangeRejected, address.CoworkingId);
        }

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
            address.CardPrice1x = request.RequestedCardPrice1x;
            address.CardPriceMonthly = request.RequestedCardPriceMonthly;
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
                $"{{\"CardPrice1x\":{request.RequestedCardPrice1x?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"},\"CardPriceMonthly\":{request.RequestedCardPriceMonthly?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"},\"CardPrice12x\":{request.RequestedCardPrice12x?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"},\"Status\":\"Approved\"}}", cancellationToken: cancellationToken);

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
