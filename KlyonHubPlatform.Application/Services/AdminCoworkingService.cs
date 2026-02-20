using KlyonHubPlatform.Application.Commands;
using KlyonHubPlatform.Application.Constants;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using static KlyonHubPlatform.Domain.Core.Constants.ServiceCodes;

namespace KlyonHubPlatform.Application.Services
{
    public class AdminCoworkingService : IAdminCoworkingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public AdminCoworkingService(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<AdminCommandResult> UpdateCoworkingAsync(int id, UpdateCoworkingCommand command, CancellationToken cancellationToken = default)
        {
            var coworking = await _context.Coworkings.FindAsync(new object[] { id }, cancellationToken);
            if (coworking == null)
                return new AdminCommandResult { Success = false, ErrorMessage = AdminResultMessages.CoworkingNotFound, RedirectToDashboard = true };

            coworking.Name = command.Name;
            coworking.CNPJ = command.CNPJ;
            coworking.TradeName = command.TradeName;
            coworking.ContactEmail = command.ContactEmail;
            coworking.ContactPhone = command.ContactPhone;
            coworking.SecondaryEmail = command.SecondaryEmail;
            coworking.SecondaryPhone = command.SecondaryPhone;
            coworking.BankName = command.BankName;
            coworking.BankAgency = command.BankAgency;
            coworking.BankAccount = command.BankAccount;
            coworking.BankAccountType = command.BankAccountType;
            coworking.BankPixKey = command.BankPixKey;
            coworking.Description = command.Description;
            coworking.Website = command.Website;
            coworking.Instagram = command.Instagram;
            coworking.Facebook = command.Facebook;
            coworking.LinkedIn = command.LinkedIn;
            coworking.Address = command.Address;
            coworking.AddressNumber = command.AddressNumber;
            coworking.AddressComplement = command.AddressComplement;
            coworking.AddressNeighborhood = command.AddressNeighborhood;
            coworking.AddressCity = command.AddressCity;
            coworking.AddressState = command.AddressState;
            coworking.AddressZipCode = command.AddressZipCode;
            coworking.IsActive = command.IsActive;
            coworking.IsPartner = command.IsPartner;
            coworking.ApprovalStatus = command.ApprovalStatus;
            coworking.RejectionReason = command.RejectionReason;
            coworking.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return new AdminCommandResult
            {
                Success = true,
                SuccessMessage = AdminResultMessages.CoworkingUpdated(coworking.Name),
                RedirectCoworkingId = id
            };
        }

        public async Task<AdminCommandResult> ApproveCoworkingAsync(int id, int userId, CancellationToken cancellationToken = default)
        {
            var coworking = await _context.Coworkings.FindAsync(new object[] { id }, cancellationToken);
            if (coworking == null)
                return new AdminCommandResult { Success = false, ErrorMessage = AdminResultMessages.CoworkingNotFound, RedirectToDashboard = true };

            var fiscalAddressService = await _context.Services
                .FirstOrDefaultAsync(s => s.Code == FiscalAddressOffer && s.IsActive, cancellationToken);
            if (fiscalAddressService != null)
            {
                var partnerService = await _context.PartnerServices
                    .FirstOrDefaultAsync(ps => ps.CoworkingId == coworking.Id && ps.ServiceId == fiscalAddressService.Id && ps.Percentage > 0, cancellationToken);
                if (partnerService == null)
                {
                    // Aviso: aprovado sem percentual de endereço fiscal - não falhamos, apenas registramos
                }
            }

            var oldStatus = coworking.ApprovalStatus;
            coworking.ApprovalStatus = CoworkingApprovalStatus.Approved;
            coworking.ApprovedAt = DateTime.UtcNow;
            coworking.ApprovedBy = userId;
            coworking.RejectionReason = null;
            coworking.RejectedAt = null;
            coworking.RejectedBy = null;
            coworking.UpdatedAt = DateTime.UtcNow;
            coworking.HasPendingStatusNotification = true;
            coworking.LastNotifiedStatus = CoworkingApprovalStatus.Approved;

            var pendingAddresses = await _context.CoworkingAddresses
                .Where(a => a.CoworkingId == coworking.Id && a.AddressStatus == AddressStatus.PendingApproval)
                .ToListAsync(cancellationToken);
            foreach (var address in pendingAddresses)
            {
                address.AddressStatus = AddressStatus.Active;
                address.IsAvailable = true;
                address.UpdatedAt = DateTime.UtcNow;
                await _auditService.LogActionAsync(userId, "AddressStatusChanged", "CoworkingAddress", address.Id,
                    "{\"AddressStatus\":\"PendingApproval\"}", "{\"AddressStatus\":\"Active\",\"Reason\":\"Aprovado automaticamente ao aprovar coworking\"}", cancellationToken: cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogActionAsync(userId, "ApprovalStatusChange", "Coworking", id, "{\"ApprovalStatus\":\"" + oldStatus + "\"}", "{\"ApprovalStatus\":\"Approved\"}", cancellationToken: cancellationToken);

            return new AdminCommandResult
            {
                Success = true,
                SuccessMessage = AdminResultMessages.CoworkingApproved(coworking.Name),
                RedirectCoworkingId = id
            };
        }

        public async Task<AdminCommandResult> RejectCoworkingAsync(int id, string rejectionReason, int userId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
                return new AdminCommandResult { Success = false, ErrorMessage = AdminResultMessages.InformRejectionReason, RedirectCoworkingId = id };

            var coworking = await _context.Coworkings.FindAsync(new object[] { id }, cancellationToken);
            if (coworking == null)
                return new AdminCommandResult { Success = false, ErrorMessage = AdminResultMessages.CoworkingNotFound, RedirectToDashboard = true };

            var oldStatus = coworking.ApprovalStatus;
            coworking.ApprovalStatus = CoworkingApprovalStatus.AwaitingAdjustment;
            coworking.RejectionReason = rejectionReason.Trim();
            coworking.RejectedAt = DateTime.UtcNow;
            coworking.RejectedBy = userId;
            coworking.UpdatedAt = DateTime.UtcNow;
            coworking.HasPendingStatusNotification = true;
            coworking.LastNotifiedStatus = CoworkingApprovalStatus.AwaitingAdjustment;

            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogActionAsync(userId, "ApprovalStatusChange", "Coworking", id, "{\"ApprovalStatus\":\"" + oldStatus + "\"}", "{\"ApprovalStatus\":\"AwaitingAdjustment\",\"RejectionReason\":" + System.Text.Json.JsonSerializer.Serialize(coworking.RejectionReason) + "}", cancellationToken: cancellationToken);

            return new AdminCommandResult
            {
                Success = true,
                SuccessMessage = AdminResultMessages.CoworkingRejected,
                RedirectCoworkingId = id
            };
        }

        public async Task<AdminCommandResult> ApproveDocumentAsync(int documentId, int userId, CancellationToken cancellationToken = default)
        {
            var doc = await _context.CoworkingDocuments.FindAsync(new object[] { documentId }, cancellationToken);
            if (doc == null)
                return new AdminCommandResult { Success = false, ErrorMessage = AdminResultMessages.DocumentNotFound, RedirectToDashboard = true };

            doc.Status = CoworkingDocumentStatus.Approved;
            doc.RejectionReason = null;
            doc.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogActionAsync(userId, "DocumentApproved", "CoworkingDocument", documentId, "{\"Status\":\"Pending\"}", "{\"Status\":\"Approved\"}", cancellationToken: cancellationToken);

            return new AdminCommandResult
            {
                Success = true,
                SuccessMessage = AdminResultMessages.DocumentApproved,
                RedirectCoworkingId = doc.CoworkingId
            };
        }

        public async Task<AdminCommandResult> RejectDocumentAsync(int documentId, string rejectionReason, int userId, CancellationToken cancellationToken = default)
        {
            var doc = await _context.CoworkingDocuments.FindAsync(new object[] { documentId }, cancellationToken);
            if (doc == null)
                return new AdminCommandResult { Success = false, ErrorMessage = AdminResultMessages.DocumentNotFound, RedirectToDashboard = true };
            if (string.IsNullOrWhiteSpace(rejectionReason))
                return new AdminCommandResult { Success = false, ErrorMessage = AdminResultMessages.InformRejectionReason, RedirectCoworkingId = doc.CoworkingId };

            var oldStatus = doc.Status.ToString();
            doc.Status = CoworkingDocumentStatus.Rejected;
            doc.RejectionReason = rejectionReason.Trim();
            doc.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogActionAsync(userId, "DocumentRejected", "CoworkingDocument", documentId, "{\"Status\":\"" + oldStatus + "\"}", "{\"Status\":\"Rejected\",\"RejectionReason\":" + System.Text.Json.JsonSerializer.Serialize(doc.RejectionReason) + "}", cancellationToken: cancellationToken);

            return new AdminCommandResult
            {
                Success = true,
                SuccessMessage = AdminResultMessages.DocumentRejected,
                RedirectCoworkingId = doc.CoworkingId
            };
        }
    }
}
