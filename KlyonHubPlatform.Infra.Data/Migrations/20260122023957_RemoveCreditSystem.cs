using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCreditSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remover foreign keys apenas se existirem (idempotente)
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                
                -- Remover FK_CreditPurchases_Clients_ClientId se existir
                SET @fkname = 'FK_CreditPurchases_Clients_ClientId';
                SET @tablename = 'CreditPurchases';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND CONSTRAINT_NAME = @fkname) > 0,
                    CONCAT('ALTER TABLE ', @tablename, ' DROP FOREIGN KEY ', @fkname),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @fkname = 'FK_CreditPurchases_CreditPlans_CreditPlanId';
                SET @tablename = 'CreditPurchases';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND CONSTRAINT_NAME = @fkname) > 0,
                    CONCAT('ALTER TABLE ', @tablename, ' DROP FOREIGN KEY ', @fkname),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @fkname = 'FK_ClientCredits_Clients_ClientId';
                SET @tablename = 'ClientCredits';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND CONSTRAINT_NAME = @fkname) > 0,
                    CONCAT('ALTER TABLE ', @tablename, ' DROP FOREIGN KEY ', @fkname),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Remover índices apenas se existirem
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @indexname = 'IX_CreditPurchases_ClientId';
                SET @tablename = 'CreditPurchases';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND INDEX_NAME = @indexname) > 0,
                    CONCAT('DROP INDEX ', @indexname, ' ON ', @tablename),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @indexname = 'IX_CreditPurchases_CreditPlanId';
                SET @tablename = 'CreditPurchases';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND INDEX_NAME = @indexname) > 0,
                    CONCAT('DROP INDEX ', @indexname, ' ON ', @tablename),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @indexname = 'IX_ClientCredits_ClientId';
                SET @tablename = 'ClientCredits';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND INDEX_NAME = @indexname) > 0,
                    CONCAT('DROP INDEX ', @indexname, ' ON ', @tablename),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Remover colunas apenas se existirem
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'Reservations';
                SET @columnname = 'CreditsUsed';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND COLUMN_NAME = @columnname) > 0,
                    CONCAT('ALTER TABLE ', @tablename, ' DROP COLUMN ', @columnname),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'CoworkingRooms';
                SET @columnname = 'CreditsPerHour';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND COLUMN_NAME = @columnname) > 0,
                    CONCAT('ALTER TABLE ', @tablename, ' DROP COLUMN ', @columnname),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'ReservationCommissions';
                SET @columnname = 'TotalCredits';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename 
                     AND COLUMN_NAME = @columnname) > 0,
                    CONCAT('ALTER TABLE ', @tablename, ' DROP COLUMN ', @columnname),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Remover tabelas apenas se existirem
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'CreditPurchases';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename) > 0,
                    CONCAT('DROP TABLE IF EXISTS ', @tablename),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'ClientCredits';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename) > 0,
                    CONCAT('DROP TABLE IF EXISTS ', @tablename),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'CreditPlans';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename) > 0,
                    CONCAT('DROP TABLE IF EXISTS ', @tablename),
                    'SELECT 1'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'PlatformCreditBalances';
                SET @preparedStatement = (SELECT IF(
                    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
                     WHERE TABLE_SCHEMA = @dbname 
                     AND TABLE_NAME = @tablename) > 0,
                    CONCAT('DROP TABLE IF EXISTS ', @tablename),
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
            // Recriar tabelas (para rollback)
            migrationBuilder.CreateTable(
                name: "CreditPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Price = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Credits = table.Column<int>(type: "int", nullable: false),
                    BonusCredits = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ValidityMonths = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditPlans", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PlatformCreditBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TotalCredits = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformCreditBalances", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClientCredits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    Credits = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientCredits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientCredits_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CreditPurchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    CreditPlanId = table.Column<int>(type: "int", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CreditsReceived = table.Column<int>(type: "int", nullable: false),
                    BonusCredits = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PaymentMethod = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExternalPaymentId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PixCode = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PixExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    BoletoCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BoletoBarcode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BoletoExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TransactionId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    PaidAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditPurchases_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreditPurchases_CreditPlans_CreditPlanId",
                        column: x => x.CreditPlanId,
                        principalTable: "CreditPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Recriar índices
            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_ClientId",
                table: "CreditPurchases",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_CreditPlanId",
                table: "CreditPurchases",
                column: "CreditPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientCredits_ClientId",
                table: "ClientCredits",
                column: "ClientId");

            // Recriar colunas
            migrationBuilder.AddColumn<int>(
                name: "CreditsUsed",
                table: "Reservations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CreditsPerHour",
                table: "CoworkingRooms",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCredits",
                table: "ReservationCommissions",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
