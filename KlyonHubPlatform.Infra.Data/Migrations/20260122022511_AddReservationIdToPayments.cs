using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationIdToPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remover foreign key apenas se existir (condicional para evitar erro)
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'Payments';
                SET @constraintname = 'FK_Payments_Subscriptions_SubscriptionId';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND CONSTRAINT_NAME = @constraintname) > 0,
                    CONCAT('ALTER TABLE ', @tablename, ' DROP FOREIGN KEY ', @constraintname),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "SubscriptionId",
                table: "Payments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // Adicionar coluna ReservationId apenas se não existir
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'Payments';
                SET @columnname = 'ReservationId';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND COLUMN_NAME = @columnname) = 0,
                    CONCAT('ALTER TABLE ', @tablename, ' ADD COLUMN ', @columnname, ' INT NULL'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Adicionar coluna LogoPath apenas se não existir
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'Clients';
                SET @columnname = 'LogoPath';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND COLUMN_NAME = @columnname) = 0,
                    CONCAT('ALTER TABLE ', @tablename, ' ADD COLUMN ', @columnname, ' LONGTEXT NULL CHARACTER SET utf8mb4'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Criar tabela BlockedDates apenas se não existir
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'BlockedDates';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename) = 0,
                    CONCAT('CREATE TABLE ', @tablename, ' (
                        Id INT AUTO_INCREMENT PRIMARY KEY,
                        CoworkingId INT NOT NULL,
                        BlockedDateValue DATETIME(6) NOT NULL,
                        Reason VARCHAR(200) NOT NULL CHARACTER SET utf8mb4,
                        BlocksFullDay TINYINT(1) NOT NULL,
                        StartTime TIME(6) NULL,
                        EndTime TIME(6) NULL,
                        CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME(6) NULL,
                        CONSTRAINT FK_BlockedDates_Coworkings_CoworkingId FOREIGN KEY (CoworkingId) REFERENCES Coworkings(Id) ON DELETE CASCADE
                    ) CHARACTER SET utf8mb4'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Criar tabela CoworkingOperatingHours apenas se não existir
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'CoworkingOperatingHours';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename) = 0,
                    CONCAT('CREATE TABLE ', @tablename, ' (
                        Id INT AUTO_INCREMENT PRIMARY KEY,
                        CoworkingId INT NOT NULL,
                        DayOfWeek VARCHAR(20) NOT NULL CHARACTER SET utf8mb4,
                        OpeningTime TIME(6) NULL,
                        ClosingTime TIME(6) NULL,
                        IsActive TINYINT(1) NOT NULL,
                        CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME(6) NULL,
                        CONSTRAINT FK_CoworkingOperatingHours_Coworkings_CoworkingId FOREIGN KEY (CoworkingId) REFERENCES Coworkings(Id) ON DELETE CASCADE
                    ) CHARACTER SET utf8mb4'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Criar tabela FiscalNotes apenas se não existir
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'FiscalNotes';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename) = 0,
                    CONCAT('CREATE TABLE ', @tablename, ' (
                        Id INT AUTO_INCREMENT PRIMARY KEY,
                        Type VARCHAR(20) NOT NULL CHARACTER SET utf8mb4,
                        ReservationId INT NULL,
                        ContractId INT NULL,
                        PaymentId INT NULL,
                        CoworkingId INT NULL,
                        ClientId INT NULL,
                        Number VARCHAR(50) NULL CHARACTER SET utf8mb4,
                        VerificationCode VARCHAR(50) NULL CHARACTER SET utf8mb4,
                        AccessKey VARCHAR(50) NULL CHARACTER SET utf8mb4,
                        Amount DECIMAL(10,2) NOT NULL,
                        CommissionAmount DECIMAL(10,2) NULL,
                        ServiceAmount DECIMAL(10,2) NULL,
                        IssueDate DATETIME(6) NOT NULL,
                        DueDate DATETIME(6) NULL,
                        Status VARCHAR(20) NOT NULL CHARACTER SET utf8mb4,
                        ErrorMessage LONGTEXT NULL CHARACTER SET utf8mb4,
                        XmlContent LONGTEXT NULL CHARACTER SET utf8mb4,
                        ViewUrl LONGTEXT NULL CHARACTER SET utf8mb4,
                        PdfUrl LONGTEXT NULL CHARACTER SET utf8mb4,
                        AuthorizationProtocol LONGTEXT NULL CHARACTER SET utf8mb4,
                        AuthorizedAt DATETIME(6) NULL,
                        CancelledAt DATETIME(6) NULL,
                        CancellationReason LONGTEXT NULL CHARACTER SET utf8mb4,
                        CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME(6) NULL,
                        CONSTRAINT FK_FiscalNotes_Clients_ClientId FOREIGN KEY (ClientId) REFERENCES Clients(Id) ON DELETE SET NULL,
                        CONSTRAINT FK_FiscalNotes_Contracts_ContractId FOREIGN KEY (ContractId) REFERENCES Contracts(Id) ON DELETE SET NULL,
                        CONSTRAINT FK_FiscalNotes_Coworkings_CoworkingId FOREIGN KEY (CoworkingId) REFERENCES Coworkings(Id) ON DELETE SET NULL,
                        CONSTRAINT FK_FiscalNotes_Payments_PaymentId FOREIGN KEY (PaymentId) REFERENCES Payments(Id) ON DELETE SET NULL,
                        CONSTRAINT FK_FiscalNotes_Reservations_ReservationId FOREIGN KEY (ReservationId) REFERENCES Reservations(Id) ON DELETE SET NULL
                    ) CHARACTER SET utf8mb4'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Tabela FiscalNotes já criada acima via SQL condicional

            // Criar tabela SefazConfigurations apenas se não existir
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'SefazConfigurations';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename) = 0,
                    CONCAT('CREATE TABLE ', @tablename, ' (
                        Id INT AUTO_INCREMENT PRIMARY KEY,
                        State VARCHAR(2) NOT NULL CHARACTER SET utf8mb4,
                        Environment VARCHAR(20) NOT NULL CHARACTER SET utf8mb4,
                        PlatformCNPJ VARCHAR(18) NOT NULL CHARACTER SET utf8mb4,
                        PlatformMunicipalRegistration VARCHAR(50) NOT NULL CHARACTER SET utf8mb4,
                        CertificatePath LONGTEXT NULL CHARACTER SET utf8mb4,
                        CertificatePassword LONGTEXT NULL CHARACTER SET utf8mb4,
                        CertificateContent LONGTEXT NULL CHARACTER SET utf8mb4,
                        IntermediationServiceCode VARCHAR(20) NOT NULL CHARACTER SET utf8mb4,
                        IntermediationServiceDescription VARCHAR(500) NOT NULL CHARACTER SET utf8mb4,
                        PlatformCNAE VARCHAR(20) NOT NULL CHARACTER SET utf8mb4,
                        TaxRegime VARCHAR(100) NOT NULL CHARACTER SET utf8mb4,
                        ISSAliquot DECIMAL(5,2) NOT NULL,
                        ISSMunicipality VARCHAR(200) NOT NULL CHARACTER SET utf8mb4,
                        HomologationUrl LONGTEXT NULL CHARACTER SET utf8mb4,
                        ProductionUrl LONGTEXT NULL CHARACTER SET utf8mb4,
                        ApiToken LONGTEXT NULL CHARACTER SET utf8mb4,
                        ApiKey LONGTEXT NULL CHARACTER SET utf8mb4,
                        TimeoutSeconds INT NOT NULL,
                        IsActive TINYINT(1) NOT NULL,
                        CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME(6) NULL,
                        Notes LONGTEXT NULL CHARACTER SET utf8mb4
                    ) CHARACTER SET utf8mb4'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Tabela SefazConfigurations já criada acima via SQL condicional

            // Criar tabela Services apenas se não existir
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'Services';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename) = 0,
                    CONCAT('CREATE TABLE ', @tablename, ' (
                        Id INT AUTO_INCREMENT PRIMARY KEY,
                        Name VARCHAR(200) NOT NULL CHARACTER SET utf8mb4,
                        Description VARCHAR(1000) NOT NULL CHARACTER SET utf8mb4,
                        ImageUrl VARCHAR(500) NULL CHARACTER SET utf8mb4,
                        Route VARCHAR(500) NOT NULL CHARACTER SET utf8mb4,
                        DisplayOrder INT NOT NULL,
                        IsActive TINYINT(1) NOT NULL,
                        CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME(6) NULL
                    ) CHARACTER SET utf8mb4'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Criar tabela PartnerServices apenas se não existir
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'PartnerServices';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename) = 0,
                    CONCAT('CREATE TABLE ', @tablename, ' (
                        Id INT AUTO_INCREMENT PRIMARY KEY,
                        CoworkingId INT NOT NULL,
                        ServiceId INT NOT NULL,
                        Percentage DECIMAL(5,2) NOT NULL,
                        CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME(6) NULL,
                        CONSTRAINT FK_PartnerServices_Coworkings_CoworkingId FOREIGN KEY (CoworkingId) REFERENCES Coworkings(Id) ON DELETE CASCADE,
                        CONSTRAINT FK_PartnerServices_Services_ServiceId FOREIGN KEY (ServiceId) REFERENCES Services(Id) ON DELETE RESTRICT
                    ) CHARACTER SET utf8mb4'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Criar índice apenas se não existir
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'Payments';
                SET @indexname = 'IX_Payments_ReservationId';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND INDEX_NAME = @indexname) = 0,
                    CONCAT('CREATE INDEX ', @indexname, ' ON ', @tablename, ' (ReservationId)'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Criar índices apenas se não existirem
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'BlockedDates';
                SET @indexname = 'IX_BlockedDates_CoworkingId_BlockedDateValue';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND INDEX_NAME = @indexname) = 0,
                    CONCAT('CREATE INDEX ', @indexname, ' ON ', @tablename, ' (CoworkingId, BlockedDateValue)'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'CoworkingOperatingHours';
                SET @indexname = 'IX_CoworkingOperatingHours_CoworkingId_DayOfWeek';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND INDEX_NAME = @indexname) = 0,
                    CONCAT('CREATE UNIQUE INDEX ', @indexname, ' ON ', @tablename, ' (CoworkingId, DayOfWeek)'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Criar índices para FiscalNotes apenas se não existirem
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'FiscalNotes';
                SET @indexname = 'IX_FiscalNotes_AccessKey';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND INDEX_NAME = @indexname) = 0,
                    CONCAT('CREATE INDEX ', @indexname, ' ON ', @tablename, ' (AccessKey)'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'FiscalNotes';
                SET @indexname = 'IX_FiscalNotes_ClientId';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND INDEX_NAME = @indexname) = 0,
                    CONCAT('CREATE INDEX ', @indexname, ' ON ', @tablename, ' (ClientId)'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'FiscalNotes';
                SET @indexname = 'IX_FiscalNotes_ContractId';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND INDEX_NAME = @indexname) = 0,
                    CONCAT('CREATE INDEX ', @indexname, ' ON ', @tablename, ' (ContractId)'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'FiscalNotes';
                SET @indexname = 'IX_FiscalNotes_CoworkingId';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND INDEX_NAME = @indexname) = 0,
                    CONCAT('CREATE INDEX ', @indexname, ' ON ', @tablename, ' (CoworkingId)'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'FiscalNotes';
                SET @indexname = 'IX_FiscalNotes_PaymentId';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND INDEX_NAME = @indexname) = 0,
                    CONCAT('CREATE INDEX ', @indexname, ' ON ', @tablename, ' (PaymentId)'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'FiscalNotes';
                SET @indexname = 'IX_FiscalNotes_ReservationId';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND INDEX_NAME = @indexname) = 0,
                    CONCAT('CREATE INDEX ', @indexname, ' ON ', @tablename, ' (ReservationId)'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'FiscalNotes';
                SET @indexname = 'IX_FiscalNotes_Type_ReservationId_ContractId';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND INDEX_NAME = @indexname) = 0,
                    CONCAT('CREATE INDEX ', @indexname, ' ON ', @tablename, ' (Type, ReservationId, ContractId)'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Criar índices para PartnerServices apenas se não existirem
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'PartnerServices';
                SET @indexname = 'IX_PartnerServices_CoworkingId_ServiceId';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND INDEX_NAME = @indexname) = 0,
                    CONCAT('CREATE UNIQUE INDEX ', @indexname, ' ON ', @tablename, ' (CoworkingId, ServiceId)'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'PartnerServices';
                SET @indexname = 'IX_PartnerServices_ServiceId';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND INDEX_NAME = @indexname) = 0,
                    CONCAT('CREATE INDEX ', @indexname, ' ON ', @tablename, ' (ServiceId)'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Criar índice para SefazConfigurations apenas se não existir
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'SefazConfigurations';
                SET @indexname = 'IX_SefazConfigurations_State_Environment';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND INDEX_NAME = @indexname) = 0,
                    CONCAT('CREATE UNIQUE INDEX ', @indexname, ' ON ', @tablename, ' (State, Environment)'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Foreign key para AuditLogs removida - pode já existir ou ter dados inválidos
            // migrationBuilder.AddForeignKey(
            //     name: "FK_AuditLogs_Users_UserId",
            //     table: "AuditLogs",
            //     column: "UserId",
            //     principalTable: "Users",
            //     principalColumn: "Id",
            //     onDelete: ReferentialAction.SetNull);

            // Adicionar foreign key apenas se não existir
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'Payments';
                SET @constraintname = 'FK_Payments_Reservations_ReservationId';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND CONSTRAINT_NAME = @constraintname) = 0,
                    CONCAT('ALTER TABLE ', @tablename, ' ADD CONSTRAINT ', @constraintname, 
                           ' FOREIGN KEY (ReservationId) REFERENCES Reservations(Id) ON DELETE SET NULL'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Adicionar foreign key apenas se não existir
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'Payments';
                SET @constraintname = 'FK_Payments_Subscriptions_SubscriptionId';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND CONSTRAINT_NAME = @constraintname) = 0,
                    CONCAT('ALTER TABLE ', @tablename, ' ADD CONSTRAINT ', @constraintname, 
                           ' FOREIGN KEY (SubscriptionId) REFERENCES Subscriptions(Id) ON DELETE SET NULL'),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Foreign key para AuditLogs removida
            // migrationBuilder.DropForeignKey(
            //     name: "FK_AuditLogs_Users_UserId",
            //     table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Reservations_ReservationId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Subscriptions_SubscriptionId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "BlockedDates");

            migrationBuilder.DropTable(
                name: "CoworkingOperatingHours");

            migrationBuilder.DropTable(
                name: "FiscalNotes");

            migrationBuilder.DropTable(
                name: "PartnerServices");

            migrationBuilder.DropTable(
                name: "SefazConfigurations");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReservationId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "LogoPath",
                table: "Clients");

            migrationBuilder.AlterColumn<int>(
                name: "SubscriptionId",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Subscriptions_SubscriptionId",
                table: "Payments",
                column: "SubscriptionId",
                principalTable: "Subscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
