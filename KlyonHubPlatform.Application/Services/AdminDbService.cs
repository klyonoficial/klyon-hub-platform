using System.Data.Common;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
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
            var storageProvider = await _context.SystemConfigurations.FirstOrDefaultAsync(c => c.Key == Provider, cancellationToken);
            if (storageProvider != null)
            {
                storageProvider.Value = ProviderName;
                storageProvider.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.SystemConfigurations.Add(new SystemConfiguration
                {
                    Key = Provider,
                    Value = ProviderName,
                    Description = "Provedor de armazenamento: Oracle, FTP ou AWS",
                    Category = "Storage",
                    IsSensitive = false,
                    IsEncrypted = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
            var ftpConfigs = new (FtpConfigKey Key, string Value)[]
            {
                (FtpConfigKey.Host, "187.110.161.110"),
                (FtpConfigKey.Port, FtpDefaults.PortString),
                (FtpConfigKey.Username, "gestorhub"),
                (FtpConfigKey.Password, "F@z3rd@rC3rt0"),
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
                        Category = "Storage",
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
                    Category = "Storage",
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
                storageProvider.Value = "AWS";
                storageProvider.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.SystemConfigurations.Add(new SystemConfiguration
                {
                    Key = Provider,
                    Value = "AWS",
                    Description = "Provedor de armazenamento: Oracle, FTP ou AWS",
                    Category = "Storage",
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
                        Action = "Rejected",
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
                    return (false, "NoPendingMigrations");
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
                        return (false, "MigrationMarkedAsApplied");
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
            => await _context.Services.Where(x => !x.Code.Contains("FISCAL_ADDRESS")).Select(x => x.Name).ToListAsync(cancellationToken);

        public async Task<(bool Success, string? ErrorMessage)> SavePartnerServiceAsync(int id, int coworkingId, int serviceId, decimal percentage, CancellationToken cancellationToken = default)
        {
            if (id > 0)
            {
                var existing = await _context.PartnerServices.FindAsync(new object[] { id }, cancellationToken);
                if (existing == null || existing.CoworkingId != coworkingId) return (false, "PartnerServiceNotFound");
                existing.ServiceId = serviceId;
                existing.Percentage = percentage;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var exists = await _context.PartnerServices.AnyAsync(ps => ps.CoworkingId == coworkingId && ps.ServiceId == serviceId, cancellationToken);
                if (exists) return (false, "ServiceAlreadyRegisteredForPartner");
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
            if (partnerService == null || partnerService.CoworkingId != coworkingId) return (false, "PartnerServiceNotFound");
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
                new CreditPlan { Name = "Básico", Price = 300m, Credits = 300, BonusCredits = 0, Period = "Mensal", ValidityMonths = 6, IsActive = true },
                new CreditPlan { Name = "Profissional", Price = 500m, Credits = 520, BonusCredits = 20, Period = "Mensal", ValidityMonths = 6, IsActive = true },
                new CreditPlan { Name = "Business", Price = 1000m, Credits = 1080, BonusCredits = 80, Period = "Mensal", ValidityMonths = 6, IsActive = true }
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

        public async Task<(bool Success, string Message)> ClearAndSeedDatabaseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;", cancellationToken);
                var tablesToClear = new[] { "AuditLogs", "PaymentHistories", "FiscalNoteScriptExecutions", "FiscalNotes", "ReservationCommissions", "Commissions", "Transfers", "Reservations", "Payments", "Subscriptions", "Contracts", "AddressDeclarations", "CapacityChangeRequests", "CoworkingRoomPhotos", "CoworkingRooms", "CoworkingPhotos", "CoworkingDocuments", "CoworkingOperatingHours", "BlockedDates", "CoworkingPartners", "CoworkingSefazConfigurations", "CoworkingAddresses", "PartnerServices", "LocationCommissions", "PlatformCreditBalances", "Coworkings", "Clients", "CreditPurchases", "ClientCredits", "CreditPlans", "CreditCards", "Documents", "Interests", "CoworkingProducts", "CoworkingRatings", "Translations", "Services" };
                var clearedTables = new List<string>();
                var errors = new List<string>();
                var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == "admin@klyon.com", cancellationToken);
                var adminUserRolePairs = new List<(int UserId, int RoleId)>();
                if (adminUser != null)
                {
                    var adminRoles = await _context.UserRoles.Where(ur => ur.UserId == adminUser.Id).ToListAsync(cancellationToken);
                    adminUserRolePairs = adminRoles.Select(ur => (ur.UserId, ur.RoleId)).ToList();
                }
                await RunWithConnectionAsync(async connection =>
                {
                    foreach (var tableName in tablesToClear)
                    {
                        try
                        {
                            using var checkCmd = connection.CreateCommand();
                            checkCmd.CommandText = $@"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = '{tableName}'";
                            var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(cancellationToken)) > 0;
                            if (exists)
                            {
                                using var deleteCmd = connection.CreateCommand();
                                deleteCmd.CommandText = $"DELETE FROM `{tableName}`;";
                                var rowsDeleted = await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
                                try
                                {
                                    using var resetCmd = connection.CreateCommand();
                                    resetCmd.CommandText = $"ALTER TABLE `{tableName}` AUTO_INCREMENT = 1;";
                                    await resetCmd.ExecuteNonQueryAsync(cancellationToken);
                                }
                                catch { }
                                clearedTables.Add($"{tableName} ({rowsDeleted} registros)");
                            }
                        }
                        catch
                        {
                            try
                            {
                                using var truncateCmd = connection.CreateCommand();
                                truncateCmd.CommandText = $"TRUNCATE TABLE `{tableName}`;";
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

                await RunWithConnectionAsync(async connection =>
                {
                    foreach (var tableName in new[] { "AuditLogs", "Users", "Roles", "Clients", "Coworkings", "Contracts", "Payments", "Subscriptions", "Locations", "Services", "SystemConfigurations", "Languages", "LocationCommissions", "UserRoles" })
                    {
                        try
                        {
                            using var checkCmd = connection.CreateCommand();
                            checkCmd.CommandText = $@"SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = '{tableName.Replace("`", "``")}' AND column_name = 'Id' AND extra LIKE '%auto_increment%'";
                            var hasAutoIncrement = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(cancellationToken)) > 0;
                            if (hasAutoIncrement)
                            {
                                using var resetCmd = connection.CreateCommand();
                                resetCmd.CommandText = $"ALTER TABLE `{tableName.Replace("`", "``")}` AUTO_INCREMENT = 1;";
                                await resetCmd.ExecuteNonQueryAsync(cancellationToken);
                            }
                        }
                        catch { }
                    }
                }, cancellationToken);

                await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;", cancellationToken);
                var seedService = new SeedService(_context);
                await seedService.SeedInitialDataAsync();
                var successMessage = $"Base de dados limpa com sucesso! {clearedTables.Count} tabela(s) foram limpas.";
                if (errors.Any()) successMessage += $" Avisos: {string.Join("; ", errors.Take(3))}";
                return (true, successMessage);
            }
            catch (Exception ex)
            {
                try { await _context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;", cancellationToken); }
                catch { }
                return (false, ex.Message + (ex.InnerException != null ? " " + ex.InnerException.Message : ""));
            }
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
            var alters = new[] { ("CreditPurchases", "PaymentMethod", "ALTER TABLE CreditPurchases ADD COLUMN PaymentMethod varchar(50) NULL"), ("CreditPurchases", "ExternalPaymentId", "ALTER TABLE CreditPurchases ADD COLUMN ExternalPaymentId varchar(255) NULL"), ("CreditPurchases", "PixCode", "ALTER TABLE CreditPurchases ADD COLUMN PixCode varchar(500) NULL"), ("CreditPurchases", "PixExpiresAt", "ALTER TABLE CreditPurchases ADD COLUMN PixExpiresAt datetime(6) NULL"), ("CreditPurchases", "BoletoCode", "ALTER TABLE CreditPurchases ADD COLUMN BoletoCode varchar(100) NULL"), ("CreditPurchases", "BoletoBarcode", "ALTER TABLE CreditPurchases ADD COLUMN BoletoBarcode varchar(100) NULL"), ("CreditPurchases", "BoletoExpiresAt", "ALTER TABLE CreditPurchases ADD COLUMN BoletoExpiresAt datetime(6) NULL"), ("CreditPurchases", "TransactionId", "ALTER TABLE CreditPurchases ADD COLUMN TransactionId varchar(255) NULL") };
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
    }
}
