using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Serviço de aplicação para operações do Coworking que usam o DbContext.
    /// Centraliza todas as consultas e escritas que o CoworkingController fazia diretamente no ApplicationDbContext.
    /// </summary>
    public interface ICoworkingAppService
    {
        // Dashboard / Alerts
        Task<List<CoworkingDocument>> GetRejectedCoworkingDocumentsAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<int> GetPendingCoworkingDocumentsCountAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<List<CoworkingDocument>> GetPendingCoworkingDocumentsAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<int> GetPendingPaymentsWithoutFiscalNoteCountAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<int> GetRejectedFiscalNotesCountAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<List<CoworkingAddress>> GetCoworkingAddressesWithCapacityPendingOrRejectedAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<Coworking?> GetCoworkingWithAddressesAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<List<Contract>> GetContractsForDashboardAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<int> GetPendingPaymentsCountForCoworkingAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<int> GetPendingInterestsCountAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<int> GetConfirmedReservationsCountAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task DismissStatusNotificationAsync(int coworkingId, CancellationToken cancellationToken = default);

        // Register / User
        Task<User?> FindUserAsync(int userId, CancellationToken cancellationToken = default);
        Task<Coworking?> GetCoworkingByCnpjAsync(string cnpjClean, CancellationToken cancellationToken = default);
        void AddCoworkingPhoto(CoworkingPhoto photo);
        void AddCoworkingDocument(CoworkingDocument doc);
        Task<int> GetCoworkingOperatingHoursCountAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        void UpdateUser(User user);

        // MyAddresses
        Task<(List<CoworkingAddress> Items, int TotalItems)> GetCoworkingAddressesPagedAsync(int coworkingId, string? search, string? status, int? locationId, int page, int pageSize, CancellationToken cancellationToken = default);
        Task<List<Location>> GetActiveLocationsAsync(CancellationToken cancellationToken = default);
        Task<Dictionary<int, int>> GetActiveContractsCountByAddressAsync(IEnumerable<int> addressIds, CancellationToken cancellationToken = default);
        Task<bool> HasFiscalAddressAsync(int coworkingId, CancellationToken cancellationToken = default);

        // AddAddress / EditAddress
        Task<bool> HasRoomRentalAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<Location?> FindLocationAsync(int locationId, CancellationToken cancellationToken = default);
        Task<CoworkingAddress?> GetAddressWithLocationAsync(int addressId, int coworkingId, CancellationToken cancellationToken = default);
        Task<CoworkingAddress?> GetAddressWithLocationAndCapacityRequestsAsync(int addressId, int coworkingId, CancellationToken cancellationToken = default);
        Task<bool> HasActiveContractsForAddressAsync(int addressId, CancellationToken cancellationToken = default);
        Task<Service?> GetServiceByCodeAsync(string code, CancellationToken cancellationToken = default);
        Task<bool> PartnerServiceExistsAsync(int coworkingId, int serviceId, CancellationToken cancellationToken = default);
        void AddPartnerService(PartnerService partnerService);
        Task<CoworkingAddress?> GetExistingAddressWithCapacityRequestsAsync(int addressId, int coworkingId, CancellationToken cancellationToken = default);
        Task<CoworkingAddress?> GetExistingAddressWithLocationAsync(int addressId, int coworkingId, CancellationToken cancellationToken = default);

        // CardPrices
        Task<CoworkingAddress?> GetAddressWithCardPriceRequestsAsync(int addressId, int coworkingId, CancellationToken cancellationToken = default);
        void AddCardPriceChangeRequest(CardPriceChangeRequest request);

        // EditMaxContracts
        Task<CoworkingAddress?> GetAddressWithMaxContractsRequestsAsync(int addressId, int coworkingId, CancellationToken cancellationToken = default);
        void AddMaxContractsChangeRequest(MaxContractsChangeRequest request);

        // RequestCapacityChange
        Task<CoworkingAddress?> GetAddressForCapacityChangeAsync(int addressId, int coworkingId, CancellationToken cancellationToken = default);
        void AddCapacityChangeRequest(CapacityChangeRequest request);

        // ActiveContracts
        Task<(List<Contract> Items, int TotalItems)> GetContractsPagedAsync(int coworkingId, string? search, string? status, int? locationId, string? clientName, DateTime? startDateFrom, DateTime? startDateTo, int page, int pageSize, CancellationToken cancellationToken = default);
        Task<List<Location>> GetActiveLocationsOrderedAsync(CancellationToken cancellationToken = default);

        // ContractDetails
        Task<Contract?> GetContractDetailsAsync(int contractId, int coworkingId, CancellationToken cancellationToken = default);
        Task<List<AddressDeclaration>> GetAddressDeclarationsByContractAsync(int contractId, CancellationToken cancellationToken = default);
        Task<List<Payment>> GetPaymentsBySubscriptionIdAsync(int subscriptionId, CancellationToken cancellationToken = default);
        Task<Dictionary<int, List<PaymentHistory>>> GetPaymentHistoriesByPaymentIdsAsync(IEnumerable<int> paymentIds, CancellationToken cancellationToken = default);

        // DownloadDeclaration / DownloadContractDocument
        Task<AddressDeclaration?> GetDeclarationWithContractAsync(int declarationId, CancellationToken cancellationToken = default);
        Task<Document?> GetDocumentWithContractAsync(int documentId, CancellationToken cancellationToken = default);

        // Reservations
        Task<List<CoworkingAddress>> GetCoworkingAddressesForReservationsAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<List<Reservation>> GetReservationsFilteredAsync(int coworkingId, string? search, int? addressId, DateTime? allocationDateFrom, DateTime? allocationDateTo, string? status, CancellationToken cancellationToken = default);
        Task<List<Interest>> GetInterestsFilteredAsync(int coworkingId, string? search, int? addressId, DateTime? allocationDateFrom, DateTime? allocationDateTo, string? status, CancellationToken cancellationToken = default);

        // UpdateReservationStatus / ConfirmReservationAttended
        Task<Interest?> GetInterestWithAddressAsync(int interestId, int coworkingId, CancellationToken cancellationToken = default);
        Task<Reservation?> GetReservationWithAddressAsync(int reservationId, int coworkingId, CancellationToken cancellationToken = default);

        // RoomRevenueReport
        Task<List<Reservation>> GetReservationsForRevenueReportAsync(int coworkingId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default);
        Task<List<ReservationCommission>> GetReservationCommissionsByReservationIdsAsync(IEnumerable<int> reservationIds, CancellationToken cancellationToken = default);
        Task<List<Transfer>> GetTransfersByReservationIdsAsync(IEnumerable<int> reservationIds, CancellationToken cancellationToken = default);

        // Commissions
        Task<List<Commission>> GetCommissionsByCoworkingIdAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<List<Transfer>> GetTransfersByCoworkingIdAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<List<int>> GetPaymentIdsWithEmitidaFiscalNoteAsync(IEnumerable<int> paymentIds, CancellationToken cancellationToken = default);

        // Services (AddService / RemoveService)
        Task<List<string>> GetServiceNamesExceptFiscalAsync(CancellationToken cancellationToken = default);
        Task<Service?> FindServiceByIdAsync(int serviceId, CancellationToken cancellationToken = default);
        Task<PartnerService?> GetPartnerServiceAsync(int coworkingId, int serviceId, CancellationToken cancellationToken = default);
        Task<PartnerService?> GetPartnerServiceForRemovalAsync(int partnerServiceId, int coworkingId, CancellationToken cancellationToken = default);
        Task<List<int>> GetCoworkingAddressIdsAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<bool> HasActiveOrSuspendedContractsInAddressesAsync(IEnumerable<int> addressIds, CancellationToken cancellationToken = default);
        Task<List<int>> GetContractIdsByCoworkingAddressIdsAsync(IEnumerable<int> addressIds, CancellationToken cancellationToken = default);
        Task<List<int>> GetSubscriptionIdsByContractIdsAsync(IEnumerable<int> contractIds, CancellationToken cancellationToken = default);
        Task<bool> HasPaymentsAwaitingValidationInSubscriptionsAsync(IEnumerable<int> subscriptionIds, CancellationToken cancellationToken = default);

        // ServiceFiscalNotes
        Task<(List<Payment> Items, int TotalItems, decimal TotalAmount)> GetPaymentsWithoutFiscalNotePagedAsync(int coworkingId, string? serviceType, string? client, string? contractNumber, int page, int pageSize, CancellationToken cancellationToken = default);
        Task<List<int>> GetMatchingClientIdsForSearchAsync(string clientTerm, CancellationToken cancellationToken = default);
        Task<List<Commission>> GetCommissionsByPaymentIdsAsync(IEnumerable<int> paymentIds, CancellationToken cancellationToken = default);
        Task<bool> HasActiveSefazConfigurationAsync(int coworkingId, CancellationToken cancellationToken = default);

        // ServiceFiscalNoteDetail / IssueServiceFiscalNote
        Task<Payment?> GetPaymentForFiscalNoteDetailAsync(int paymentId, CancellationToken cancellationToken = default);
        Task<FiscalNote?> GetExistingEmitidaFiscalNoteAsync(int paymentId, CancellationToken cancellationToken = default);
        void AddFiscalNote(FiscalNote fiscalNote);
        void UpdateFiscalNote(FiscalNote fiscalNote);

        // FindCoworking for update
        Task<Coworking?> FindCoworkingAsync(int coworkingId, CancellationToken cancellationToken = default);
    }
}
