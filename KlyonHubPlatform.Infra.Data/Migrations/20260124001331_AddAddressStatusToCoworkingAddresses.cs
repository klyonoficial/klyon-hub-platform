using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressStatusToCoworkingAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Verificar e adicionar colunas apenas se não existirem (idempotente)
            // AddressStatus
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = @dbname
                    AND TABLE_NAME = 'CoworkingAddresses'
                    AND COLUMN_NAME = 'AddressStatus');
                SET @sql = IF(@col_exists = 0,
                    'ALTER TABLE `CoworkingAddresses` ADD COLUMN `AddressStatus` INT NOT NULL DEFAULT 0',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;");

            // ApprovedFiscalCapacity
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = @dbname
                    AND TABLE_NAME = 'CoworkingAddresses'
                    AND COLUMN_NAME = 'ApprovedFiscalCapacity');
                SET @sql = IF(@col_exists = 0,
                    'ALTER TABLE `CoworkingAddresses` ADD COLUMN `ApprovedFiscalCapacity` INT NOT NULL DEFAULT 0',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;");

            // CapacityApprovalStatus
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = @dbname
                    AND TABLE_NAME = 'CoworkingAddresses'
                    AND COLUMN_NAME = 'CapacityApprovalStatus');
                SET @sql = IF(@col_exists = 0,
                    'ALTER TABLE `CoworkingAddresses` ADD COLUMN `CapacityApprovalStatus` INT NOT NULL DEFAULT 0',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;");

            // RequestedFiscalCapacity
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = @dbname
                    AND TABLE_NAME = 'CoworkingAddresses'
                    AND COLUMN_NAME = 'RequestedFiscalCapacity');
                SET @sql = IF(@col_exists = 0,
                    'ALTER TABLE `CoworkingAddresses` ADD COLUMN `RequestedFiscalCapacity` INT NOT NULL DEFAULT 0',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;");

            migrationBuilder.AddColumn<string>(
                name: "SnapshotAddress",
                table: "Contracts",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "SnapshotCapacityConsumed",
                table: "Contracts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SnapshotCommissionPercentage",
                table: "Contracts",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SnapshotCoworkingCommissionPercentage",
                table: "Contracts",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SnapshotMonthlyValue",
                table: "Contracts",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SnapshotPlatformCommissionPercentage",
                table: "Contracts",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CapacityChangeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CoworkingAddressId = table.Column<int>(type: "int", nullable: false),
                    RequestedCapacity = table.Column<int>(type: "int", nullable: false),
                    PreviousCapacity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Justification = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RejectionReason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewedBy = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapacityChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CapacityChangeRequests_CoworkingAddresses_CoworkingAddressId",
                        column: x => x.CoworkingAddressId,
                        principalTable: "CoworkingAddresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CapacityChangeRequests_Users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CapacityChangeRequests_CoworkingAddressId",
                table: "CapacityChangeRequests",
                column: "CoworkingAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_CapacityChangeRequests_ReviewedBy",
                table: "CapacityChangeRequests",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CapacityChangeRequests_Status",
                table: "CapacityChangeRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapacityChangeRequests");

            migrationBuilder.DropColumn(
                name: "AddressStatus",
                table: "CoworkingAddresses");

            migrationBuilder.DropColumn(
                name: "ApprovedFiscalCapacity",
                table: "CoworkingAddresses");

            migrationBuilder.DropColumn(
                name: "CapacityApprovalStatus",
                table: "CoworkingAddresses");

            migrationBuilder.DropColumn(
                name: "RequestedFiscalCapacity",
                table: "CoworkingAddresses");

            migrationBuilder.DropColumn(
                name: "SnapshotAddress",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SnapshotCapacityConsumed",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SnapshotCommissionPercentage",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SnapshotCoworkingCommissionPercentage",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SnapshotMonthlyValue",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SnapshotPlatformCommissionPercentage",
                table: "Contracts");
        }
    }
}
