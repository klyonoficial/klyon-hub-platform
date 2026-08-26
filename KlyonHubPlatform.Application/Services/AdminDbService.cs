using System.Data.Common;
using KlyonHubPlatform.Application.Constants;
using KlyonHubPlatform.Application.DTOs.Admin;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Helpers;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using StorageConfigKeys = KlyonHubPlatform.Domain.Core.Constants.StorageConfigKeys;

namespace KlyonHubPlatform.Application.Services
{
    public class AdminDbService : IAdminDbService
    {
        private readonly ApplicationDbContext _context;

        public AdminDbService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Payment?> GetPaymentForStatementAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            return await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s!.Contract)
                        .ThenInclude(c => c!.CoworkingAddress)
                            .ThenInclude(a => a!.Location)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s!.Contract)
                        .ThenInclude(c => c!.Client)
                .Include(p => p.Reservation)
                    .ThenInclude(r => r!.CoworkingAddress)
                        .ThenInclude(a => a!.Coworking)
                .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        }

        public async Task<Reservation?> GetReservationForStatementAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            return await _context.Reservations
                .Include(r => r.Client)
                .Include(r => r.CoworkingAddress)
                    .ThenInclude(a => a.Coworking)
                .Include(r => r.CoworkingRoom)
                .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);
        }

        public async Task<(Reservation? Reservation, ReservationCommission? Commission, Transfer? Transfer, List<AuditLog> AuditLogs)> GetReservationFinancialDetailAsync(int reservationId, CancellationToken cancellationToken = default)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Client!)
                    .ThenInclude(c => c.User)
                .Include(r => r.CoworkingAddress!)
                    .ThenInclude(a => a.Coworking)
                .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);

            ReservationCommission? commission = null;
            try
            {
                commission = await _context.ReservationCommissions
                    .FirstOrDefaultAsync(rc => rc.ReservationId == reservationId, cancellationToken);
            }
            catch { commission = null; }

            Transfer? transfer = null;
            try
            {
                transfer = await _context.Transfers
                    .FirstOrDefaultAsync(t => t.ReservationId == reservationId, cancellationToken);
            }
            catch { transfer = null; }

            List<AuditLog> auditLogs;
            try
            {
                auditLogs = await _context.AuditLogs
                    .Where(a => a.EntityType == "Reservation" && a.EntityId == reservationId)
                    .OrderByDescending(a => a.ActionDate)
                    .ToListAsync(cancellationToken);
            }
            catch
            {
                auditLogs = new List<AuditLog>();
            }

            return (reservation, commission, transfer, auditLogs);
        }

        public async Task UpdateStorageToFtpAsync(CancellationToken cancellationToken = default)
        {
            var storageProvider = await _context.SystemConfigurations.FirstOrDefaultAsync(c => c.Key == StorageConfigKeys.Provider, cancellationToken);
            if (storageProvider != null)
            {
                storageProvider.Value = FtpDefaults.ProviderName;
                storageProvider.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.SystemConfigurations.Add(new SystemConfiguration
                {
                    Key = StorageConfigKeys.Provider,
                    Value = FtpDefaults.ProviderName,
                    Description = "Provedor de armazenamento: Oracle, FTP ou AWS",
                    Category = SystemConfigurationCategoryKeys.Storage,
                    IsSensitive = false,
                    IsEncrypted = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
            var ftpConfigs = new (FtpConfigKey Key, string Value)[]
            {
                (FtpConfigKey.Host, "187.110.161.110"),
                (FtpConfigKey.Port, FtpDefaults.PortString),
                (FtpConfigKey.Username, FtpDefaults.Username),
                (FtpConfigKey.Password, FtpDefaults.Password),
                (FtpConfigKey.UsePassiveMode, FtpDefaults.UsePassive),
                (FtpConfigKey.UseSSL, FtpDefaults.UseSsl)
            };
            foreach (var (key, value) in ftpConfigs)
            {
                var configKey = key.ToConfigKey();
                var existing = await _context.SystemConfigurations.FirstOrDefaultAsync(c => c.Key == configKey, cancellationToken);
                if (existing != null)
                {
                    existing.Value = value;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.SystemConfigurations.Add(new SystemConfiguration
                    {
                        Key = configKey,
                        Value = value,
                        Description = key.GetDescription(),
                        Category = SystemConfigurationCategoryKeys.Storage,
                        IsSensitive = key == FtpConfigKey.Password,
                        IsEncrypted = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateStorageToOracleAsync(CancellationToken cancellationToken = default)
        {
            var storageProvider = await _context.SystemConfigurations.FirstOrDefaultAsync(c => c.Key == StorageConfigKeys.Provider, cancellationToken);
            if (storageProvider != null)
            {
                storageProvider.Value = OracleStorageDefaults.ProviderName;
                storageProvider.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.SystemConfigurations.Add(new SystemConfiguration
                {
                    Key = StorageConfigKeys.Provider,
                    Value = OracleStorageDefaults.ProviderName,
                    Description = "Provedor de armazenamento: Oracle, FTP ou AWS",
                    Category = SystemConfigurationCategoryKeys.Storage,
                    IsSensitive = false,
                    IsEncrypted = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateStorageToAwsAsync(CancellationToken cancellationToken = default)
        {
            var storageProvider = await _context.SystemConfigurations.FirstOrDefaultAsync(c => c.Key == StorageConfigKeys.Provider, cancellationToken);
            if (storageProvider != null)
            {
                storageProvider.Value = StorageProviderNames.Aws;
                storageProvider.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.SystemConfigurations.Add(new SystemConfiguration
                {
                    Key = StorageConfigKeys.Provider,
                    Value = StorageProviderNames.Aws,
                    Description = "Provedor de armazenamento: Oracle, FTP ou AWS",
                    Category = SystemConfigurationCategoryKeys.Storage,
                    IsSensitive = false,
                    IsEncrypted = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> CreateAddressDeclarationsTableIfNotExistsAsync(CancellationToken cancellationToken = default)
        {
            var tableExists = false;
            await RunWithConnectionAsync(async connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT COUNT(*) 
                    FROM information_schema.tables 
                    WHERE table_schema = DATABASE() 
                    AND table_name = 'AddressDeclarations'";
                var result = await command.ExecuteScalarAsync(cancellationToken);
                tableExists = Convert.ToInt32(result) > 0;
            }, cancellationToken);

            if (tableExists) return false;

            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS `AddressDeclarations` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `ContractId` int NOT NULL,
                    `DeclarationNumber` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
                    `Version` int NOT NULL,
                    `StartDate` datetime(6) NOT NULL,
                    `EndDate` datetime(6) NOT NULL,
                    `FilePath` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
                    `DigitalSignature` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
                    `IsValid` tinyint(1) NOT NULL,
                    `InvalidatedAt` datetime(6) NULL,
                    `InvalidationReason` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    `UpdatedAt` datetime(6) NULL,
                    PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_AddressDeclarations_Contracts_ContractId` FOREIGN KEY (`ContractId`) REFERENCES `Contracts` (`Id`) ON DELETE CASCADE,
                    INDEX `IX_AddressDeclarations_ContractId_Version` (`ContractId`, `Version`),
                    INDEX `IX_AddressDeclarations_DeclarationNumber` (`DeclarationNumber`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
            await _context.Database.ExecuteSqlRawAsync(createTableSql, cancellationToken);
            return true;
        }

        public async Task<(List<Payment> Items, int TotalCount)> GetPaymentsPagedAsync(string? search, string? status, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.Payments
                .Include(p => p.Subscription!)
                    .ThenInclude(s => s.Contract!)
                        .ThenInclude(c => c.Client!)
                            .ThenInclude(cl => cl.User)
                .Include(p => p.Subscription!)
                    .ThenInclude(s => s.Contract!)
                        .ThenInclude(c => c.CoworkingAddress)
                .AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p =>
                    p.ExternalPaymentId.Contains(search) ||
                    (p.TransactionId != null && p.TransactionId.Contains(search)) ||
                    (p.Subscription != null && p.Subscription.Contract != null && p.Subscription.Contract.ContractNumber != null && p.Subscription.Contract.ContractNumber.Contains(search)) ||
                    (p.Subscription != null && p.Subscription.Contract != null && p.Subscription.Contract.Client != null && (
                        (p.Subscription.Contract.Client.RazaoSocial != null && p.Subscription.Contract.Client.RazaoSocial.Contains(search)) ||
                        (p.Subscription.Contract.Client.NomeFantasia != null && p.Subscription.Contract.Client.NomeFantasia.Contains(search)) ||
                        (p.Subscription.Contract.Client.ContactName != null && p.Subscription.Contract.Client.ContactName.Contains(search)) ||
                        (p.Subscription.Contract.Client.User != null && p.Subscription.Contract.Client.User.FullName != null && p.Subscription.Contract.Client.User.FullName.Contains(search)))));
            }
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<PaymentStatus>(status, out var paymentStatus))
                query = query.Where(p => p.Status == paymentStatus);
            query = query.OrderByDescending(p => p.CreatedAt);
            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
            return (items, totalCount);
        }

        public async Task<Payment?> GetPaymentForApproveAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            return await _context.Payments
                .Include(p => p.Subscription!)
                    .ThenInclude(s => s!.Contract)
                .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        }

        public async Task RejectPaymentAsync(int paymentId, int userId, string rejectionReason, CancellationToken cancellationToken = default)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                var payment = await _context.Payments
                    .Include(p => p.Subscription!)
                        .ThenInclude(s => s!.Contract)
                    .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
                if (payment == null) throw new InvalidOperationException("Pagamento não encontrado.");
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    _context.PaymentHistories.Add(new PaymentHistory
                    {
                        PaymentId = paymentId,
                        Action = PaymentHistoryActions.Rejected,
                        RejectionReason = rejectionReason,
                        ActionBy = userId,
                        ActionAt = DateTime.UtcNow
                    });
                    payment.Status = PaymentStatus.Pending;
                    payment.UpdatedAt = DateTime.UtcNow;
                    payment.ReceiptFilePath = null;
                    _context.Payments.Update(payment);
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }

        public async Task<(List<PaymentHistory> Items, int TotalCount)> GetPaymentHistoryPagedAsync(int paymentId, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.PaymentHistories
                .Include(h => h.ActionByUser)
                    .ThenInclude(u => u!.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(h => h.PaymentId == paymentId)
                .OrderByDescending(h => h.ActionAt);
            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
            return (items, totalCount);
        }

        public async Task<IEnumerable<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default)
            => await _context.Database.GetPendingMigrationsAsync(cancellationToken);

        public async Task<IEnumerable<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default)
            => await _context.Database.GetAppliedMigrationsAsync(cancellationToken);

        public async Task<(bool Success, string Message)> RunMigrationsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                try
                {
                    await _context.Database.ExecuteSqlRawAsync("SELECT 1 FROM CreditPlans LIMIT 1", cancellationToken);
                    var pendingCheck = await _context.Database.GetPendingMigrationsAsync(cancellationToken);
                    if (pendingCheck.Contains("20260124000542_AddCoworkingSefazConfiguration"))
                    {
                        await _context.Database.ExecuteSqlRawAsync("INSERT IGNORE INTO `__EFMigrationsHistory` (MigrationId, ProductVersion) VALUES ('20260124000542_AddCoworkingSefazConfiguration', '8.0.0')", cancellationToken);
                    }
                }
                catch { }
                var pendingMigrations = (await _context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pendingMigrations.Count == 0)
                    return (false, AdminResultCodes.NoPendingMigrations);
                await _context.Database.MigrateAsync(cancellationToken);
                return (true, string.Join(", ", pendingMigrations));
            }
            catch (Exception ex)
            {
                var msg = ex.Message + (ex.InnerException != null ? " | " + ex.InnerException.Message : "");
                if ((msg.Contains("already exists") || (msg.Contains("Table") && msg.Contains("exists"))) &&
                    (msg.Contains("creditplans", StringComparison.OrdinalIgnoreCase) || msg.Contains("CreditPlans")))
                {
                    try
                    {
                        await _context.Database.ExecuteSqlRawAsync("INSERT IGNORE INTO `__EFMigrationsHistory` (MigrationId, ProductVersion) VALUES ('20260124000542_AddCoworkingSefazConfiguration', '8.0.0')", cancellationToken);
                        return (false, AdminResultCodes.MigrationMarkedAsApplied);
                    }
                    catch { }
                }
                return (false, msg);
            }
        }

        public async Task ExecuteSqlRawAsync(string sql, CancellationToken cancellationToken = default)
            => await _context.Database.ExecuteSqlRawAsync(sql, cancellationToken);

        public async Task RunWithConnectionAsync(Func<DbConnection, Task> action, CancellationToken cancellationToken = default)
        {
            var connection = _context.Database.GetDbConnection();
            var wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen) await connection.OpenAsync(cancellationToken);
            try
            {
                await action(connection);
            }
            finally
            {
                if (!wasOpen) await connection.CloseAsync();
            }
        }

        public async Task<T> RunWithConnectionAsync<T>(Func<DbConnection, Task<T>> action, CancellationToken cancellationToken = default)
        {
            var connection = _context.Database.GetDbConnection();
            var wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen) await connection.OpenAsync(cancellationToken);
            try
            {
                return await action(connection);
            }
            finally
            {
                if (!wasOpen) await connection.CloseAsync();
            }
        }

        public async Task<Coworking?> FindCoworkingAsync(int id, CancellationToken cancellationToken = default)
            => await _context.Coworkings.FindAsync(new object[] { id }, cancellationToken);

        public async Task<Service?> FindServiceAsync(int id, CancellationToken cancellationToken = default)
            => await _context.Services.FindAsync(new object[] { id }, cancellationToken);

        public async Task<List<string>> GetCoworkingServiceNamesExcludingFiscalAsync(CancellationToken cancellationToken = default)
            => await _context.Services.Where(x => !x.Code.Contains(ServiceCodes.FiscalAddressCodePrefix)).Select(x => x.Name).ToListAsync(cancellationToken);

        public async Task<(bool Success, string? ErrorMessage)> SavePartnerServiceAsync(int id, int coworkingId, int serviceId, decimal percentage, CancellationToken cancellationToken = default)
        {
            if (id > 0)
            {
                var existing = await _context.PartnerServices.FindAsync(new object[] { id }, cancellationToken);
                if (existing == null || existing.CoworkingId != coworkingId) return (false, AdminResultCodes.PartnerServiceNotFound);
                existing.ServiceId = serviceId;
                existing.Percentage = percentage;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var exists = await _context.PartnerServices.AnyAsync(ps => ps.CoworkingId == coworkingId && ps.ServiceId == serviceId, cancellationToken);
                if (exists) return (false, AdminResultCodes.ServiceAlreadyRegisteredForPartner);
                _context.PartnerServices.Add(new PartnerService
                {
                    CoworkingId = coworkingId,
                    ServiceId = serviceId,
                    Percentage = percentage,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> DeletePartnerServiceAsync(int id, int coworkingId, CancellationToken cancellationToken = default)
        {
            var partnerService = await _context.PartnerServices.FindAsync(new object[] { id }, cancellationToken);
            if (partnerService == null || partnerService.CoworkingId != coworkingId) return (false, AdminResultCodes.PartnerServiceNotFound);
            _context.PartnerServices.Remove(partnerService);
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null);
        }

        public async Task<(List<PartnerService> Items, int TotalCount)> GetPartnerServicesRemovalRequestsPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.PartnerServices
                .Include(ps => ps.Coworking!)
                    .ThenInclude(c => c.User)
                .Include(ps => ps.Service)
                .Where(ps => ps.RemovalRequestStatus == RemovalRequestStatus.Pending)
                .OrderByDescending(ps => ps.RemovalRequestedAt);
            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
            return (items, totalCount);
        }

        public async Task<PartnerService?> GetPartnerServiceForRemovalAsync(int partnerServiceId, CancellationToken cancellationToken = default)
            => await _context.PartnerServices
                .Include(ps => ps.Service)
                .Include(ps => ps.Coworking)
                .FirstOrDefaultAsync(ps => ps.Id == partnerServiceId, cancellationToken);

        public async Task RemovePartnerServiceAsync(PartnerService partnerService, CancellationToken cancellationToken = default)
        {
            _context.PartnerServices.Remove(partnerService);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdatePartnerServiceRemovalRejectedAsync(PartnerService partnerService, int userId, string rejectionReason, CancellationToken cancellationToken = default)
        {
            partnerService.RemovalRequestStatus = RemovalRequestStatus.Rejected;
            partnerService.RemovalReviewedAt = DateTime.UtcNow;
            partnerService.RemovalReviewedBy = userId;
            partnerService.RemovalRejectionReason = rejectionReason.Trim();
            partnerService.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => await _context.SaveChangesAsync(cancellationToken);

        public async Task<SefazConfiguration?> GetSefazConfigurationAsync(string state, CancellationToken cancellationToken = default)
        {
            var config = await _context.SefazConfigurations
                .Where(c => c.State == state)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            return config;
        }

        public async Task<List<FiscalNoteScriptExecution>> GetLastFiscalNoteExecutionsAsync(int take, CancellationToken cancellationToken = default)
            => await _context.FiscalNoteScriptExecutions
                .OrderByDescending(e => e.ExecutedAt)
                .Take(take)
                .ToListAsync(cancellationToken);

        public async Task SaveSefazConfigurationAsync(SefazConfiguration config, CancellationToken cancellationToken = default)
        {
            if (config.Id > 0)
            {
                var existing = await _context.SefazConfigurations.FindAsync(new object[] { config.Id }, cancellationToken);
                if (existing != null)
                {
                    existing.State = config.State;
                    existing.Environment = config.Environment;
                    existing.PlatformCNPJ = config.PlatformCNPJ;
                    existing.PlatformMunicipalRegistration = config.PlatformMunicipalRegistration;
                    existing.CertificatePath = config.CertificatePath;
                    existing.CertificatePassword = config.CertificatePassword;
                    if (!string.IsNullOrEmpty(config.CertificateContent)) existing.CertificateContent = config.CertificateContent;
                    existing.IntermediationServiceCode = config.IntermediationServiceCode;
                    existing.IntermediationServiceDescription = config.IntermediationServiceDescription;
                    existing.PlatformCNAE = config.PlatformCNAE;
                    existing.TaxRegime = config.TaxRegime;
                    existing.ISSAliquot = config.ISSAliquot;
                    existing.ISSMunicipality = config.ISSMunicipality;
                    existing.HomologationUrl = config.HomologationUrl;
                    existing.ProductionUrl = config.ProductionUrl;
                    existing.ApiToken = config.ApiToken;
                    existing.ApiKey = config.ApiKey;
                    existing.TimeoutSeconds = config.TimeoutSeconds;
                    existing.IsActive = config.IsActive;
                    existing.Notes = config.Notes;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                config.CreatedAt = DateTime.UtcNow;
                _context.SefazConfigurations.Add(config);
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> InitializeCreditPlansAsync(CancellationToken cancellationToken = default)
        {
            var existingPlans = await _context.CreditPlans.AnyAsync(cancellationToken);
            if (existingPlans) return false;
            var plans = new List<CreditPlan>
            {
                new CreditPlan { Name = CreditPlanNames.Basico, Price = 300m, Credits = 300, BonusCredits = 0, Period = CreditPlanPeriods.Mensal, ValidityMonths = 6, IsActive = true },
                new CreditPlan { Name = CreditPlanNames.Profissional, Price = 500m, Credits = 520, BonusCredits = 20, Period = CreditPlanPeriods.Mensal, ValidityMonths = 6, IsActive = true },
                new CreditPlan { Name = CreditPlanNames.Business, Price = 1000m, Credits = 1080, BonusCredits = 80, Period = CreditPlanPeriods.Mensal, ValidityMonths = 6, IsActive = true }
            };
            _context.CreditPlans.AddRange(plans);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task AddRoomConfigurationColumnsAsync(CancellationToken cancellationToken = default)
        {
            await RunWithConnectionAsync(async connection =>
            {
                var columns = new[] { ("OffersSharedRoom", "BOOLEAN DEFAULT FALSE"), ("SharedRoomPricePerHour", "DECIMAL(10,2) NULL"), ("SharedRoomMaxSeats", "INT NULL"), ("OffersPrivateRoom", "BOOLEAN DEFAULT FALSE"), ("PrivateRoomPricePerHour", "DECIMAL(10,2) NULL"), ("PrivateRoomMaxSeats", "INT NULL") };
                foreach (var (columnName, columnType) in columns)
                {
                    try
                    {
                        using var checkCommand = connection.CreateCommand();
                        checkCommand.CommandText = $@"SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CoworkingAddresses' AND COLUMN_NAME = '{columnName}'";
                        var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync(cancellationToken)) > 0;
                        if (!exists)
                        {
                            using var addCommand = connection.CreateCommand();
                            addCommand.CommandText = $"ALTER TABLE CoworkingAddresses ADD COLUMN {columnName} {columnType}";
                            await addCommand.ExecuteNonQueryAsync(cancellationToken);
                        }
                    }
                    catch (Exception ex) when (ex.Message.Contains("Duplicate column name")) { }
                }
            }, cancellationToken);
        }

        public async Task AddReservationColumnsAsync(CancellationToken cancellationToken = default)
        {
            await RunWithConnectionAsync(async connection =>
            {
                var columns = new[] { ("RoomType", "VARCHAR(20) DEFAULT 'Shared'"), ("TotalValue", "DECIMAL(10,2) NOT NULL DEFAULT 0") };
                foreach (var (columnName, columnType) in columns)
                {
                    try
                    {
                        using var checkCommand = connection.CreateCommand();
                        checkCommand.CommandText = $@"SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Reservations' AND COLUMN_NAME = '{columnName}'";
                        var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync(cancellationToken)) > 0;
                        if (!exists)
                        {
                            using var addCommand = connection.CreateCommand();
                            addCommand.CommandText = $"ALTER TABLE Reservations ADD COLUMN {columnName} {columnType}";
                            await addCommand.ExecuteNonQueryAsync(cancellationToken);
                        }
                    }
                    catch (Exception ex) when (ex.Message.Contains("Duplicate column name")) { }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Tabelas PRESERVADAS no reset (configuração / sistema / catálogo seed).
        /// Não apagar: SystemConfigurations, SefazConfigurations, Roles, Languages, Translations,
        /// Locations, LocationCommissions, Services, CreditPlans, ChatFaqItems, ChatIntentions,
        /// __EFMigrationsHistory. Users/UserRoles: apenas admin@klyon.com (tratamento parcial).
        /// Demais tabelas = dados de negócio → limpas.
        /// </summary>
        private static readonly HashSet<string> DatabaseResetPreservedTables = new(StringComparer.OrdinalIgnoreCase)
        {
            "__EFMigrationsHistory",
            "SystemConfigurations",
            "SefazConfigurations",
            "Roles",
            "Languages",
            "Translations",
            "Locations",
            "LocationCommissions",
            "Services",
            "CreditPlans",
            "ChatFaqItems",
            "ChatIntentions",
        };

        /// <summary>Users/UserRoles: limpeza parcial (mantém admin seed).</summary>
        private static readonly HashSet<string> DatabaseResetPartialPreserveTables = new(StringComparer.OrdinalIgnoreCase)
        {
            "Users",
            "UserRoles",
        };

        private const string DatabaseResetAdminEmail = "admin@klyon.com";

        public async Task<(bool Success, string Message)> ClearAndSeedDatabaseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;", cancellationToken);
                var clearedTables = new List<string>();
                var errors = new List<string>();

                var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == DatabaseResetAdminEmail, cancellationToken);
                var adminUserRolePairs = new List<(int UserId, int RoleId)>();
                if (adminUser != null)
                {
                    var adminRoles = await _context.UserRoles.Where(ur => ur.UserId == adminUser.Id).ToListAsync(cancellationToken);
                    adminUserRolePairs = adminRoles.Select(ur => (ur.UserId, ur.RoleId)).ToList();
                }

                // Whitelist: limpa todas as tabelas do schema exceto as preservadas (e Users/UserRoles, tratados à parte).
                var allTables = await RunWithConnectionAsync(async connection =>
                {
                    var names = new List<string>();
                    using var listCmd = connection.CreateCommand();
                    listCmd.CommandText = @"SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE'";
                    await using var reader = await listCmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                        names.Add(reader.GetString(0));
                    return names;
                }, cancellationToken);

                var tablesToClear = allTables
                    .Where(t => !DatabaseResetPreservedTables.Contains(t) && !DatabaseResetPartialPreserveTables.Contains(t))
                    .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                await RunWithConnectionAsync(async connection =>
                {
                    foreach (var tableName in tablesToClear)
                    {
                        var escaped = tableName.Replace("`", "``");
                        try
                        {
                            using var deleteCmd = connection.CreateCommand();
                            deleteCmd.CommandText = $"DELETE FROM `{escaped}`;";
                            var rowsDeleted = await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
                            try
                            {
                                using var resetCmd = connection.CreateCommand();
                                resetCmd.CommandText = $"ALTER TABLE `{escaped}` AUTO_INCREMENT = 1;";
                                await resetCmd.ExecuteNonQueryAsync(cancellationToken);
                            }
                            catch { }
                            clearedTables.Add($"{tableName} ({rowsDeleted} registros)");
                        }
                        catch
                        {
                            try
                            {
                                using var truncateCmd = connection.CreateCommand();
                                truncateCmd.CommandText = $"TRUNCATE TABLE `{escaped}`;";
                                await truncateCmd.ExecuteNonQueryAsync(cancellationToken);
                                clearedTables.Add($"{tableName} (truncate)");
                            }
                            catch (Exception ex2) { errors.Add($"{tableName}: {ex2.Message}"); }
                        }
                    }
                }, cancellationToken);

                if (adminUser != null && adminUserRolePairs.Any())
                {
                    var roleConditions = string.Join(" OR ", adminUserRolePairs.Select(pair => $"(UserId = {pair.UserId} AND RoleId = {pair.RoleId})"));
                    try { await _context.Database.ExecuteSqlRawAsync($"DELETE FROM UserRoles WHERE NOT ({roleConditions});", cancellationToken); }
                    catch (Exception ex) { errors.Add($"UserRoles: {ex.Message}"); }
                    try { await _context.Database.ExecuteSqlRawAsync($"DELETE FROM Users WHERE Id != {adminUser.Id};", cancellationToken); }
                    catch (Exception ex) { errors.Add($"Users: {ex.Message}"); }
                }
                else
                {
                    try
                    {
                        await _context.Database.ExecuteSqlRawAsync("DELETE FROM UserRoles;", cancellationToken);
                        await _context.Database.ExecuteSqlRawAsync("DELETE FROM Users;", cancellationToken);
                    }
                    catch (Exception ex) { errors.Add($"Users/UserRoles: {ex.Message}"); }
                }

                await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;", cancellationToken);

                var pendingMigrations = (await _context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pendingMigrations.Count > 0)
                {
                    var (migrateOk, migrateMsg) = await RunMigrationsAsync(cancellationToken);
                    if (!migrateOk && migrateMsg != AdminResultCodes.NoPendingMigrations && migrateMsg != AdminResultCodes.MigrationMarkedAsApplied)
                        errors.Add($"Migrations: {migrateMsg}");
                }

                // Seed idempotente: não sobrescreve SystemConfigurations/Roles/etc. já existentes.
                var seedService = new SeedService(_context);
                await seedService.SeedInitialDataAsync();
                await InitializeCreditPlansAsync(cancellationToken);

                var preservedList = string.Join(", ", DatabaseResetPreservedTables.OrderBy(t => t, StringComparer.OrdinalIgnoreCase));
                var successMessage =
                    $"Base corrente resetada com sucesso! {clearedTables.Count} tabela(s) de negócio limpas. " +
                    $"Preservadas: {preservedList}; Users/UserRoles (admin). " +
                    (pendingMigrations.Count > 0 ? $"Migrations aplicadas: {pendingMigrations.Count}. " : "Nenhuma migration pendente. ") +
                    "Seed executado (idempotente).";
                if (errors.Any()) successMessage += $" Avisos: {string.Join("; ", errors.Take(5))}";
                return (true, successMessage);
            }
            catch (Exception ex)
            {
                try { await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;", cancellationToken); }
                catch { }
                return (false, ex.Message + (ex.InnerException != null ? " " + ex.InnerException.Message : ""));
            }
        }

        public async Task<UserPurgeLookupDto> LookupUserForPurgeAsync(int userId, CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user == null)
            {
                return new UserPurgeLookupDto
                {
                    Found = false,
                    UserId = userId,
                    CanDelete = false,
                    BlockReason = "Usuário não encontrado."
                };
            }

            var roleNames = user.UserRoles
                .Select(ur => ur.Role?.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var isAdmin = roleNames.Contains(RoleNames.Administrator, StringComparer.OrdinalIgnoreCase)
                || string.Equals(user.Email, DatabaseResetAdminEmail, StringComparison.OrdinalIgnoreCase);
            var isPartner = roleNames.Contains(RoleNames.CoworkingPartner, StringComparer.OrdinalIgnoreCase);
            var isClient = roleNames.Contains(RoleNames.Client, StringComparer.OrdinalIgnoreCase);

            string roleType;
            string roleDisplayName;
            string displayName = ClientDisplayName.Resolve(user.FullName, user.Email);

            if (isAdmin)
            {
                roleType = RoleNames.Administrator;
                roleDisplayName = RoleNames.GetDisplayName(RoleNames.Administrator);
            }
            else if (isPartner)
            {
                roleType = RoleNames.CoworkingPartner;
                roleDisplayName = RoleNames.GetDisplayName(RoleNames.CoworkingPartner);
                var coworking = await _context.Coworkings.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
                if (coworking != null && !string.IsNullOrWhiteSpace(coworking.Name))
                    displayName = coworking.Name.Trim();
            }
            else if (isClient)
            {
                roleType = RoleNames.Client;
                roleDisplayName = RoleNames.GetDisplayName(RoleNames.Client);
                var client = await _context.Clients.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
                if (client != null)
                {
                    var clientLabel = !string.IsNullOrWhiteSpace(client.NomeFantasia) ? client.NomeFantasia
                        : !string.IsNullOrWhiteSpace(client.RazaoSocial) ? client.RazaoSocial
                        : !string.IsNullOrWhiteSpace(client.ContactName) ? client.ContactName
                        : user.FullName;
                    displayName = ClientDisplayName.Resolve(clientLabel, user.Email);
                }
            }
            else
            {
                roleType = "Unknown";
                roleDisplayName = roleNames.Count > 0
                    ? string.Join(", ", roleNames.Select(RoleNames.GetDisplayName))
                    : "Sem perfil";
            }

            var result = new UserPurgeLookupDto
            {
                Found = true,
                UserId = user.Id,
                Name = displayName,
                Email = user.Email,
                RoleType = roleType,
                RoleDisplayName = roleDisplayName,
                CanDelete = true
            };

            if (isAdmin)
            {
                result.CanDelete = false;
                result.BlockReason = "Não é permitido apagar usuários administradores do sistema.";
                return result;
            }

            if (!isClient && !isPartner)
            {
                result.CanDelete = false;
                result.BlockReason = "Somente usuários com perfil Cliente ou Parceiro Coworking podem ser excluídos por esta ferramenta.";
                return result;
            }

            if (isPartner)
            {
                var coworking = await _context.Coworkings.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
                if (coworking != null)
                {
                    var associated = await GetPartnerAssociatedClientBlockReasonAsync(coworking.Id, cancellationToken);
                    if (associated != null)
                    {
                        result.CanDelete = false;
                        result.BlockReason = associated;
                    }
                }
            }

            return result;
        }

        public async Task<UserPurgeResultDto> PurgeUserDataAsync(int userId, CancellationToken cancellationToken = default)
        {
            var lookup = await LookupUserForPurgeAsync(userId, cancellationToken);
            if (!lookup.Found)
                return new UserPurgeResultDto { Success = false, Message = lookup.BlockReason ?? "Usuário não encontrado." };
            if (!lookup.CanDelete)
                return new UserPurgeResultDto
                {
                    Success = false,
                    Message = lookup.BlockReason ?? "Exclusão não permitida.",
                    Name = lookup.Name,
                    RoleType = lookup.RoleType
                };

            try
            {
                // MySqlRetryingExecutionStrategy não permite BeginTransaction fora da strategy.
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        // FK checks off apenas nesta unidade retriável; reabilitado antes do commit/rollback.
                        await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;", cancellationToken);

                        await NullifyUserReferencesAsync(userId, cancellationToken);

                        if (lookup.RoleType == RoleNames.Client)
                            await PurgeClientOwnedDataAsync(userId, cancellationToken);
                        else if (lookup.RoleType == RoleNames.CoworkingPartner)
                            await PurgeCoworkingOwnedDataAsync(userId, cancellationToken);

                        await PurgeUserChatAsync(userId, cancellationToken);
                        await PurgeAuditLogsForUserAsync(userId, cancellationToken);

                        await _context.UserRoles.Where(ur => ur.UserId == userId).ExecuteDeleteAsync(cancellationToken);
                        await _context.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(cancellationToken);

                        await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;", cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                    }
                    catch
                    {
                        try { await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;", cancellationToken); }
                        catch { /* best-effort restore */ }
                        try { await transaction.RollbackAsync(cancellationToken); }
                        catch { /* best-effort rollback */ }
                        throw;
                    }
                });

                return new UserPurgeResultDto
                {
                    Success = true,
                    Message = $"Todos os registros de '{lookup.Name}' ({lookup.RoleDisplayName}) foram removidos com sucesso.",
                    Name = lookup.Name,
                    RoleType = lookup.RoleType
                };
            }
            catch (Exception ex)
            {
                return new UserPurgeResultDto
                {
                    Success = false,
                    Message = "Erro ao excluir dados: " + ex.Message + (ex.InnerException != null ? " " + ex.InnerException.Message : ""),
                    Name = lookup.Name,
                    RoleType = lookup.RoleType
                };
            }
        }

        /// <summary>
        /// Bloqueia se houver qualquer contrato de cliente nos endereços do parceiro,
        /// ou reserva / lead fiscal / NF / avaliação já vinculado a cliente (associação efetiva).
        /// Não remove dados de clientes — apenas impede o purge do parceiro.
        /// </summary>
        private async Task<string?> GetPartnerAssociatedClientBlockReasonAsync(int coworkingId, CancellationToken cancellationToken)
        {
            var addressIds = await _context.CoworkingAddresses.AsNoTracking()
                .Where(a => a.CoworkingId == coworkingId)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);

            if (addressIds.Count > 0)
            {
                var contractCount = await _context.Contracts.AsNoTracking()
                    .CountAsync(c => addressIds.Contains(c.CoworkingAddressId), cancellationToken);
                if (contractCount > 0)
                {
                    return $"Este parceiro possui {contractCount} contrato(s) de cliente vinculado(s) aos seus endereços. " +
                           "Remova ou transfira os clientes antes de excluir o parceiro.";
                }

                var reservationCount = await _context.Reservations.AsNoTracking()
                    .CountAsync(r => addressIds.Contains(r.CoworkingAddressId), cancellationToken);
                if (reservationCount > 0)
                {
                    return $"Este parceiro possui {reservationCount} reserva(s) de cliente vinculada(s) aos seus endereços. " +
                           "Não é possível excluir enquanto houver clientes associados.";
                }
            }

            var leadWithClient = await _context.FiscalOnboardingLeads.AsNoTracking()
                .CountAsync(l => l.CoworkingId == coworkingId && l.ClientId != null, cancellationToken);
            if (leadWithClient > 0)
            {
                return $"Este parceiro possui {leadWithClient} lead(s) de onboarding fiscal já vinculado(s) a cliente. " +
                       "Não é possível excluir enquanto houver clientes associados.";
            }

            var fiscalNotesWithClient = await _context.FiscalNotes.AsNoTracking()
                .CountAsync(f => f.CoworkingId == coworkingId && f.ClientId != null, cancellationToken);
            if (fiscalNotesWithClient > 0)
            {
                return $"Este parceiro possui {fiscalNotesWithClient} nota(s) fiscal(is) vinculada(s) a cliente. " +
                       "Não é possível excluir enquanto houver dados de clientes associados.";
            }

            var ratingsWithClient = await _context.CoworkingRatings.AsNoTracking()
                .CountAsync(r => r.CoworkingId == coworkingId && r.ClientId != null, cancellationToken);
            if (ratingsWithClient > 0)
            {
                return $"Este parceiro possui {ratingsWithClient} avaliação(ões) vinculada(s) a cliente. " +
                       "Não é possível excluir enquanto houver dados de clientes associados.";
            }

            return null;
        }

        private async Task NullifyUserReferencesAsync(int userId, CancellationToken cancellationToken)
        {
            await _context.Contracts
                .Where(c => c.AssistedSaleAdminId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.AssistedSaleAdminId, (int?)null), cancellationToken);
            await _context.Contracts
                .Where(c => c.DocumentsReviewedByUserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.DocumentsReviewedByUserId, (int?)null), cancellationToken);
            await _context.Contracts
                .Where(c => c.CancellationApprovedBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.CancellationApprovedBy, (int?)null), cancellationToken);

            await _context.Payments
                .Where(p => p.ValidatedBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.ValidatedBy, (int?)null), cancellationToken);

            await _context.PaymentHistories
                .Where(h => h.ActionBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(h => h.ActionBy, (int?)null), cancellationToken);

            await _context.ClientOnboardingDocuments
                .Where(d => d.ReviewedByUserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.ReviewedByUserId, (int?)null), cancellationToken);

            await _context.Interests
                .Where(i => i.ContactedByUserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.ContactedByUserId, (int?)null), cancellationToken);

            await _context.FiscalNoteScriptExecutions
                .Where(e => e.ExecutedByUserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.ExecutedByUserId, (int?)null), cancellationToken);

            await _context.MonthlySettlements
                .Where(m => m.ApprovedBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.ApprovedBy, (int?)null), cancellationToken);

            await _context.PartnerInvoices
                .Where(p => p.ValidatedBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.ValidatedBy, (int?)null), cancellationToken);

            await _context.ChatUnansweredQuestions
                .Where(q => q.ReviewedBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.ReviewedBy, (int?)null), cancellationToken);

            await _context.Coworkings
                .Where(c => c.ApprovedBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ApprovedBy, (int?)null), cancellationToken);
            await _context.Coworkings
                .Where(c => c.RejectedBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.RejectedBy, (int?)null), cancellationToken);

            await _context.ContractHistories
                .Where(h => h.ActorUserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(h => h.ActorUserId, (int?)null), cancellationToken);

            await _context.CapacityChangeRequests
                .Where(r => r.ReviewedBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ReviewedBy, (int?)null), cancellationToken);
            await _context.MaxContractsChangeRequests
                .Where(r => r.ReviewedBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ReviewedBy, (int?)null), cancellationToken);
            await _context.CardPriceChangeRequests
                .Where(r => r.ReviewedBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ReviewedBy, (int?)null), cancellationToken);
            await _context.PartnerServices
                .Where(p => p.RemovalReviewedBy == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.RemovalReviewedBy, (int?)null), cancellationToken);

            // Correspondências criadas por este usuário em contratos de terceiros: reatribui ao admin seed.
            var adminId = await _context.Users
                .Where(u => u.Email == DatabaseResetAdminEmail)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (adminId.HasValue && adminId.Value != userId)
            {
                await _context.Correspondences
                    .Where(c => c.CreatedByUserId == userId)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.CreatedByUserId, adminId.Value), cancellationToken);
            }

            // PromoCodes criados pelo usuário: desvincula pagamentos e remove códigos do usuário.
            var promoIds = await _context.PromoCodes
                .Where(p => p.CreatedByUserId == userId)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
            if (promoIds.Count > 0)
            {
                await _context.Payments
                    .Where(p => p.PromoCodeId != null && promoIds.Contains(p.PromoCodeId.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.PromoCodeId, (int?)null), cancellationToken);
                await _context.PromoCodes.Where(p => promoIds.Contains(p.Id)).ExecuteDeleteAsync(cancellationToken);
            }
        }

        private async Task PurgeClientOwnedDataAsync(int userId, CancellationToken cancellationToken)
        {
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
            if (client == null)
                return;

            var clientId = client.Id;
            var userEmail = await _context.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(cancellationToken);

            var contractIds = await _context.Contracts.Where(c => c.ClientId == clientId).Select(c => c.Id).ToListAsync(cancellationToken);
            var reservationIds = await _context.Reservations.Where(r => r.ClientId == clientId).Select(r => r.Id).ToListAsync(cancellationToken);
            var subscriptionIds = contractIds.Count == 0
                ? new List<int>()
                : await _context.Subscriptions.Where(s => contractIds.Contains(s.ContractId)).Select(s => s.Id).ToListAsync(cancellationToken);

            var leadIds = await _context.FiscalOnboardingLeads
                .Where(l => l.ClientId == clientId || l.UserId == userId)
                .Select(l => l.Id)
                .ToListAsync(cancellationToken);

            var paymentIds = await _context.Payments
                .Where(p =>
                    (p.SubscriptionId != null && subscriptionIds.Contains(p.SubscriptionId.Value)) ||
                    (p.ReservationId != null && reservationIds.Contains(p.ReservationId.Value)) ||
                    (p.FiscalOnboardingLeadId != null && leadIds.Contains(p.FiscalOnboardingLeadId.Value)))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            var fiscalNoteIds = await _context.FiscalNotes
                .Where(f =>
                    f.ClientId == clientId ||
                    (f.ContractId != null && contractIds.Contains(f.ContractId.Value)) ||
                    (f.ReservationId != null && reservationIds.Contains(f.ReservationId.Value)) ||
                    (f.PaymentId != null && paymentIds.Contains(f.PaymentId.Value)))
                .Select(f => f.Id)
                .ToListAsync(cancellationToken);

            // Filhos de Payment / FiscalNote (webhooks) antes dos pais.
            if (paymentIds.Count > 0)
            {
                await _context.PagBankWebhookLogs
                    .Where(w => w.PaymentId != null && paymentIds.Contains(w.PaymentId.Value))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            if (fiscalNoteIds.Count > 0)
            {
                await _context.FocusNFeWebhookLogs
                    .Where(w => w.FiscalNoteId != null && fiscalNoteIds.Contains(w.FiscalNoteId.Value))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            if (paymentIds.Count > 0)
            {
                var commissionIds = await _context.Commissions
                    .Where(c => paymentIds.Contains(c.PaymentId))
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);
                if (commissionIds.Count > 0)
                {
                    await _context.SettlementCommissions
                        .Where(sc => commissionIds.Contains(sc.CommissionId))
                        .ExecuteDeleteAsync(cancellationToken);
                    await _context.Commissions.Where(c => commissionIds.Contains(c.Id)).ExecuteDeleteAsync(cancellationToken);
                }

                await _context.PaymentHistories.Where(h => paymentIds.Contains(h.PaymentId)).ExecuteDeleteAsync(cancellationToken);
                await _context.FiscalNotes.Where(f => f.PaymentId != null && paymentIds.Contains(f.PaymentId.Value)).ExecuteDeleteAsync(cancellationToken);
                await _context.Payments.Where(p => paymentIds.Contains(p.Id)).ExecuteDeleteAsync(cancellationToken);
            }

            if (reservationIds.Count > 0)
            {
                await _context.Transfers.Where(t => t.ReservationId != null && reservationIds.Contains(t.ReservationId.Value)).ExecuteDeleteAsync(cancellationToken);
                await _context.ReservationCommissions.Where(rc => reservationIds.Contains(rc.ReservationId)).ExecuteDeleteAsync(cancellationToken);
                await _context.FiscalNotes.Where(f => f.ReservationId != null && reservationIds.Contains(f.ReservationId.Value)).ExecuteDeleteAsync(cancellationToken);
                await _context.Reservations.Where(r => reservationIds.Contains(r.Id)).ExecuteDeleteAsync(cancellationToken);
            }

            if (contractIds.Count > 0)
            {
                var correspondenceIds = await _context.Correspondences
                    .Where(c => contractIds.Contains(c.ContractId))
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);
                if (correspondenceIds.Count > 0)
                {
                    await _context.CorrespondencePhotos.Where(p => correspondenceIds.Contains(p.CorrespondenceId)).ExecuteDeleteAsync(cancellationToken);
                    await _context.Correspondences.Where(c => correspondenceIds.Contains(c.Id)).ExecuteDeleteAsync(cancellationToken);
                }

                await _context.AddressDeclarations.Where(d => contractIds.Contains(d.ContractId)).ExecuteDeleteAsync(cancellationToken);
                await _context.Documents.Where(d => contractIds.Contains(d.ContractId)).ExecuteDeleteAsync(cancellationToken);
                await _context.ClientOnboardingDocuments.Where(d => contractIds.Contains(d.ContractId) || d.ClientId == clientId).ExecuteDeleteAsync(cancellationToken);
                await _context.ContractHistories.Where(h => contractIds.Contains(h.ContractId)).ExecuteDeleteAsync(cancellationToken);
                await _context.FiscalNotes.Where(f => f.ContractId != null && contractIds.Contains(f.ContractId.Value)).ExecuteDeleteAsync(cancellationToken);
                await _context.Subscriptions.Where(s => contractIds.Contains(s.ContractId)).ExecuteDeleteAsync(cancellationToken);
                await _context.Contracts.Where(c => contractIds.Contains(c.Id)).ExecuteDeleteAsync(cancellationToken);
            }

            // Residual por ClientId (notas sem contract/payment, docs órfãos, etc.).
            await _context.FiscalNotes.Where(f => f.ClientId == clientId).ExecuteDeleteAsync(cancellationToken);
            await _context.ClientCredits.Where(c => c.ClientId == clientId).ExecuteDeleteAsync(cancellationToken);
            await _context.CreditPurchases.Where(c => c.ClientId == clientId).ExecuteDeleteAsync(cancellationToken);
            await _context.CreditCards.Where(c => c.ClientId == clientId).ExecuteDeleteAsync(cancellationToken);
            await _context.ClientRegistrationDocuments.Where(d => d.ClientId == clientId).ExecuteDeleteAsync(cancellationToken);
            await _context.ClientOnboardingDocuments.Where(d => d.ClientId == clientId).ExecuteDeleteAsync(cancellationToken);
            await _context.CoworkingRatings.Where(r => r.ClientId == clientId).ExecuteDeleteAsync(cancellationToken);

            if (leadIds.Count > 0)
            {
                await _context.ClientMagicLoginTokens.Where(t => leadIds.Contains(t.FiscalOnboardingLeadId)).ExecuteDeleteAsync(cancellationToken);
                var leftoverLeadPaymentIds = await _context.Payments
                    .Where(p => p.FiscalOnboardingLeadId != null && leadIds.Contains(p.FiscalOnboardingLeadId.Value))
                    .Select(p => p.Id)
                    .ToListAsync(cancellationToken);
                if (leftoverLeadPaymentIds.Count > 0)
                {
                    await _context.PagBankWebhookLogs
                        .Where(w => w.PaymentId != null && leftoverLeadPaymentIds.Contains(w.PaymentId.Value))
                        .ExecuteDeleteAsync(cancellationToken);
                    await _context.PaymentHistories.Where(h => leftoverLeadPaymentIds.Contains(h.PaymentId)).ExecuteDeleteAsync(cancellationToken);
                    await _context.Payments.Where(p => leftoverLeadPaymentIds.Contains(p.Id)).ExecuteDeleteAsync(cancellationToken);
                }
                await _context.FiscalOnboardingLeads.Where(l => leadIds.Contains(l.Id)).ExecuteDeleteAsync(cancellationToken);
            }

            // Interests sem ClientId: remove pelo e-mail do usuário/contato do cliente.
            var interestEmails = new List<string>();
            if (!string.IsNullOrWhiteSpace(userEmail))
                interestEmails.Add(userEmail.Trim());
            if (!string.IsNullOrWhiteSpace(client.ContactEmail)
                && !interestEmails.Contains(client.ContactEmail.Trim(), StringComparer.OrdinalIgnoreCase))
                interestEmails.Add(client.ContactEmail.Trim());
            if (interestEmails.Count > 0)
            {
                await _context.Interests
                    .Where(i => interestEmails.Contains(i.Email))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            // Client inclui dados fiscais/empresa (CNPJ, endereço fiscal, etc.) na própria entidade.
            await _context.Clients.Where(c => c.Id == clientId).ExecuteDeleteAsync(cancellationToken);
        }

        private async Task PurgeCoworkingOwnedDataAsync(int userId, CancellationToken cancellationToken)
        {
            var coworking = await _context.Coworkings.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
            if (coworking == null)
                return;

            var coworkingId = coworking.Id;
            var block = await GetPartnerAssociatedClientBlockReasonAsync(coworkingId, cancellationToken);
            if (block != null)
                throw new InvalidOperationException(block);

            var addressIds = await _context.CoworkingAddresses
                .Where(a => a.CoworkingId == coworkingId)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);

            var settlementIds = await _context.MonthlySettlements
                .Where(m => m.CoworkingId == coworkingId)
                .Select(m => m.Id)
                .ToListAsync(cancellationToken);

            if (settlementIds.Count > 0)
            {
                await _context.SettlementCommissions.Where(sc => settlementIds.Contains(sc.MonthlySettlementId)).ExecuteDeleteAsync(cancellationToken);
                await _context.PartnerInvoices.Where(p => settlementIds.Contains(p.MonthlySettlementId) || p.CoworkingId == coworkingId).ExecuteDeleteAsync(cancellationToken);
                await _context.MonthlySettlements.Where(m => settlementIds.Contains(m.Id)).ExecuteDeleteAsync(cancellationToken);
            }
            else
            {
                await _context.PartnerInvoices.Where(p => p.CoworkingId == coworkingId).ExecuteDeleteAsync(cancellationToken);
            }

            var commissionIds = await _context.Commissions
                .Where(c => c.CoworkingId == coworkingId)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
            if (commissionIds.Count > 0)
            {
                await _context.SettlementCommissions.Where(sc => commissionIds.Contains(sc.CommissionId)).ExecuteDeleteAsync(cancellationToken);
                await _context.Commissions.Where(c => commissionIds.Contains(c.Id)).ExecuteDeleteAsync(cancellationToken);
            }

            await _context.Transfers.Where(t => t.CoworkingId == coworkingId).ExecuteDeleteAsync(cancellationToken);

            // Só notas sem ClientId — nunca apagar NFs de outros clientes no purge de parceiro.
            var partnerOnlyFiscalNoteIds = await _context.FiscalNotes
                .Where(f => f.CoworkingId == coworkingId && f.ClientId == null)
                .Select(f => f.Id)
                .ToListAsync(cancellationToken);
            if (partnerOnlyFiscalNoteIds.Count > 0)
            {
                await _context.FocusNFeWebhookLogs
                    .Where(w => w.FiscalNoteId != null && partnerOnlyFiscalNoteIds.Contains(w.FiscalNoteId.Value))
                    .ExecuteDeleteAsync(cancellationToken);
                await _context.FiscalNotes.Where(f => partnerOnlyFiscalNoteIds.Contains(f.Id)).ExecuteDeleteAsync(cancellationToken);
            }

            await _context.BlockedDates.Where(b => b.CoworkingId == coworkingId).ExecuteDeleteAsync(cancellationToken);
            await _context.CoworkingOperatingHours.Where(h => h.CoworkingId == coworkingId).ExecuteDeleteAsync(cancellationToken);
            await _context.PartnerServices.Where(p => p.CoworkingId == coworkingId).ExecuteDeleteAsync(cancellationToken);
            await _context.CoworkingProducts.Where(p => p.CoworkingId == coworkingId).ExecuteDeleteAsync(cancellationToken);
            await _context.CoworkingPartners.Where(p => p.CoworkingId == coworkingId).ExecuteDeleteAsync(cancellationToken);
            await _context.CoworkingPhotos.Where(p => p.CoworkingId == coworkingId).ExecuteDeleteAsync(cancellationToken);
            await _context.CoworkingDocuments.Where(d => d.CoworkingId == coworkingId).ExecuteDeleteAsync(cancellationToken);
            await _context.CoworkingSefazConfigurations.Where(s => s.CoworkingId == coworkingId).ExecuteDeleteAsync(cancellationToken);
            // Avaliações sem ClientId (com ClientId o lookup já bloqueia).
            await _context.CoworkingRatings.Where(r => r.CoworkingId == coworkingId && r.ClientId == null).ExecuteDeleteAsync(cancellationToken);

            var promoIds = await _context.PromoCodes
                .Where(p => p.CoworkingId == coworkingId)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
            if (promoIds.Count > 0)
            {
                await _context.Payments
                    .Where(p => p.PromoCodeId != null && promoIds.Contains(p.PromoCodeId.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.PromoCodeId, (int?)null), cancellationToken);
                await _context.PromoCodes.Where(p => promoIds.Contains(p.Id)).ExecuteDeleteAsync(cancellationToken);
            }

            var leadIds = await _context.FiscalOnboardingLeads
                .Where(l => l.CoworkingId == coworkingId && l.ClientId == null)
                .Select(l => l.Id)
                .ToListAsync(cancellationToken);
            if (leadIds.Count > 0)
            {
                // Somente leads sem ClientId (bloqueio prévio garante ausência de clientes associados).
                await _context.ClientMagicLoginTokens.Where(t => leadIds.Contains(t.FiscalOnboardingLeadId)).ExecuteDeleteAsync(cancellationToken);
                var leadPaymentIds = await _context.Payments
                    .Where(p => p.FiscalOnboardingLeadId != null && leadIds.Contains(p.FiscalOnboardingLeadId.Value))
                    .Select(p => p.Id)
                    .ToListAsync(cancellationToken);
                if (leadPaymentIds.Count > 0)
                {
                    await _context.PagBankWebhookLogs
                        .Where(w => w.PaymentId != null && leadPaymentIds.Contains(w.PaymentId.Value))
                        .ExecuteDeleteAsync(cancellationToken);
                    await _context.PaymentHistories.Where(h => leadPaymentIds.Contains(h.PaymentId)).ExecuteDeleteAsync(cancellationToken);
                    await _context.Payments.Where(p => leadPaymentIds.Contains(p.Id)).ExecuteDeleteAsync(cancellationToken);
                }
                await _context.FiscalOnboardingLeads.Where(l => leadIds.Contains(l.Id)).ExecuteDeleteAsync(cancellationToken);
            }

            if (addressIds.Count > 0)
            {
                await _context.CapacityChangeRequests.Where(r => addressIds.Contains(r.CoworkingAddressId)).ExecuteDeleteAsync(cancellationToken);
                await _context.MaxContractsChangeRequests.Where(r => addressIds.Contains(r.CoworkingAddressId)).ExecuteDeleteAsync(cancellationToken);
                await _context.CardPriceChangeRequests.Where(r => addressIds.Contains(r.CoworkingAddressId)).ExecuteDeleteAsync(cancellationToken);
                await _context.Interests.Where(i => addressIds.Contains(i.CoworkingAddressId)).ExecuteDeleteAsync(cancellationToken);

                var roomIds = await _context.CoworkingRooms
                    .Where(r => addressIds.Contains(r.CoworkingAddressId))
                    .Select(r => r.Id)
                    .ToListAsync(cancellationToken);
                if (roomIds.Count > 0)
                {
                    await _context.CoworkingRoomPhotos.Where(p => roomIds.Contains(p.CoworkingRoomId)).ExecuteDeleteAsync(cancellationToken);
                    await _context.CoworkingRooms.Where(r => roomIds.Contains(r.Id)).ExecuteDeleteAsync(cancellationToken);
                }

                await _context.CoworkingAddresses.Where(a => addressIds.Contains(a.Id)).ExecuteDeleteAsync(cancellationToken);
            }

            await _context.Coworkings.Where(c => c.Id == coworkingId).ExecuteDeleteAsync(cancellationToken);
        }

        private async Task PurgeUserChatAsync(int userId, CancellationToken cancellationToken)
        {
            var conversationIds = await _context.ChatConversations
                .Where(c => c.UserId == userId)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
            if (conversationIds.Count == 0)
                return;

            await _context.ChatUnansweredQuestions.Where(q => conversationIds.Contains(q.ConversationId)).ExecuteDeleteAsync(cancellationToken);
            await _context.ChatIntentions.Where(i => conversationIds.Contains(i.ConversationId)).ExecuteDeleteAsync(cancellationToken);
            await _context.ChatMessages.Where(m => conversationIds.Contains(m.ConversationId)).ExecuteDeleteAsync(cancellationToken);
            await _context.ChatConversations.Where(c => conversationIds.Contains(c.Id)).ExecuteDeleteAsync(cancellationToken);
        }

        private async Task PurgeAuditLogsForUserAsync(int userId, CancellationToken cancellationToken)
        {
            await _context.AuditLogs.Where(a => a.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        }

        public async Task<(bool IsQuery, string Result)> ExecuteSqlForAdminAsync(string sqlQuery, CancellationToken cancellationToken = default)
        {
            var upperQuery = sqlQuery.ToUpper().Trim();
            var result = new System.Text.StringBuilder();
            if (upperQuery.TrimStart().StartsWith("SELECT") || upperQuery.TrimStart().StartsWith("SHOW") || upperQuery.TrimStart().StartsWith("DESCRIBE") || upperQuery.TrimStart().StartsWith("DESC"))
            {
                await RunWithConnectionAsync(async connection =>
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = sqlQuery;
                    command.CommandTimeout = 300;
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    if (reader.HasRows)
                    {
                        var columnNames = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++) columnNames.Add(reader.GetName(i));
                        result.AppendLine(string.Join(" | ", columnNames));
                        result.AppendLine(new string('-', Math.Min(200, columnNames.Sum(c => c.Length) + (columnNames.Count * 3))));
                        int rowCount = 0;
                        while (await reader.ReadAsync(cancellationToken) && rowCount < 1000)
                        {
                            var values = new List<string>();
                            for (int i = 0; i < reader.FieldCount; i++)
                                values.Add(reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString() ?? "");
                            result.AppendLine(string.Join(" | ", values));
                            rowCount++;
                        }
                        if (rowCount >= 1000) result.AppendLine("... (limitado a 1000 linhas)");
                    }
                }, cancellationToken);
                return (true, result.ToString());
            }
            else
            {
                var rowsAffected = 0;
                await RunWithConnectionAsync(async connection =>
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = sqlQuery;
                    command.CommandTimeout = 300;
                    rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
                }, cancellationToken);
                return (false, rowsAffected > 0 ? $"{rowsAffected} linha(s) afetada(s)." : "Comando executado.");
            }
        }

        public async Task<string> GetVerifyCoworkingOperatingHoursResultAsync(CancellationToken cancellationToken = default)
        {
            var sb = new System.Text.StringBuilder();
            await RunWithConnectionAsync(async connection =>
            {
                await using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'CoworkingOperatingHours'";
                    var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
                    sb.AppendLine(exists ? "[OK] Tabela CoworkingOperatingHours existe." : "[FALTA] Tabela CoworkingOperatingHours NÃO existe. Use 'Criar tabela' abaixo.");
                    sb.AppendLine();
                    if (!exists) return;
                }
                await using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM CoworkingOperatingHours";
                    var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
                    sb.AppendLine($"Total de registros: {count}");
                    sb.AppendLine();
                }
                await using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"SELECT Id, CoworkingId, DayOfWeek, OpeningTime, ClosingTime, IsActive, CreatedAt FROM CoworkingOperatingHours ORDER BY CoworkingId, DayOfWeek LIMIT 20";
                    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    if (!reader.HasRows)
                        sb.AppendLine("(Nenhum registro ainda. Os horários são gravados ao salvar em Parceiro > Meu Perfil > Horários de Funcionamento.)");
                    else
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            var id = reader.GetInt32(0);
                            var cowId = reader.GetInt32(1);
                            var day = reader.GetInt32(2);
                            var open = reader.IsDBNull(3) ? "" : reader.GetValue(3)?.ToString() ?? "";
                            var close = reader.IsDBNull(4) ? "" : reader.GetValue(4)?.ToString() ?? "";
                            var active = reader.GetBoolean(5);
                            sb.AppendLine($"  Id={id} CoworkingId={cowId} DayOfWeek={day} Opening={open} Closing={close} IsActive={active}");
                        }
                    }
                }
            }, cancellationToken);
            return sb.ToString();
        }

        public async Task<List<string>> AddTransfersIndexAndFkAsync(CancellationToken cancellationToken = default)
        {
            var added = new List<string>();
            try { await _context.Database.ExecuteSqlRawAsync("CREATE INDEX IX_Transfers_ReservationId ON Transfers (ReservationId)", cancellationToken); added.Add("IX_Transfers_ReservationId"); }
            catch (Exception ex) when (ex.Message?.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true || ex.InnerException?.Message?.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true) { }
            try { await _context.Database.ExecuteSqlRawAsync("ALTER TABLE Transfers ADD CONSTRAINT FK_Transfers_Reservations_ReservationId FOREIGN KEY (ReservationId) REFERENCES Reservations(Id) ON DELETE SET NULL", cancellationToken); added.Add("FK_Transfers_Reservations_ReservationId"); }
            catch (Exception ex) when (ex.Message?.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true || ex.InnerException?.Message?.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true) { }
            return added;
        }

        public async Task<List<string>> FixCreditPurchaseColumnsAsync(CancellationToken cancellationToken = default)
        {
            var added = new List<string>();
            var alters = new[] { (MigrationTableConstants.CreditPurchases, MigrationTableConstants.PaymentMethod, $"ALTER TABLE {MigrationTableConstants.CreditPurchases} ADD COLUMN {MigrationTableConstants.PaymentMethod} varchar(50) NULL"), (MigrationTableConstants.CreditPurchases, "ExternalPaymentId", $"ALTER TABLE {MigrationTableConstants.CreditPurchases} ADD COLUMN ExternalPaymentId varchar(255) NULL"), (MigrationTableConstants.CreditPurchases, "PixCode", $"ALTER TABLE {MigrationTableConstants.CreditPurchases} ADD COLUMN PixCode varchar(500) NULL"), (MigrationTableConstants.CreditPurchases, "PixExpiresAt", $"ALTER TABLE {MigrationTableConstants.CreditPurchases} ADD COLUMN PixExpiresAt datetime(6) NULL"), (MigrationTableConstants.CreditPurchases, "BoletoCode", $"ALTER TABLE {MigrationTableConstants.CreditPurchases} ADD COLUMN BoletoCode varchar(100) NULL"), (MigrationTableConstants.CreditPurchases, "BoletoBarcode", $"ALTER TABLE {MigrationTableConstants.CreditPurchases} ADD COLUMN BoletoBarcode varchar(100) NULL"), (MigrationTableConstants.CreditPurchases, "BoletoExpiresAt", $"ALTER TABLE {MigrationTableConstants.CreditPurchases} ADD COLUMN BoletoExpiresAt datetime(6) NULL"), (MigrationTableConstants.CreditPurchases, "TransactionId", $"ALTER TABLE {MigrationTableConstants.CreditPurchases} ADD COLUMN TransactionId varchar(255) NULL") };
            foreach (var (_, column, sql) in alters)
            {
                try
                {
                    await _context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                    added.Add(column);
                }
                catch (Exception ex) when (ex.Message?.Contains("Duplicate column name", StringComparison.OrdinalIgnoreCase) == true || ex.InnerException?.Message?.Contains("Duplicate column name", StringComparison.OrdinalIgnoreCase) == true) { }
            }
            return added;
        }

        public async Task<bool> CreateInterestsTableIfNotExistsAsync(CancellationToken cancellationToken = default)
        {
            var tableExists = await RunWithConnectionAsync(async connection =>
            {
                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = @"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'Interests'";
                return Convert.ToInt32(await checkCommand.ExecuteScalarAsync(cancellationToken)) > 0;
            }, cancellationToken);
            if (tableExists) return false;
            var createTableSql = @"CREATE TABLE IF NOT EXISTS Interests (Id INT AUTO_INCREMENT PRIMARY KEY, CoworkingAddressId INT NOT NULL, AllocationDate DATETIME NULL, StartTime TIME NULL, EndTime TIME NULL, Name VARCHAR(255) NOT NULL, LastName VARCHAR(255) NOT NULL, Email VARCHAR(255) NOT NULL, Phone VARCHAR(50) NULL, Status VARCHAR(50) DEFAULT 'Pending', Notes TEXT NULL, CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP, UpdatedAt DATETIME NULL, ContactedByUserId INT NULL, ContactedAt DATETIME NULL, FOREIGN KEY (CoworkingAddressId) REFERENCES CoworkingAddresses(Id) ON DELETE CASCADE, FOREIGN KEY (ContactedByUserId) REFERENCES Users(Id) ON DELETE SET NULL);";
            await _context.Database.ExecuteSqlRawAsync(createTableSql, cancellationToken);
            return true;
        }

        public async Task<bool> CreateCoworkingProductsTableIfNotExistsAsync(CancellationToken cancellationToken = default)
        {
            var tableExists = await RunWithConnectionAsync(async connection =>
            {
                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = @"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'CoworkingProducts'";
                return Convert.ToInt32(await checkCommand.ExecuteScalarAsync(cancellationToken)) > 0;
            }, cancellationToken);
            if (tableExists) return false;
            var createTableSql = @"CREATE TABLE IF NOT EXISTS CoworkingProducts (Id INT AUTO_INCREMENT PRIMARY KEY, CoworkingId INT NOT NULL, ProductType VARCHAR(50) NOT NULL, IsActive BOOLEAN DEFAULT TRUE, CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP, UpdatedAt DATETIME NULL, FOREIGN KEY (CoworkingId) REFERENCES Coworkings(Id) ON DELETE CASCADE, UNIQUE KEY UK_CoworkingProduct (CoworkingId, ProductType));";
            await _context.Database.ExecuteSqlRawAsync(createTableSql, cancellationToken);
            return true;
        }

        public async Task<bool> CreateCoworkingOperatingHoursTableIfNotExistsAsync(CancellationToken cancellationToken = default)
        {
            var tableExists = await RunWithConnectionAsync(async connection =>
            {
                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = @"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'CoworkingOperatingHours'";
                return Convert.ToInt32(await checkCommand.ExecuteScalarAsync(cancellationToken)) > 0;
            }, cancellationToken);
            if (tableExists) return false;
            var createTableSql = await GetCoworkingOperatingHoursCreateTableSqlAsync(cancellationToken);
            await _context.Database.ExecuteSqlRawAsync(createTableSql, cancellationToken);
            return true;
        }

        private static async Task<string> GetCoworkingOperatingHoursCreateTableSqlAsync(CancellationToken _) => await Task.FromResult(@"
CREATE TABLE IF NOT EXISTS CoworkingOperatingHours (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    CoworkingId INT NOT NULL,
    DayOfWeek INT NOT NULL,
    OpeningTime TIME NULL,
    ClosingTime TIME NULL,
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    FOREIGN KEY (CoworkingId) REFERENCES Coworkings(Id) ON DELETE CASCADE
);");

        public async Task<bool> CreateCreditPlansTableIfNotExistsAsync(CancellationToken cancellationToken = default)
        {
            var tableExists = await RunWithConnectionAsync(async connection =>
            {
                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = @"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'CreditPlans'";
                return Convert.ToInt32(await checkCommand.ExecuteScalarAsync(cancellationToken)) > 0;
            }, cancellationToken);
            if (tableExists) return false;
            var createTableSql = await GetCreditPlansCreateTableSqlAsync(cancellationToken);
            await _context.Database.ExecuteSqlRawAsync(createTableSql, cancellationToken);
            return true;
        }

        private static async Task<string> GetCreditPlansCreateTableSqlAsync(CancellationToken _) => await Task.FromResult(@"
CREATE TABLE IF NOT EXISTS CreditPlans (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    Price DECIMAL(18,2) NOT NULL,
    Credits INT NOT NULL,
    BonusCredits INT NOT NULL DEFAULT 0,
    Period VARCHAR(50) NOT NULL,
    ValidityMonths INT NOT NULL,
    IsActive BOOLEAN DEFAULT TRUE,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL
);");

        public async Task UpdateOrAddSystemConfigurationsAsync(IEnumerable<(string Key, string Value, string Description, bool IsSensitive)> configs, string category, string? updatedBy, CancellationToken cancellationToken = default)
        {
            foreach (var (key, value, description, isSensitive) in configs)
            {
                var existing = await _context.SystemConfigurations.FirstOrDefaultAsync(c => c.Key == key, cancellationToken);
                if (existing != null)
                {
                    existing.Value = value;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = updatedBy;
                }
                else
                {
                    _context.SystemConfigurations.Add(new SystemConfiguration
                    {
                        Key = key,
                        Value = value,
                        Description = description,
                        Category = category,
                        IsSensitive = isSensitive,
                        IsEncrypted = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task ExecuteWithContextAsync(Func<ApplicationDbContext, Task> action, CancellationToken cancellationToken = default)
        {
            await action(_context);
        }

        public ApplicationDbContext GetContext() => _context;

        /// <inheritdoc />
        public async Task<DuplicateContractArtifactsCleanupResult> CleanupDuplicateContractArtifactsAsync(
            bool dryRun = false,
            CancellationToken cancellationToken = default)
        {
            var result = new DuplicateContractArtifactsCleanupResult { DryRun = dryRun };
            var affected = new HashSet<int>();

            var contractDocuments = await _context.Documents
                .AsNoTracking()
                .Where(d => d.Type == DocumentType.Contract)
                .Select(d => new { d.Id, d.ContractId })
                .OrderBy(d => d.Id)
                .ToListAsync(cancellationToken);

            var duplicateDocumentIds = contractDocuments
                .GroupBy(d => d.ContractId)
                .Where(g => g.Count() > 1)
                .SelectMany(g =>
                {
                    affected.Add(g.Key);
                    return g.OrderBy(x => x.Id).Skip(1).Select(x => x.Id);
                })
                .ToList();

            var generatedHistory = await _context.ContractHistories
                .AsNoTracking()
                .Where(h => h.Action == ContractHistoryActions.ContractGenerated)
                .Select(h => new { h.Id, h.ContractId, h.ActionAt })
                .OrderBy(h => h.Id)
                .ToListAsync(cancellationToken);

            var duplicateHistoryIds = generatedHistory
                .GroupBy(h => h.ContractId)
                .Where(g => g.Count() > 1)
                .SelectMany(g =>
                {
                    affected.Add(g.Key);
                    return g.OrderBy(x => x.ActionAt).ThenBy(x => x.Id).Skip(1).Select(x => x.Id);
                })
                .ToList();

            var validDeclarations = await _context.AddressDeclarations
                .AsNoTracking()
                .Where(d => d.IsValid)
                .Select(d => new { d.Id, d.ContractId, d.ServiceId, d.Version, d.CreatedAt })
                .OrderBy(d => d.Id)
                .ToListAsync(cancellationToken);

            var duplicateDeclarationIds = validDeclarations
                .GroupBy(d => new { d.ContractId, d.ServiceId, d.Version })
                .Where(g => g.Count() > 1)
                .SelectMany(g =>
                {
                    affected.Add(g.Key.ContractId);
                    return g.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip(1).Select(x => x.Id);
                })
                .ToList();

            result.RemovedDocumentIds = duplicateDocumentIds;
            result.RemovedHistoryIds = duplicateHistoryIds;
            result.RemovedDeclarationIds = duplicateDeclarationIds;
            result.DocumentsRemoved = duplicateDocumentIds.Count;
            result.HistoryEntriesRemoved = duplicateHistoryIds.Count;
            result.DeclarationsRemoved = duplicateDeclarationIds.Count;
            result.AffectedContractIds = affected.OrderBy(id => id).ToList();

            if (dryRun || result.TotalRemoved == 0)
                return result;

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                if (duplicateDocumentIds.Count > 0)
                {
                    var docs = await _context.Documents
                        .Where(d => duplicateDocumentIds.Contains(d.Id))
                        .ToListAsync(cancellationToken);
                    _context.Documents.RemoveRange(docs);
                }

                if (duplicateHistoryIds.Count > 0)
                {
                    var histories = await _context.ContractHistories
                        .Where(h => duplicateHistoryIds.Contains(h.Id))
                        .ToListAsync(cancellationToken);
                    _context.ContractHistories.RemoveRange(histories);
                }

                if (duplicateDeclarationIds.Count > 0)
                {
                    var declarations = await _context.AddressDeclarations
                        .Where(d => duplicateDeclarationIds.Contains(d.Id))
                        .ToListAsync(cancellationToken);
                    _context.AddressDeclarations.RemoveRange(declarations);
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return result;
        }
    }
}
