using KlyonHubPlatform.Application.DTOs.Admin;
using KlyonHubPlatform.Domain.Entities;
using System.Data.Common;
using KlyonHubPlatform.Infra.Data.Context;

namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Serviço de acesso ao banco para ações administrativas. Concentra todo uso de DbContext que antes existia no AdminController.
    /// </summary>
    public interface IAdminDbService
    {
        /// <summary>
        /// Remove duplicatas de PDF de contrato (Documents Type=Contract), entradas ContractGenerated no histórico
        /// e declarações válidas repetidas (mesmo ContractId + ServiceId + Version). Mantém o menor Id de cada grupo.
        /// Não apaga arquivos no Oracle — apenas registros no banco.
        /// </summary>
        Task<DuplicateContractArtifactsCleanupResult> CleanupDuplicateContractArtifactsAsync(
            bool dryRun = false,
            CancellationToken cancellationToken = default);

        Task<Payment?> GetPaymentForStatementAsync(int paymentId, CancellationToken cancellationToken = default);
        Task<Reservation?> GetReservationForStatementAsync(int reservationId, CancellationToken cancellationToken = default);
        Task<(Reservation? Reservation, ReservationCommission? Commission, Transfer? Transfer, List<AuditLog> AuditLogs)> GetReservationFinancialDetailAsync(int reservationId, CancellationToken cancellationToken = default);

        Task UpdateStorageToFtpAsync(CancellationToken cancellationToken = default);
        Task UpdateStorageToOracleAsync(CancellationToken cancellationToken = default);
        Task UpdateStorageToAwsAsync(CancellationToken cancellationToken = default);

        Task<bool> CreateAddressDeclarationsTableIfNotExistsAsync(CancellationToken cancellationToken = default);

        Task<(List<Payment> Items, int TotalCount)> GetPaymentsPagedAsync(string? search, string? status, int page, int pageSize, CancellationToken cancellationToken = default);
        Task<Payment?> GetPaymentForApproveAsync(int paymentId, CancellationToken cancellationToken = default);
        Task RejectPaymentAsync(int paymentId, int userId, string rejectionReason, CancellationToken cancellationToken = default);
        Task<(List<PaymentHistory> Items, int TotalCount)> GetPaymentHistoryPagedAsync(int paymentId, int page, int pageSize, CancellationToken cancellationToken = default);

        Task<IEnumerable<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default);
        Task<(bool Success, string Message)> RunMigrationsAsync(CancellationToken cancellationToken = default);
        Task ExecuteSqlRawAsync(string sql, CancellationToken cancellationToken = default);

        /// <summary>Executa uma ação com a conexão do banco (abre/fecha se necessário).</summary>
        Task RunWithConnectionAsync(Func<DbConnection, Task> action, CancellationToken cancellationToken = default);
        /// <summary>Executa uma ação com a conexão do banco e retorna resultado.</summary>
        Task<T> RunWithConnectionAsync<T>(Func<DbConnection, Task<T>> action, CancellationToken cancellationToken = default);

        Task<Coworking?> FindCoworkingAsync(int id, CancellationToken cancellationToken = default);
        Task<Service?> FindServiceAsync(int id, CancellationToken cancellationToken = default);
        Task<List<string>> GetCoworkingServiceNamesExcludingFiscalAsync(CancellationToken cancellationToken = default);

        Task<(bool Success, string? ErrorMessage)> SavePartnerServiceAsync(int id, int coworkingId, int serviceId, decimal percentage, CancellationToken cancellationToken = default);
        Task<(bool Success, string? ErrorMessage)> DeletePartnerServiceAsync(int id, int coworkingId, CancellationToken cancellationToken = default);
        Task<(List<PartnerService> Items, int TotalCount)> GetPartnerServicesRemovalRequestsPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
        Task<PartnerService?> GetPartnerServiceForRemovalAsync(int partnerServiceId, CancellationToken cancellationToken = default);
        Task RemovePartnerServiceAsync(PartnerService partnerService, CancellationToken cancellationToken = default);
        Task UpdatePartnerServiceRemovalRejectedAsync(PartnerService partnerService, int userId, string rejectionReason, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);

        Task<SefazConfiguration?> GetSefazConfigurationAsync(string state, CancellationToken cancellationToken = default);
        Task<List<FiscalNoteScriptExecution>> GetLastFiscalNoteExecutionsAsync(int take, CancellationToken cancellationToken = default);
        Task SaveSefazConfigurationAsync(SefazConfiguration config, CancellationToken cancellationToken = default);

        Task<bool> InitializeCreditPlansAsync(CancellationToken cancellationToken = default);

        Task AddRoomConfigurationColumnsAsync(CancellationToken cancellationToken = default);
        Task AddReservationColumnsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reseta a base corrente: limpa dados de negócio (whitelist de tabelas preservadas),
        /// aplica migrations pendentes se houver e executa seed idempotente.
        /// Preserva SystemConfigurations, SefazConfigurations, Roles, Languages, Locations, Services, etc. e o admin.
        /// </summary>
        Task<(bool Success, string Message)> ClearAndSeedDatabaseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Busca usuário por Id (tabela Users) para preview de exclusão em cascata.
        /// Bloqueia Admin; bloqueia Parceiro com clientes/contratos vinculados aos endereços.
        /// </summary>
        Task<UserPurgeLookupDto> LookupUserForPurgeAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Apaga todos os dados de negócio do Cliente ou Parceiro (UserId).
        /// Não apaga Administradores. Parceiro só se não houver contratos de clientes nos endereços.
        /// </summary>
        Task<UserPurgeResultDto> PurgeUserDataAsync(int userId, CancellationToken cancellationToken = default);

        Task<(bool IsQuery, string Result)> ExecuteSqlForAdminAsync(string sqlQuery, CancellationToken cancellationToken = default);
        Task<string> GetVerifyCoworkingOperatingHoursResultAsync(CancellationToken cancellationToken = default);

        Task<List<string>> AddTransfersIndexAndFkAsync(CancellationToken cancellationToken = default);
        Task<List<string>> FixCreditPurchaseColumnsAsync(CancellationToken cancellationToken = default);
        Task<bool> CreateInterestsTableIfNotExistsAsync(CancellationToken cancellationToken = default);
        Task<bool> CreateCoworkingProductsTableIfNotExistsAsync(CancellationToken cancellationToken = default);
        Task<bool> CreateCoworkingOperatingHoursTableIfNotExistsAsync(CancellationToken cancellationToken = default);
        Task<bool> CreateCreditPlansTableIfNotExistsAsync(CancellationToken cancellationToken = default);

        /// <summary>Atualiza ou adiciona configurações de sistema (ex.: Asaas sandbox).</summary>
        Task UpdateOrAddSystemConfigurationsAsync(IEnumerable<(string Key, string Value, string Description, bool IsSensitive)> configs, string category, string? updatedBy, CancellationToken cancellationToken = default);

        /// <summary>Executa uma ação com o DbContext. Use apenas para fluxos complexos que ainda não foram extraídos para métodos específicos.</summary>
        Task ExecuteWithContextAsync(Func<ApplicationDbContext, Task> action, CancellationToken cancellationToken = default);

        /// <summary>Retorna o DbContext do escopo atual. Preferir métodos específicos ou ExecuteWithContextAsync quando possível.</summary>
        ApplicationDbContext GetContext();
    }
}
