using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixCreditCardClientRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: only drop if exists (safe when migration was partially applied before)
            var dropFk = @"
SET @stmt = (SELECT IF(COUNT(*) > 0, 'ALTER TABLE `CreditCards` DROP FOREIGN KEY `FK_CreditCards_Clients_ClientId1`', 'SELECT 1')
  FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
  WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'CreditCards' AND CONSTRAINT_NAME = 'FK_CreditCards_Clients_ClientId1');
PREPARE stmt FROM @stmt;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;";
            migrationBuilder.Sql(dropFk);

            var dropIndex = @"
SET @stmt = (SELECT IF(COUNT(*) > 0, 'ALTER TABLE `CreditCards` DROP INDEX `IX_CreditCards_ClientId1`', 'SELECT 1')
  FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CreditCards' AND INDEX_NAME = 'IX_CreditCards_ClientId1' LIMIT 1);
PREPARE stmt FROM @stmt;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;";
            migrationBuilder.Sql(dropIndex);

            var dropColumn = @"
SET @stmt = (SELECT IF(COUNT(*) > 0, 'ALTER TABLE `CreditCards` DROP COLUMN `ClientId1`', 'SELECT 1')
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CreditCards' AND COLUMN_NAME = 'ClientId1' LIMIT 1);
PREPARE stmt FROM @stmt;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;";
            migrationBuilder.Sql(dropColumn);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientId1",
                table: "CreditCards",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditCards_ClientId1",
                table: "CreditCards",
                column: "ClientId1");

            migrationBuilder.AddForeignKey(
                name: "FK_CreditCards_Clients_ClientId1",
                table: "CreditCards",
                column: "ClientId1",
                principalTable: "Clients",
                principalColumn: "Id");
        }
    }
}
