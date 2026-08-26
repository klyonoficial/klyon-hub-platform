using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Serviço de aplicação para o fluxo do cliente. Concentra acesso a dados que antes usavam DbContext no ClientController.
    /// </summary>
    public interface IClientAppService
    {
        Task<Client?> GetClientByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<Client?> GetClientByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<Client?> GetClientByIdWithUserAsync(int id, CancellationToken cancellationToken = default);
        Task<Client?> GetClientByUserIdAsNoTrackingAsync(int userId, CancellationToken cancellationToken = default);
        Task<Client?> GetClientByUserIdTrackingAsync(int userId, CancellationToken cancellationToken = default);
        Task<List<Client>> GetClientsWithCnpjExcludingAsync(int excludeId, CancellationToken cancellationToken = default);
        Task<List<Client>> GetClientsWithCpfExcludingAsync(int excludeId, CancellationToken cancellationToken = default);
        Task<bool> ClientExistsWithCnpjAsync(string cnpjClean, int? excludeClientId, CancellationToken cancellationToken = default);
        Task<bool> ClientExistsWithCpfAsync(string cpfClean, int? excludeClientId, CancellationToken cancellationToken = default);
        Task AddClientAsync(Client client, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
        /// <summary>Persiste o perfil do cliente, registra o evento no histórico dos contratos ativos e tenta sincronizar o customer no Asaas se já existir.</summary>
        /// <returns>Aviso não-bloqueante se o sync Asaas falhou; null se o perfil foi salvo (incluindo quando ainda não há customer no Asaas).</returns>
        Task<string?> SaveClientProfileAsync(int clientId, int actingUserId, CancellationToken cancellationToken = default);

        Task<User?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default);

        Task<List<int>> GetContractAddressIdsByClientIdAsync(int clientId, CancellationToken cancellationToken = default);
        Task<int?> GetActiveContractIdByClientIdAsync(int clientId, CancellationToken cancellationToken = default);
        Task<List<Contract>> GetPendingContractsWithSubscriptionPaymentsAsync(int clientId, CancellationToken cancellationToken = default);
        Task PromotePendingContractsToActiveIfPaidAsync(int clientId, CancellationToken cancellationToken = default);
        Task<(List<Contract> Items, int TotalItems)> GetMyContractsAsync(int clientId, string? search, string? status, int page, int pageSize, CancellationToken cancellationToken = default);
        Task<Contract?> GetContractByIdAsync(int contractId, CancellationToken cancellationToken = default);
        Task<Contract?> GetContractByIdAndClientIdAsync(int contractId, int clientId, CancellationToken cancellationToken = default);
        Task<Contract?> GetExistingContractForAddressAsync(int clientId, int coworkingAddressId, CancellationToken cancellationToken = default);
        Task<Contract?> GetExistingContractInLocationAsync(int clientId, int locationId, CancellationToken cancellationToken = default);
        Task UpdateContractAsync(Contract contract, CancellationToken cancellationToken = default);

        Task<Subscription?> GetSubscriptionByContractIdAsync(int contractId, CancellationToken cancellationToken = default);
        Task<Subscription?> GetSubscriptionWithContractAsync(int subscriptionId, CancellationToken cancellationToken = default);
        Task<List<Subscription>> GetSubscriptionsByContractIdsAsync(IEnumerable<int> contractIds, CancellationToken cancellationToken = default);
        Task<List<int>> GetSubscriptionIdsByContractIdAsync(int contractId, CancellationToken cancellationToken = default);
        Task<Payment?> GetFirstPaymentBySubscriptionIdAsync(int subscriptionId, CancellationToken cancellationToken = default);
        Task<Payment?> GetPaymentAwaitingValidationBySubscriptionIdsAsync(IEnumerable<int> subscriptionIds, CancellationToken cancellationToken = default);

        Task<Payment?> GetPaymentByIdAsync(int paymentId, CancellationToken cancellationToken = default);
        Task<Payment?> GetPaymentByIdWithFullDetailsAsync(int paymentId, CancellationToken cancellationToken = default);
        Task<Payment?> GetPaymentWithSubscriptionAndContractAsync(int paymentId, CancellationToken cancellationToken = default);
        Task UpdatePaymentAsync(Payment payment, CancellationToken cancellationToken = default);
        Task<List<Payment>> GetPaymentsBySubscriptionIdAsync(int subscriptionId, CancellationToken cancellationToken = default);

        Task AddPaymentHistoryAsync(PaymentHistory history, CancellationToken cancellationToken = default);
        Task<(List<PaymentHistory> Items, int TotalItems)> GetPaymentHistoriesByPaymentIdAsync(int paymentId, int page, int pageSize, CancellationToken cancellationToken = default);
        Task<List<PaymentHistory>> GetPaymentHistoriesByPaymentIdsAsync(IEnumerable<int> paymentIds, CancellationToken cancellationToken = default);
        Task<int> GetPaymentHistoryCountAsync(int paymentId, CancellationToken cancellationToken = default);
        Task<PaymentHistory?> GetReceiptUploadedEntryAsync(int paymentId, CancellationToken cancellationToken = default);

        Task<Reservation?> GetReservationByIdAsync(int reservationId, CancellationToken cancellationToken = default);
        Task<CoworkingPhoto?> GetMainPhotoByCoworkingIdAsync(int coworkingId, CancellationToken cancellationToken = default);
        Task<Location?> GetLocationByIdAsync(int locationId, CancellationToken cancellationToken = default);
        Task<CoworkingAddress?> GetCoworkingAddressWithDetailsAsync(int addressId, CancellationToken cancellationToken = default);
        Task<CoworkingAddress?> GetCoworkingAddressByIdAsync(int addressId, CancellationToken cancellationToken = default);
        Task<AddressDeclaration?> GetAddressDeclarationByIdAsync(int declarationId, CancellationToken cancellationToken = default);
        Task<List<AddressDeclaration>> GetAddressDeclarationsByContractIdAsync(int contractId, CancellationToken cancellationToken = default);
        Task<List<AddressDeclaration>> GetAddressDeclarationsByContractIdsWithContractAsync(IEnumerable<int> contractIds, CancellationToken cancellationToken = default);
        Task<List<int>> GetContractIdsWithPaidPaymentsAsync(IEnumerable<int> contractIds, CancellationToken cancellationToken = default);
        Task<List<Contract>> GetActiveContractsWithAddressAndLocationAsync(int clientId, CancellationToken cancellationToken = default);
        Task<Document?> GetDocumentByIdAsync(int documentId, CancellationToken cancellationToken = default);
        Task<List<CreditCard>> GetCreditCardsByClientIdAsync(int clientId, CancellationToken cancellationToken = default);
        Task<CreditCard?> GetCreditCardByIdAndClientIdAsync(int cardId, int clientId, CancellationToken cancellationToken = default);
        Task AddCreditCardAsync(CreditCard creditCard, CancellationToken cancellationToken = default);
        Task<int> CountActiveCreditCardsAsync(int clientId, CancellationToken cancellationToken = default);
        Task SetCreditCardAsaasTokenAsync(int cardId, int clientId, string token, string? postalCode = null, string? addressNumber = null, CancellationToken cancellationToken = default);
        Task RemoveCreditCardAsync(int cardId, int clientId, CancellationToken cancellationToken = default);
        Task<bool> UpdateCreditCardNicknameAsync(int cardId, int clientId, string nickname, CancellationToken cancellationToken = default);
        Task SetDefaultCreditCardAsync(int clientId, int cardId, CancellationToken cancellationToken = default);
        Task ClearDefaultCreditCardsAsync(int clientId, CancellationToken cancellationToken = default);
    }
}
