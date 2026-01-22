using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationDoubleConfirmAndPlatformCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Tabela PlatformCreditBalances (conta do administrador) — idempotente
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS PlatformCreditBalances (
                    Id INT NOT NULL,
                    TotalCredits DECIMAL(12,2) NOT NULL,
                    UpdatedAt DATETIME(6) NOT NULL,
                    PRIMARY KEY (Id)
                ) CHARACTER SET utf8mb4;
            ");
            migrationBuilder.Sql(@"
                INSERT INTO PlatformCreditBalances (Id, TotalCredits, UpdatedAt)
                SELECT 1, 0, UTC_TIMESTAMP() FROM DUAL
                WHERE NOT EXISTS (SELECT 1 FROM PlatformCreditBalances LIMIT 1);
            ");

            // 2) Colunas em Reservations — ADD COLUMN IF NOT EXISTS (MariaDB)
            migrationBuilder.Sql("ALTER TABLE Reservations ADD COLUMN IF NOT EXISTS ClientConfirmedAt datetime(6) NULL;");
            migrationBuilder.Sql("ALTER TABLE Reservations ADD COLUMN IF NOT EXISTS CoworkingConfirmedAt datetime(6) NULL;");

            // 3) ReservationId em Transfers (FK: MariaDB não tem IF NOT EXISTS; usa verificação)
            migrationBuilder.Sql("ALTER TABLE Transfers ADD COLUMN IF NOT EXISTS ReservationId int NULL;");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS IX_Transfers_ReservationId ON Transfers (ReservationId);");
            migrationBuilder.Sql(@"
                SET @fk_exists = (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
                    WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'Transfers'
                    AND CONSTRAINT_NAME = 'FK_Transfers_Reservations_ReservationId');
                SET @sql = IF(@fk_exists = 0,
                    'ALTER TABLE Transfers ADD CONSTRAINT FK_Transfers_Reservations_ReservationId FOREIGN KEY (ReservationId) REFERENCES Reservations(Id) ON DELETE SET NULL',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // 4) Tabela ReservationCommissions — idempotente
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ReservationCommissions (
                    Id int NOT NULL AUTO_INCREMENT,
                    ReservationId int NOT NULL,
                    TotalCredits decimal(10,2) NOT NULL,
                    CommissionPercentage decimal(5,2) NOT NULL,
                    CommissionAmount decimal(10,2) NOT NULL,
                    CoworkingAmount decimal(10,2) NOT NULL,
                    CreatedAt datetime(6) NOT NULL DEFAULT (CURRENT_TIMESTAMP(6)),
                    PRIMARY KEY (Id),
                    KEY IX_ReservationCommissions_ReservationId (ReservationId),
                    CONSTRAINT FK_ReservationCommissions_Reservations_ReservationId
                        FOREIGN KEY (ReservationId) REFERENCES Reservations (Id) ON DELETE CASCADE
                ) CHARACTER SET utf8mb4;
            ");

            // 5) Parâmetro de comissão (se não existir)
            migrationBuilder.Sql(@"
                INSERT INTO SystemConfigurations (`Key`, `Value`, `Description`, `Category`, `IsEncrypted`, `IsSensitive`, `CreatedAt`)
                SELECT 'Platform.CommissionPercentage', '20', 'Percentual de comissão da plataforma sobre reservas (ex: 20 = 20%). Alterável em Configurações do Sistema.', 'Platform', 0, 0, UTC_TIMESTAMP()
                FROM DUAL
                WHERE NOT EXISTS (SELECT 1 FROM SystemConfigurations WHERE `Key` = 'Platform.CommissionPercentage');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ReservationCommissions");
            migrationBuilder.DropForeignKey(name: "FK_Transfers_Reservations_ReservationId", table: "Transfers");
            migrationBuilder.DropIndex(name: "IX_Transfers_ReservationId", table: "Transfers");
            migrationBuilder.DropColumn(name: "ReservationId", table: "Transfers");
            migrationBuilder.DropColumn(name: "ClientConfirmedAt", table: "Reservations");
            migrationBuilder.DropColumn(name: "CoworkingConfirmedAt", table: "Reservations");
            migrationBuilder.DropTable(name: "PlatformCreditBalances");

            migrationBuilder.Sql("DELETE FROM SystemConfigurations WHERE `Key` = 'Platform.CommissionPercentage';");
        }
    }
}
