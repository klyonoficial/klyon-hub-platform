using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using static KlyonHubPlatform.Domain.Core.Constants.ServiceCodes;

namespace KlyonHubPlatform.Application.Services
{
    public class CoworkingAppService : ICoworkingAppService
    {
        private readonly ApplicationDbContext _context;

        public CoworkingAppService(ApplicationDbContext context)
        {
            _context = context;
        }

        public ApplicationDbContext DbContext => _context;

        public async Task<List<CoworkingDocument>> GetRejectedCoworkingDocumentsAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingDocuments
                .Where(d => d.CoworkingId == coworkingId && d.Status == CoworkingDocumentStatus.Rejected)
                .OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> GetPendingCoworkingDocumentsCountAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingDocuments
                .Where(d => d.CoworkingId == coworkingId && d.Status == CoworkingDocumentStatus.Pending)
                .CountAsync(cancellationToken);
        }

        public async Task<List<CoworkingDocument>> GetPendingCoworkingDocumentsAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingDocuments
                .Where(d => d.CoworkingId == coworkingId && d.Status == CoworkingDocumentStatus.Pending)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> GetPendingPaymentsWithoutFiscalNoteCountAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.Payments
                .Where(p => (p.Subscription != null && p.Subscription.Contract.CoworkingAddress.CoworkingId == coworkingId) ||
                            (p.Reservation != null && p.Reservation.CoworkingAddress.CoworkingId == coworkingId))
                .Where(p => p.Status == PaymentStatus.Paid)
                .Where(p => (p.Subscription != null && p.Subscription.Contract.Status == ContractStatus.Active) || (p.Reservation != null))
                .Where(p => !_context.FiscalNotes.Any(fn =>
                    fn.PaymentId == p.Id &&
                    fn.Type == FiscalNoteType.Servico &&
                    (fn.Status == FiscalNoteStatus.Emitida || fn.Status == FiscalNoteStatus.Importada)))
                .CountAsync(cancellationToken);
        }

        public async Task<int> GetRejectedFiscalNotesCountAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.FiscalNotes
                .Where(fn => fn.CoworkingId == coworkingId && fn.Type == FiscalNoteType.Servico && fn.Status == FiscalNoteStatus.Rejeitada)
                .Where(fn => !_context.FiscalNotes.Any(fn2 =>
                    fn2.PaymentId == fn.PaymentId &&
                    fn2.Type == FiscalNoteType.Servico &&
                    (fn2.Status == FiscalNoteStatus.Emitida || fn2.Status == FiscalNoteStatus.Importada) &&
                    fn2.Id != fn.Id))
                .CountAsync(cancellationToken);
        }

        public async Task<Coworking?> GetCoworkingWithAddressesAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.Coworkings
                .Include(c => c.Addresses)
                .FirstOrDefaultAsync(c => c.Id == coworkingId, cancellationToken);
        }

        public async Task<List<Contract>> GetContractsForDashboardAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.Contracts
                .Include(c => c.CoworkingAddress)
                .Where(c => c.CoworkingAddress.CoworkingId == coworkingId)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> GetPendingPaymentsCountForCoworkingAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.Payments
                .Include(p => p.Subscription!)
                    .ThenInclude(s => s.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                .Where(p => p.Status == PaymentStatus.AwaitingValidation
                    && p.Subscription!.Contract.CoworkingAddress.CoworkingId == coworkingId)
                .CountAsync(cancellationToken);
        }

        public async Task<int> GetPendingInterestsCountAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.Interests
                .Include(i => i.CoworkingAddress)
                .Where(i => i.CoworkingAddress.CoworkingId == coworkingId && i.Status == InterestStatus.Pending)
                .CountAsync(cancellationToken);
        }

        public async Task<int> GetConfirmedReservationsCountAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.Reservations
                .Include(r => r.CoworkingAddress)
                .Where(r => r.CoworkingAddress.CoworkingId == coworkingId && r.Status == ReservationStatus.Confirmed)
                .CountAsync(cancellationToken);
        }

        public async Task DismissStatusNotificationAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            var coworkingToUpdate = await _context.Coworkings.FindAsync(new object[] { coworkingId }, cancellationToken);
            if (coworkingToUpdate != null)
            {
                coworkingToUpdate.HasPendingStatusNotification = false;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<User?> FindUserAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        }

        public async Task<Coworking?> GetCoworkingByCnpjAsync(string cnpjClean, CancellationToken cancellationToken = default)
        {
            return await _context.Coworkings
                .Where(c => c.CNPJ != null &&
                    c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjClean)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public void AddCoworkingPhoto(CoworkingPhoto photo)
        {
            _context.CoworkingPhotos.Add(photo);
        }

        public void AddCoworkingDocument(CoworkingDocument doc)
        {
            _context.CoworkingDocuments.Add(doc);
        }

        public async Task<int> GetCoworkingOperatingHoursCountAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingOperatingHours
                .Where(h => h.CoworkingId == coworkingId && h.IsActive)
                .CountAsync(cancellationToken);
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }

        public void UpdateUser(User user)
        {
            _context.Users.Update(user);
        }

        /// <inheritdoc />
        public async Task<Dictionary<int, string>> GetCardPriceRejectionReasonsByAddressIdsAsync(IEnumerable<int> addressIds, CancellationToken cancellationToken = default)
        {
            var ids = addressIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, string>();
            var rejected = await _context.CardPriceChangeRequests
                .Where(r => ids.Contains(r.CoworkingAddressId) && r.Status == CapacityApprovalStatus.Rejected && !string.IsNullOrEmpty(r.RejectionReason))
                .OrderByDescending(r => r.ReviewedAt ?? r.UpdatedAt ?? r.CreatedAt)
                .Select(r => new { r.CoworkingAddressId, r.RejectionReason })
                .ToListAsync(cancellationToken);
            var dict = new Dictionary<int, string>();
            foreach (var r in rejected)
            {
                if (r.RejectionReason != null && !dict.ContainsKey(r.CoworkingAddressId))
                    dict[r.CoworkingAddressId] = r.RejectionReason;
            }
            return dict;
        }

        public async Task<(List<CoworkingAddress> Items, int TotalItems)> GetCoworkingAddressesPagedAsync(int coworkingId, string? search, string? status, int? locationId, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.CoworkingAddresses
                .Include(a => a.Location)
                .Where(a => a.CoworkingId == coworkingId)
                .AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a =>
                    a.Address.Contains(search) ||
                    a.City.Contains(search) ||
                    a.State.Contains(search) ||
                    a.Neighborhood.Contains(search));
            }
            if (!string.IsNullOrEmpty(status))
            {
                var isAvailable = status == "available";
                query = query.Where(a => a.IsAvailable == isAvailable);
            }
            if (locationId.HasValue && locationId.Value > 0)
                query = query.Where(a => a.LocationId == locationId.Value);
            query = query.OrderByDescending(a => a.CreatedAt);
            var totalItems = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            return (items, totalItems);
        }

        public async Task<List<Location>> GetActiveLocationsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Locations.Where(l => l.IsActive).OrderBy(l => l.City).ToListAsync(cancellationToken);
        }

        public async Task<Dictionary<int, int>> GetActiveContractsCountByAddressAsync(IEnumerable<int> addressIds, CancellationToken cancellationToken = default)
        {
            var ids = addressIds.ToList();
            return await _context.Contracts
                .Where(c => ids.Contains(c.CoworkingAddressId) && c.Status == ContractStatus.Active)
                .GroupBy(c => c.CoworkingAddressId)
                .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);
        }

        public async Task<bool> HasFiscalAddressAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingAddresses.AnyAsync(a => a.CoworkingId == coworkingId, cancellationToken);
        }

        public async Task<bool> HasRoomRentalAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingProducts
                .AnyAsync(p => p.CoworkingId == coworkingId && p.ProductType == "RoomRental" && p.IsActive, cancellationToken);
        }

        public async Task<Location?> FindLocationAsync(int locationId, CancellationToken cancellationToken = default)
        {
            return await _context.Locations.FindAsync(new object[] { locationId }, cancellationToken);
        }

        public async Task<CoworkingAddress?> GetAddressWithLocationAsync(int addressId, int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingAddresses
                .Include(a => a.Location)
                .Include(a => a.CapacityChangeRequests)
                .FirstOrDefaultAsync(a => a.Id == addressId && a.CoworkingId == coworkingId, cancellationToken);
        }

        public async Task<CoworkingAddress?> GetAddressWithLocationAndCapacityRequestsAsync(int addressId, int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingAddresses
                .Include(a => a.Location)
                .Include(a => a.CapacityChangeRequests)
                .FirstOrDefaultAsync(a => a.Id == addressId && a.CoworkingId == coworkingId, cancellationToken);
        }

        public async Task<CoworkingAddress?> GetAddressWithLocationAndCapacityRequestsByAddressIdAsync(int addressId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingAddresses
                .Include(a => a.Location)
                .Include(a => a.CapacityChangeRequests)
                .Include(a => a.Coworking)
                .FirstOrDefaultAsync(a => a.Id == addressId, cancellationToken);
        }

        public async Task<bool> HasActiveContractsForAddressAsync(int addressId, CancellationToken cancellationToken = default)
        {
            return await _context.Contracts
                .AnyAsync(c => c.CoworkingAddressId == addressId && c.Status == ContractStatus.Active, cancellationToken);
        }

        public async Task<Service?> GetServiceByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            return await _context.Services
                .FirstOrDefaultAsync(s => s.Code == code && s.IsActive, cancellationToken);
        }

        public async Task<bool> PartnerServiceExistsAsync(int coworkingId, int serviceId, CancellationToken cancellationToken = default)
        {
            return await _context.PartnerServices
                .AnyAsync(ps => ps.CoworkingId == coworkingId && ps.ServiceId == serviceId, cancellationToken);
        }

        public void AddPartnerService(PartnerService partnerService)
        {
            _context.PartnerServices.Add(partnerService);
        }

        public async Task<CoworkingAddress?> GetExistingAddressWithCapacityRequestsAsync(int addressId, int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingAddresses
                .Include(a => a.CapacityChangeRequests)
                .FirstOrDefaultAsync(a => a.Id == addressId && a.CoworkingId == coworkingId, cancellationToken);
        }

        public async Task<CoworkingAddress?> GetExistingAddressWithLocationAsync(int addressId, int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingAddresses
                .Include(a => a.Location)
                .FirstOrDefaultAsync(a => a.Id == addressId && a.CoworkingId == coworkingId, cancellationToken);
        }

        public async Task<CoworkingAddress?> GetAddressWithCardPriceRequestsAsync(int addressId, int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingAddresses
                .Include(a => a.Location)
                .Include(a => a.CardPriceChangeRequests)
                .FirstOrDefaultAsync(a => a.Id == addressId && a.CoworkingId == coworkingId, cancellationToken);
        }

        public async Task<CoworkingAddress?> GetAddressWithCardPriceRequestsByAddressIdAsync(int addressId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingAddresses
                .Include(a => a.Location)
                .Include(a => a.CardPriceChangeRequests)
                .Include(a => a.Coworking)
                .FirstOrDefaultAsync(a => a.Id == addressId, cancellationToken);
        }

        public void AddCardPriceChangeRequest(CardPriceChangeRequest request)
        {
            _context.CardPriceChangeRequests.Add(request);
        }

        public async Task<(List<Contract> Items, int TotalItems)> GetContractsPagedAsync(int coworkingId, string? search, string? status, int? locationId, string? clientName, DateTime? startDateFrom, DateTime? startDateTo, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.Contracts
                .Include(c => c.Client)
                .Include(c => c.CoworkingAddress!)
                    .ThenInclude(a => a.Location)
                .Include(c => c.Subscription)
                    .ThenInclude(s => s!.Payments)
                .Where(c => c.CoworkingAddress!.CoworkingId == coworkingId)
                .AsQueryable();
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ContractStatus>(status, out var contractStatus))
                query = query.Where(c => c.Status == contractStatus);
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c =>
                    (c.Client != null && (c.Client.RazaoSocial != null && c.Client.RazaoSocial.Contains(search) ||
                                         c.Client.ContactName != null && c.Client.ContactName.Contains(search) ||
                                         c.Client.CNPJ != null && c.Client.CNPJ.Contains(search) ||
                                         c.Client.CPF != null && c.Client.CPF.Contains(search))) ||
                    (c.CoworkingAddress != null && (c.CoworkingAddress.Address != null && c.CoworkingAddress.Address.Contains(search) ||
                                                    c.CoworkingAddress.City != null && c.CoworkingAddress.City.Contains(search) ||
                                                    c.CoworkingAddress.State != null && c.CoworkingAddress.State.Contains(search) ||
                                                    (c.CoworkingAddress.Location != null && (c.CoworkingAddress.Location.City != null && c.CoworkingAddress.Location.City.Contains(search) ||
                                                                                             c.CoworkingAddress.Location.State != null && c.CoworkingAddress.Location.State.Contains(search))))) ||
                    (c.ContractNumber != null && c.ContractNumber.Contains(search)));
            }
            if (locationId.HasValue && locationId.Value > 0)
                query = query.Where(c => c.CoworkingAddress != null && c.CoworkingAddress.LocationId == locationId.Value);
            if (!string.IsNullOrEmpty(clientName))
                query = query.Where(c => c.Client != null &&
                    (c.Client.RazaoSocial != null && c.Client.RazaoSocial.Contains(clientName) ||
                     c.Client.ContactName != null && c.Client.ContactName.Contains(clientName)));
            if (startDateFrom.HasValue)
                query = query.Where(c => c.StartDate >= startDateFrom.Value);
            if (startDateTo.HasValue)
                query = query.Where(c => c.StartDate <= startDateTo.Value);
            var totalItems = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            return (items, totalItems);
        }

        public async Task<List<Location>> GetActiveLocationsOrderedAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Locations
                .Where(l => l.IsActive)
                .OrderBy(l => l.City)
                .ThenBy(l => l.State)
                .ToListAsync(cancellationToken);
        }

        public async Task<Contract?> GetContractDetailsAsync(int contractId, int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.Contracts
                .Include(c => c.Client!)
                    .ThenInclude(cl => cl.User)
                .Include(c => c.CoworkingAddress!)
                    .ThenInclude(a => a!.Location)
                .Include(c => c.CoworkingAddress!)
                    .ThenInclude(a => a!.Coworking)
                .Include(c => c.Service)
                .Include(c => c.Subscription)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == contractId && c.CoworkingAddress!.CoworkingId == coworkingId, cancellationToken);
        }

        public async Task<List<AddressDeclaration>> GetAddressDeclarationsByContractAsync(int contractId, CancellationToken cancellationToken = default)
        {
            return await _context.AddressDeclarations
                .Where(d => d.ContractId == contractId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Payment>> GetPaymentsBySubscriptionIdAsync(int subscriptionId, CancellationToken cancellationToken = default)
        {
            return await _context.Payments
                .Where(p => p.SubscriptionId == subscriptionId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<Dictionary<int, List<PaymentHistory>>> GetPaymentHistoriesByPaymentIdsAsync(IEnumerable<int> paymentIds, CancellationToken cancellationToken = default)
        {
            var ids = paymentIds.ToList();
            if (ids.Count == 0) return new Dictionary<int, List<PaymentHistory>>();
            var list = await _context.PaymentHistories
                .Where(ph => ids.Contains(ph.PaymentId))
                .Include(ph => ph.ActionByUser)
                    .ThenInclude(u => u!.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .ToListAsync(cancellationToken);
            return list.GroupBy(ph => ph.PaymentId).ToDictionary(g => g.Key, g => g.ToList());
        }

        public async Task<AddressDeclaration?> GetDeclarationWithContractAsync(int declarationId, CancellationToken cancellationToken = default)
        {
            return await _context.AddressDeclarations
                .Include(d => d.Contract!)
                    .ThenInclude(c => c!.CoworkingAddress)
                .Include(d => d.Contract!)
                    .ThenInclude(c => c!.Service)
                .FirstOrDefaultAsync(d => d.Id == declarationId, cancellationToken);
        }

        public async Task<Document?> GetDocumentWithContractAsync(int documentId, CancellationToken cancellationToken = default)
        {
            return await _context.Documents
                .Include(d => d.Contract!)
                    .ThenInclude(c => c!.CoworkingAddress)
                .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        }

        public async Task<List<CoworkingAddress>> GetCoworkingAddressesForReservationsAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingAddresses
                .Where(a => a.CoworkingId == coworkingId)
                .OrderBy(a => a.City)
                .ThenBy(a => a.Neighborhood)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Reservation>> GetReservationsFilteredAsync(int coworkingId, string? search, int? addressId, DateTime? allocationDateFrom, DateTime? allocationDateTo, string? status, CancellationToken cancellationToken = default)
        {
            var query = _context.Reservations
                .Include(r => r.Client!)
                    .ThenInclude(c => c!.User)
                .Include(r => r.CoworkingAddress)
                .Where(r => r.CoworkingAddress!.CoworkingId == coworkingId)
                .AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r =>
                    r.Client!.User!.FullName.Contains(search) ||
                    (r.Client.ContactEmail != null && r.Client.ContactEmail.Contains(search)) ||
                    (r.Client.ContactPhone != null && r.Client.ContactPhone.Contains(search)) ||
                    (r.Client.CNPJ != null && r.Client.CNPJ.Contains(search)) ||
                    (r.Client.CPF != null && r.Client.CPF.Contains(search)));
            }
            if (addressId.HasValue)
                query = query.Where(r => r.CoworkingAddressId == addressId.Value);
            if (allocationDateFrom.HasValue)
                query = query.Where(r => r.ReservationDate >= allocationDateFrom.Value);
            if (allocationDateTo.HasValue)
                query = query.Where(r => r.ReservationDate <= allocationDateTo.Value);
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "Confirmed" || status == "Completed" || status == "Cancelled")
                {
                    var resStatus = status == "Confirmed" ? ReservationStatus.Confirmed : status == "Completed" ? ReservationStatus.Completed : ReservationStatus.Cancelled;
                    query = query.Where(r => r.Status == resStatus);
                }
                else
                    query = query.Where(r => false);
            }
            return await query
                .OrderByDescending(r => r.ReservationDate)
                .ThenByDescending(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Interest>> GetInterestsFilteredAsync(int coworkingId, string? search, int? addressId, DateTime? allocationDateFrom, DateTime? allocationDateTo, string? status, CancellationToken cancellationToken = default)
        {
            var query = _context.Interests
                .Include(i => i.CoworkingAddress)
                .Include(i => i.ContactedByUser)
                .Where(i => i.CoworkingAddress!.CoworkingId == coworkingId)
                .AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(i =>
                    i.Name.Contains(search) ||
                    i.LastName.Contains(search) ||
                    i.Email.Contains(search) ||
                    i.Phone.Contains(search));
            }
            if (addressId.HasValue)
                query = query.Where(i => i.CoworkingAddressId == addressId.Value);
            if (allocationDateFrom.HasValue)
                query = query.Where(i => i.AllocationDate.HasValue && i.AllocationDate >= allocationDateFrom.Value);
            if (allocationDateTo.HasValue)
                query = query.Where(i => i.AllocationDate.HasValue && i.AllocationDate <= allocationDateTo.Value);
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "Pending" || status == "Contacted" || status == "Converted" || status == "Rejected")
                {
                    var intStatus = status == "Pending" ? InterestStatus.Pending : status == "Contacted" ? InterestStatus.Contacted : status == "Converted" ? InterestStatus.Converted : InterestStatus.Rejected;
                    query = query.Where(i => i.Status == intStatus);
                }
                else
                    query = query.Where(i => false);
            }
            return await query.OrderByDescending(i => i.CreatedAt).ToListAsync(cancellationToken);
        }

        public async Task<Interest?> GetInterestWithAddressAsync(int interestId, int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.Interests
                .Include(i => i.CoworkingAddress)
                .FirstOrDefaultAsync(i => i.Id == interestId && i.CoworkingAddress!.CoworkingId == coworkingId, cancellationToken);
        }

        public async Task<Reservation?> GetReservationWithAddressAsync(int reservationId, int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.Reservations
                .Include(x => x.CoworkingAddress)
                .FirstOrDefaultAsync(x => x.Id == reservationId && x.CoworkingAddress != null && x.CoworkingAddress.CoworkingId == coworkingId, cancellationToken);
        }

        public async Task<List<Reservation>> GetReservationsForRevenueReportAsync(int coworkingId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default)
        {
            var q = _context.Reservations
                .Include(r => r.Client)
                .Include(r => r.CoworkingAddress)
                .Where(r => r.CoworkingAddress != null && r.CoworkingAddress.CoworkingId == coworkingId && r.Status != ReservationStatus.Cancelled);
            if (dateFrom.HasValue)
                q = q.Where(r => r.ReservationDate >= dateFrom.Value.Date);
            if (dateTo.HasValue)
                q = q.Where(r => r.ReservationDate <= dateTo.Value.Date.AddDays(1));
            return await q.ToListAsync(cancellationToken);
        }

        public async Task<List<ReservationCommission>> GetReservationCommissionsByReservationIdsAsync(IEnumerable<int> reservationIds, CancellationToken cancellationToken = default)
        {
            var ids = reservationIds.ToList();
            if (ids.Count == 0) return new List<ReservationCommission>();
            return await _context.ReservationCommissions
                .Where(rc => ids.Contains(rc.ReservationId))
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Transfer>> GetTransfersByReservationIdsAsync(IEnumerable<int> reservationIds, CancellationToken cancellationToken = default)
        {
            var ids = reservationIds.ToList();
            if (ids.Count == 0) return new List<Transfer>();
            return await _context.Transfers
                .Where(t => t.ReservationId.HasValue && ids.Contains(t.ReservationId.Value))
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Commission>> GetCommissionsByCoworkingIdAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.Commissions
                .Include(c => c.Payment!)
                    .ThenInclude(p => p!.Subscription)
                .Where(c => c.CoworkingId == coworkingId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Transfer>> GetTransfersByCoworkingIdAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.Transfers
                .Where(t => t.CoworkingId == coworkingId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<int>> GetPaymentIdsWithEmitidaFiscalNoteAsync(IEnumerable<int> paymentIds, CancellationToken cancellationToken = default)
        {
            var ids = paymentIds.ToList();
            if (ids.Count == 0) return new List<int>();
            return await _context.FiscalNotes
                .Where(fn => fn.PaymentId != null && ids.Contains(fn.PaymentId.Value)
                    && fn.Type == FiscalNoteType.Servico && (fn.Status == FiscalNoteStatus.Emitida || fn.Status == FiscalNoteStatus.Importada))
                .Select(fn => fn.PaymentId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        public async Task<List<string>> GetServiceNamesExceptFiscalAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Services
                .Where(x => !x.Code.Contains(ServiceCodes.FiscalAddressCodePrefix))
                .Select(x => x.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<Service?> FindServiceByIdAsync(int serviceId, CancellationToken cancellationToken = default)
        {
            return await _context.Services
                .FirstOrDefaultAsync(s => s.Id == serviceId && s.IsActive, cancellationToken);
        }

        public async Task<PartnerService?> GetPartnerServiceAsync(int coworkingId, int serviceId, CancellationToken cancellationToken = default)
        {
            return await _context.PartnerServices
                .FirstOrDefaultAsync(ps => ps.CoworkingId == coworkingId && ps.ServiceId == serviceId, cancellationToken);
        }

        public async Task<PartnerService?> GetPartnerServiceForRemovalAsync(int partnerServiceId, int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.PartnerServices
                .Include(ps => ps.Service)
                .FirstOrDefaultAsync(ps => ps.Id == partnerServiceId && ps.CoworkingId == coworkingId, cancellationToken);
        }

        public async Task<List<int>> GetCoworkingAddressIdsAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingAddresses
                .Where(a => a.CoworkingId == coworkingId)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> HasActiveOrSuspendedContractsInAddressesAsync(IEnumerable<int> addressIds, CancellationToken cancellationToken = default)
        {
            var ids = addressIds.ToList();
            return await _context.Contracts
                .AnyAsync(c => ids.Contains(c.CoworkingAddressId)
                    && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Suspended), cancellationToken);
        }

        public async Task<List<int>> GetContractIdsByCoworkingAddressIdsAsync(IEnumerable<int> addressIds, CancellationToken cancellationToken = default)
        {
            var ids = addressIds.ToList();
            return await _context.Contracts
                .Where(c => ids.Contains(c.CoworkingAddressId))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<int>> GetSubscriptionIdsByContractIdsAsync(IEnumerable<int> contractIds, CancellationToken cancellationToken = default)
        {
            var ids = contractIds.ToList();
            return await _context.Subscriptions
                .Where(s => ids.Contains(s.ContractId))
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> HasPaymentsAwaitingValidationInSubscriptionsAsync(IEnumerable<int> subscriptionIds, CancellationToken cancellationToken = default)
        {
            var ids = subscriptionIds.ToList();
            return await _context.Payments
                .AnyAsync(p => p.SubscriptionId != null && ids.Contains(p.SubscriptionId.Value)
                    && p.Status == PaymentStatus.AwaitingValidation, cancellationToken);
        }

        public async Task<(List<Payment> Items, int TotalItems, decimal TotalAmount)> GetPaymentsWithoutFiscalNotePagedAsync(int coworkingId, string? serviceType, string? client, string? contractNumber, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.Payments
                .Include(p => p.Subscription!)
                    .ThenInclude(s => s!.Contract)
                        .ThenInclude(c => c!.CoworkingAddress!)
                            .ThenInclude(ca => ca!.Location)
                .Include(p => p.Subscription!)
                    .ThenInclude(s => s!.Contract)
                        .ThenInclude(c => c!.Client!)
                            .ThenInclude(cl => cl!.User)
                .Include(p => p.Reservation!)
                    .ThenInclude(r => r!.CoworkingAddress!)
                        .ThenInclude(ca => ca!.Location)
                .Include(p => p.Reservation!)
                    .ThenInclude(r => r!.Client!)
                        .ThenInclude(cl => cl!.User)
                .Where(p =>
                    (p.Subscription != null && p.Subscription.Contract.CoworkingAddress.CoworkingId == coworkingId) ||
                    (p.Reservation != null && p.Reservation.CoworkingAddress!.CoworkingId == coworkingId))
                .Where(p => p.Status == PaymentStatus.Paid)
                .Where(p =>
                    (p.Subscription != null && p.Subscription.Contract.Status == ContractStatus.Active) ||
                    (p.Reservation != null))
                .Where(p => !_context.FiscalNotes.Any(fn =>
                    fn.PaymentId == p.Id &&
                    fn.Type == FiscalNoteType.Servico &&
                    (fn.Status == FiscalNoteStatus.Emitida || fn.Status == FiscalNoteStatus.Importada)))
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(serviceType))
            {
                if (serviceType.Equals("EnderecoFiscal", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(p => p.SubscriptionId != null);
                else if (serviceType.Equals("ReservaSala", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(p => p.ReservationId != null);
            }
            if (!string.IsNullOrWhiteSpace(contractNumber))
            {
                var contractNum = contractNumber.Trim();
                query = query.Where(p => p.Subscription != null && p.Subscription.Contract != null && p.Subscription.Contract.ContractNumber != null && p.Subscription.Contract.ContractNumber.Contains(contractNum));
            }
            if (!string.IsNullOrWhiteSpace(client))
            {
                var term = client.Trim().ToLower();
                var termDoc = client.Trim().Replace(".", "").Replace("/", "").Replace("-", "").Replace(" ", "");
                var matchingClientIds = await _context.Clients
                    .Where(c =>
                        (c.RazaoSocial != null && c.RazaoSocial.ToLower().Contains(term)) ||
                        (c.NomeFantasia != null && c.NomeFantasia.ToLower().Contains(term)) ||
                        (c.ContactName != null && c.ContactName.ToLower().Contains(term)) ||
                        (c.CNPJ != null && c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Contains(termDoc)) ||
                        (c.CPF != null && c.CPF.Replace(".", "").Replace("-", "").Contains(termDoc)))
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);
                if (matchingClientIds.Count > 0)
                {
                    query = query.Where(p =>
                        (p.Subscription != null && p.Subscription.Contract != null && matchingClientIds.Contains(p.Subscription.Contract.ClientId)) ||
                        (p.Reservation != null && matchingClientIds.Contains(p.Reservation.ClientId)));
                }
                else
                    query = query.Where(p => false);
            }
            var totalItems = await query.CountAsync(cancellationToken);
            var totalAmount = totalItems > 0 ? await query.SumAsync(p => p.Amount, cancellationToken) : 0m;
            var totalPages = pageSize > 0 ? (int)Math.Ceiling(totalItems / (double)pageSize) : 1;
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = Math.Max(1, totalPages);
            var items = await query
                .OrderByDescending(p => p.PaidAt ?? p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            return (items, totalItems, totalAmount);
        }

        public async Task<List<int>> GetMatchingClientIdsForSearchAsync(string clientTerm, CancellationToken cancellationToken = default)
        {
            var term = clientTerm.Trim().ToLower();
            var termDoc = clientTerm.Trim().Replace(".", "").Replace("/", "").Replace("-", "").Replace(" ", "");
            return await _context.Clients
                .Where(c =>
                    (c.RazaoSocial != null && c.RazaoSocial.ToLower().Contains(term)) ||
                    (c.NomeFantasia != null && c.NomeFantasia.ToLower().Contains(term)) ||
                    (c.ContactName != null && c.ContactName.ToLower().Contains(term)) ||
                    (c.CNPJ != null && c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Contains(termDoc)) ||
                    (c.CPF != null && c.CPF.Replace(".", "").Replace("-", "").Contains(termDoc)))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Commission>> GetCommissionsByPaymentIdsAsync(IEnumerable<int> paymentIds, CancellationToken cancellationToken = default)
        {
            var ids = paymentIds.ToList();
            if (ids.Count == 0) return new List<Commission>();
            return await _context.Commissions
                .Where(c => ids.Contains(c.PaymentId))
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> HasActiveSefazConfigurationAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingSefazConfigurations
                .AnyAsync(c => c.CoworkingId == coworkingId && c.IsActive, cancellationToken);
        }

        public async Task<Payment?> GetPaymentForFiscalNoteDetailAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            return await _context.Payments
                .Include(p => p.Subscription!)
                    .ThenInclude(s => s!.Contract)
                        .ThenInclude(c => c!.CoworkingAddress!)
                            .ThenInclude(ca => ca!.Location)
                .Include(p => p.Subscription!)
                    .ThenInclude(s => s!.Contract)
                        .ThenInclude(c => c!.Client!)
                            .ThenInclude(cl => cl!.User)
                .Include(p => p.Reservation!)
                    .ThenInclude(r => r!.CoworkingAddress!)
                        .ThenInclude(ca => ca!.Location)
                .Include(p => p.Reservation!)
                    .ThenInclude(r => r!.Client!)
                        .ThenInclude(cl => cl!.User)
                .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        }

        public async Task<FiscalNote?> GetExistingEmitidaFiscalNoteAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            return await _context.FiscalNotes
                .FirstOrDefaultAsync(fn => fn.PaymentId == paymentId && fn.Type == FiscalNoteType.Servico && fn.Status == FiscalNoteStatus.Emitida, cancellationToken);
        }

        public void AddFiscalNote(FiscalNote fiscalNote)
        {
            _context.FiscalNotes.Add(fiscalNote);
        }

        public void UpdateFiscalNote(FiscalNote fiscalNote)
        {
            _context.FiscalNotes.Update(fiscalNote);
        }

        public async Task<Coworking?> FindCoworkingAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            return await _context.Coworkings.FindAsync(new object[] { coworkingId }, cancellationToken);
        }
    }
}
