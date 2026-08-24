using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Application.Services
{
    public class ClientAppService : IClientAppService
    {
        private readonly ApplicationDbContext _context;

        public ClientAppService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Client?> GetClientByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        }

        public async Task<Client?> GetClientByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<Client?> GetClientByIdWithUserAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<Client?> GetClientByUserIdAsNoTrackingAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        }

        public async Task<Client?> GetClientByUserIdTrackingAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        }

        public async Task<List<Client>> GetClientsWithCnpjExcludingAsync(int excludeId, CancellationToken cancellationToken = default)
        {
            return await _context.Clients.Where(c => c.CNPJ != null && c.Id != excludeId).ToListAsync(cancellationToken);
        }

        public async Task<List<Client>> GetClientsWithCpfExcludingAsync(int excludeId, CancellationToken cancellationToken = default)
        {
            return await _context.Clients.Where(c => c.CPF != null && c.Id != excludeId).ToListAsync(cancellationToken);
        }

        public async Task<bool> ClientExistsWithCnpjAsync(string cnpjClean, int? excludeClientId, CancellationToken cancellationToken = default)
        {
            var query = _context.Clients.Where(c => c.CNPJ != null);
            if (excludeClientId.HasValue)
                query = query.Where(c => c.Id != excludeClientId.Value);
            var all = await query.ToListAsync(cancellationToken);
            return all.Any(c => c.CNPJ != null && c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjClean);
        }

        public async Task<bool> ClientExistsWithCpfAsync(string cpfClean, int? excludeClientId, CancellationToken cancellationToken = default)
        {
            var query = _context.Clients.Where(c => c.CPF != null);
            if (excludeClientId.HasValue)
                query = query.Where(c => c.Id != excludeClientId.Value);
            var all = await query.ToListAsync(cancellationToken);
            return all.Any(c => c.CPF != null && c.CPF.Replace(".", "").Replace("-", "").Trim() == cpfClean);
        }

        public async Task AddClientAsync(Client client, CancellationToken cancellationToken = default)
        {
            _context.Clients.Add(client);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<User?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        }

        public async Task<List<int>> GetContractAddressIdsByClientIdAsync(int clientId, CancellationToken cancellationToken = default)
        {
            return await _context.Contracts
                .AsNoTracking()
                .Where(c => c.ClientId == clientId
                    && c.Status != ContractStatus.Cancelled
                    && c.Status != ContractStatus.Expired
                    && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Suspended || c.Status == ContractStatus.PendingCancellation))
                .Select(c => c.CoworkingAddressId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        public async Task<int?> GetActiveContractIdByClientIdAsync(int clientId, CancellationToken cancellationToken = default)
        {
            var id = await _context.Contracts
                .AsNoTracking()
                .Where(c => c.ClientId == clientId && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Suspended))
                .OrderByDescending(c => c.StartDate)
                .Select(c => c.Id)
                .FirstOrDefaultAsync(cancellationToken);
            return id == 0 ? null : id;
        }

        public async Task<List<Contract>> GetPendingContractsWithSubscriptionPaymentsAsync(int clientId, CancellationToken cancellationToken = default)
        {
            return await _context.Contracts
                .Include(c => c.Subscription)
                    .ThenInclude(s => s!.Payments)
                .Where(c => c.ClientId == clientId && c.Status == ContractStatus.PendingPayment)
                .ToListAsync(cancellationToken);
        }

        public async Task PromotePendingContractsToActiveIfPaidAsync(int clientId, CancellationToken cancellationToken = default)
        {
            var pendingContracts = await _context.Contracts
                .Where(c => c.ClientId == clientId && c.Status == ContractStatus.PendingPayment)
                .ToListAsync(cancellationToken);
            if (pendingContracts.Count == 0) return;
            var contractIds = pendingContracts.Select(c => c.Id).ToList();
            var subscriptions = await _context.Subscriptions
                .Where(s => contractIds.Contains(s.ContractId))
                .ToListAsync(cancellationToken);
            var subscriptionIds = subscriptions.Select(s => s.Id).ToList();
            if (subscriptionIds.Count == 0) return;
            var paidPayments = await _context.Payments
                .Where(p => subscriptionIds.Contains(p.SubscriptionId ?? 0) && p.Status == PaymentStatus.Paid)
                .GroupBy(p => p.SubscriptionId)
                .Select(g => new { SubscriptionId = g.Key, FirstPaidAt = g.Min(p => p.PaidAt ?? p.CreatedAt) })
                .ToListAsync(cancellationToken);
            var subscriptionToFirstPaidDate = paidPayments
                .Where(p => p.SubscriptionId.HasValue)
                .ToDictionary(p => p.SubscriptionId!.Value, p => p.FirstPaidAt);
            foreach (var subscription in subscriptions)
            {
                if (!subscriptionToFirstPaidDate.TryGetValue(subscription.Id, out var firstPaidDate)) continue;
                var contract = pendingContracts.FirstOrDefault(c => c.Id == subscription.ContractId);
                if (contract == null) continue;
                contract.Status = ContractStatus.Active;
                if (!contract.ExpiryDate.HasValue)
                    contract.ExpiryDate = firstPaidDate.AddYears(1);
                contract.UpdatedAt = DateTime.UtcNow;
                _context.Entry(contract).Property(c => c.Status).IsModified = true;
                _context.Entry(contract).Property(c => c.UpdatedAt).IsModified = true;
                if (contract.ExpiryDate.HasValue)
                    _context.Entry(contract).Property(c => c.ExpiryDate).IsModified = true;
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<(List<Contract> Items, int TotalItems)> GetMyContractsAsync(int clientId, string? search, string? status, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.Contracts
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a!.Location)
                .Include(c => c.Subscription)
                    .ThenInclude(s => s!.Payments)
                .Where(c => c.ClientId == clientId)
                .AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c =>
                    c.ContractNumber.Contains(search) ||
                    (c.CoworkingAddress != null && c.CoworkingAddress.Address.Contains(search)) ||
                    (c.CoworkingAddress != null && c.CoworkingAddress.Location != null && c.CoworkingAddress.Location.City.Contains(search)));
            }
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ContractStatus>(status, out var contractStatus))
                query = query.Where(c => c.Status == contractStatus);
            query = query.OrderByDescending(c => c.StartDate);
            var totalItems = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            return (items, totalItems);
        }

        public async Task<Contract?> GetContractByIdAsync(int contractId, CancellationToken cancellationToken = default)
        {
            return await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken);
        }

        public async Task<Contract?> GetContractByIdAndClientIdAsync(int contractId, int clientId, CancellationToken cancellationToken = default)
        {
            return await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId && c.ClientId == clientId, cancellationToken);
        }

        public async Task<Contract?> GetExistingContractForAddressAsync(int clientId, int coworkingAddressId, CancellationToken cancellationToken = default)
        {
            return await _context.Contracts
                .Include(c => c.CoworkingAddress)
                .Where(c => c.ClientId == clientId
                    && c.CoworkingAddressId == coworkingAddressId
                    && c.Status != ContractStatus.Cancelled
                    && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Suspended || c.Status == ContractStatus.PendingPayment))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<Contract?> GetExistingContractInLocationAsync(int clientId, int locationId, CancellationToken cancellationToken = default)
        {
            return await _context.Contracts
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a!.Location)
                .Where(c => c.ClientId == clientId
                    && c.CoworkingAddress != null
                    && c.CoworkingAddress.LocationId == locationId
                    && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Suspended))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task UpdateContractAsync(Contract contract, CancellationToken cancellationToken = default)
        {
            _context.Contracts.Update(contract);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<Subscription?> GetSubscriptionByContractIdAsync(int contractId, CancellationToken cancellationToken = default)
        {
            return await _context.Subscriptions.FirstOrDefaultAsync(s => s.ContractId == contractId, cancellationToken);
        }

        public async Task<Subscription?> GetSubscriptionWithContractAsync(int subscriptionId, CancellationToken cancellationToken = default)
        {
            return await _context.Subscriptions
                .Include(s => s.Contract)
                .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken);
        }

        public async Task<List<Subscription>> GetSubscriptionsByContractIdsAsync(IEnumerable<int> contractIds, CancellationToken cancellationToken = default)
        {
            var ids = contractIds.ToList();
            if (ids.Count == 0) return new List<Subscription>();
            return await _context.Subscriptions
                .Where(s => ids.Contains(s.ContractId))
                .ToListAsync(cancellationToken);
        }

        public async Task<List<int>> GetSubscriptionIdsByContractIdAsync(int contractId, CancellationToken cancellationToken = default)
        {
            return await _context.Subscriptions
                .Where(s => s.ContractId == contractId)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<Payment?> GetFirstPaymentBySubscriptionIdAsync(int subscriptionId, CancellationToken cancellationToken = default)
        {
            return await _context.Payments
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(p => p.SubscriptionId == subscriptionId, cancellationToken);
        }

        public async Task<Payment?> GetPaymentAwaitingValidationBySubscriptionIdsAsync(IEnumerable<int> subscriptionIds, CancellationToken cancellationToken = default)
        {
            var ids = subscriptionIds.ToList();
            if (ids.Count == 0) return null;
            return await _context.Payments
                .Where(p => p.SubscriptionId.HasValue && ids.Contains(p.SubscriptionId.Value) && p.Status == PaymentStatus.AwaitingValidation)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<Payment?> GetPaymentByIdAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            return await _context.Payments.FindAsync(new object[] { paymentId }, cancellationToken);
        }

        public async Task<Payment?> GetPaymentByIdWithFullDetailsAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            return await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s!.Contract)
                        .ThenInclude(c => c.CoworkingAddress)
                            .ThenInclude(a => a!.Coworking)
                .Include(p => p.Reservation)
                    .ThenInclude(r => r!.CoworkingAddress)
                        .ThenInclude(a => a!.Coworking)
                .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        }

        public async Task<Payment?> GetPaymentWithSubscriptionAndContractAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            return await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s!.Contract)
                        .ThenInclude(c => c!.CoworkingAddress)
                .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        }

        public async Task UpdatePaymentAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            _context.Payments.Update(payment);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<Payment>> GetPaymentsBySubscriptionIdAsync(int subscriptionId, CancellationToken cancellationToken = default)
        {
            return await _context.Payments
                .Where(p => p.SubscriptionId == subscriptionId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task AddPaymentHistoryAsync(PaymentHistory history, CancellationToken cancellationToken = default)
        {
            _context.PaymentHistories.Add(history);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<(List<PaymentHistory> Items, int TotalItems)> GetPaymentHistoriesByPaymentIdAsync(int paymentId, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.PaymentHistories
                .Include(h => h.ActionByUser)
                .Where(h => h.PaymentId == paymentId)
                .OrderByDescending(h => h.ActionAt);
            var totalItems = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            return (items, totalItems);
        }

        public async Task<List<PaymentHistory>> GetPaymentHistoriesByPaymentIdsAsync(IEnumerable<int> paymentIds, CancellationToken cancellationToken = default)
        {
            var ids = paymentIds.ToList();
            if (ids.Count == 0) return new List<PaymentHistory>();
            return await _context.PaymentHistories
                .Include(h => h.ActionByUser)
                .Where(h => ids.Contains(h.PaymentId))
                .ToListAsync(cancellationToken);
        }

        public async Task<int> GetPaymentHistoryCountAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            return await _context.PaymentHistories
                .Where(h => h.PaymentId == paymentId)
                .CountAsync(cancellationToken);
        }

        public async Task<PaymentHistory?> GetReceiptUploadedEntryAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            return await _context.PaymentHistories
                .Where(h => h.PaymentId == paymentId && h.Action == PaymentHistoryActions.ReceiptUploaded)
                .OrderByDescending(h => h.ActionAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<Reservation?> GetReservationByIdAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            return await _context.Reservations.FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);
        }

        public async Task<CoworkingPhoto?> GetMainPhotoByCoworkingIdAsync(int coworkingId, CancellationToken cancellationToken = default)
        {
            var main = await _context.CoworkingPhotos
                .Where(p => p.CoworkingId == coworkingId && p.IsMain)
                .OrderBy(p => p.DisplayOrder)
                .ThenByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (main != null) return main;
            return await _context.CoworkingPhotos
                .Where(p => p.CoworkingId == coworkingId)
                .OrderBy(p => p.DisplayOrder)
                .ThenByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<Location?> GetLocationByIdAsync(int locationId, CancellationToken cancellationToken = default)
        {
            return await _context.Locations.FirstOrDefaultAsync(l => l.Id == locationId, cancellationToken);
        }

        public async Task<CoworkingAddress?> GetCoworkingAddressWithDetailsAsync(int addressId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingAddresses
                .Include(a => a.Location)
                .Include(a => a.Coworking)
                .FirstOrDefaultAsync(a => a.Id == addressId, cancellationToken);
        }

        public async Task<CoworkingAddress?> GetCoworkingAddressByIdAsync(int addressId, CancellationToken cancellationToken = default)
        {
            return await _context.CoworkingAddresses.FirstOrDefaultAsync(a => a.Id == addressId, cancellationToken);
        }

        public async Task<AddressDeclaration?> GetAddressDeclarationByIdAsync(int declarationId, CancellationToken cancellationToken = default)
        {
            return await _context.AddressDeclarations
                .Include(d => d.Contract)
                    .ThenInclude(c => c.Service)
                .FirstOrDefaultAsync(d => d.Id == declarationId, cancellationToken);
        }

        public async Task<List<AddressDeclaration>> GetAddressDeclarationsByContractIdAsync(int contractId, CancellationToken cancellationToken = default)
        {
            return await _context.AddressDeclarations
                .Where(d => d.ContractId == contractId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<AddressDeclaration>> GetAddressDeclarationsByContractIdsWithContractAsync(IEnumerable<int> contractIds, CancellationToken cancellationToken = default)
        {
            var ids = contractIds.ToList();
            if (ids.Count == 0) return new List<AddressDeclaration>();
            return await _context.AddressDeclarations
                .Include(d => d.Contract)
                    .ThenInclude(c => c!.Service)
                .Where(d => ids.Contains(d.ContractId))
                .ToListAsync(cancellationToken);
        }

        public async Task<List<int>> GetContractIdsWithPaidPaymentsAsync(IEnumerable<int> contractIds, CancellationToken cancellationToken = default)
        {
            var ids = contractIds.ToList();
            if (ids.Count == 0) return new List<int>();
            return await _context.Payments
                .Include(p => p.Subscription)
                .Where(p => p.Subscription != null && ids.Contains(p.Subscription.ContractId) && p.Status == PaymentStatus.Paid)
                .Select(p => p.Subscription!.ContractId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Contract>> GetActiveContractsWithAddressAndLocationAsync(int clientId, CancellationToken cancellationToken = default)
        {
            return await _context.Contracts
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a!.Coworking)
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a!.Location)
                .Include(c => c.Service)
                .Where(c => c.ClientId == clientId && c.Status == ContractStatus.Active)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<Document?> GetDocumentByIdAsync(int documentId, CancellationToken cancellationToken = default)
        {
            return await _context.Documents.FindAsync(new object[] { documentId }, cancellationToken);
        }

        public async Task<List<CreditCard>> GetCreditCardsByClientIdAsync(int clientId, CancellationToken cancellationToken = default)
        {
            return await _context.CreditCards
                .Where(c => c.ClientId == clientId && c.IsActive)
                .OrderByDescending(c => c.IsDefault)
                .ThenByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<CreditCard?> GetCreditCardByIdAndClientIdAsync(int cardId, int clientId, CancellationToken cancellationToken = default)
        {
            return await _context.CreditCards.FirstOrDefaultAsync(c => c.Id == cardId && c.ClientId == clientId, cancellationToken);
        }

        public async Task AddCreditCardAsync(CreditCard creditCard, CancellationToken cancellationToken = default)
        {
            _context.CreditCards.Add(creditCard);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task RemoveCreditCardAsync(int cardId, int clientId, CancellationToken cancellationToken = default)
        {
            var card = await _context.CreditCards.FirstOrDefaultAsync(c => c.Id == cardId && c.ClientId == clientId, cancellationToken);
            if (card != null)
            {
                _context.CreditCards.Remove(card);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task SetDefaultCreditCardAsync(int clientId, int cardId, CancellationToken cancellationToken = default)
        {
            var defaultCards = await _context.CreditCards
                .Where(c => c.ClientId == clientId && c.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var c in defaultCards)
                c.IsDefault = false;
            var card = await _context.CreditCards.FirstOrDefaultAsync(c => c.Id == cardId && c.ClientId == clientId, cancellationToken);
            if (card != null)
            {
                card.IsDefault = true;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task ClearDefaultCreditCardsAsync(int clientId, CancellationToken cancellationToken = default)
        {
            var defaultCards = await _context.CreditCards
                .Where(c => c.ClientId == clientId && c.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var c in defaultCards)
                c.IsDefault = false;
            if (defaultCards.Count > 0)
                await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
