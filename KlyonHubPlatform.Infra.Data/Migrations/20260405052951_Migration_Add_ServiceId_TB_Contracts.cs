using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Adiciona ServiceId em Contracts. Idempotente: coluna/índice/FK já existem em alguns bancos
    /// (schema aplicado fora do histórico EF).
    /// </summary>
    public partial class Migration_Add_ServiceId_TB_Contracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET @stmt = (SELECT IF(COUNT(*) = 0,
  'ALTER TABLE `Contracts` ADD COLUMN `ServiceId` int NOT NULL DEFAULT 0',
  'SELECT 1')
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Contracts' AND COLUMN_NAME = 'ServiceId');
PREPARE stmt FROM @stmt;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
SET @stmt = (SELECT IF(COUNT(*) = 0,
  'CREATE INDEX `IX_Contracts_ServiceId` ON `Contracts` (`ServiceId`)',
  'SELECT 1')
  FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Contracts' AND INDEX_NAME = 'IX_Contracts_ServiceId');
PREPARE stmt FROM @stmt;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
SET @stmt = (SELECT IF(COUNT(*) = 0,
  'ALTER TABLE `Contracts` ADD CONSTRAINT `FK_Contracts_Services_ServiceId` FOREIGN KEY (`ServiceId`) REFERENCES `Services` (`Id`) ON DELETE RESTRICT',
  'SELECT 1')
  FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Contracts' AND CONSTRAINT_NAME = 'FK_Contracts_Services_ServiceId');
PREPARE stmt FROM @stmt;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET @stmt = (SELECT IF(COUNT(*) > 0,
  'ALTER TABLE `Contracts` DROP FOREIGN KEY `FK_Contracts_Services_ServiceId`',
  'SELECT 1')
  FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Contracts' AND CONSTRAINT_NAME = 'FK_Contracts_Services_ServiceId');
PREPARE stmt FROM @stmt;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
SET @stmt = (SELECT IF(COUNT(*) > 0,
  'DROP INDEX `IX_Contracts_ServiceId` ON `Contracts`',
  'SELECT 1')
  FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Contracts' AND INDEX_NAME = 'IX_Contracts_ServiceId');
PREPARE stmt FROM @stmt;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
SET @stmt = (SELECT IF(COUNT(*) > 0,
  'ALTER TABLE `Contracts` DROP COLUMN `ServiceId`',
  'SELECT 1')
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Contracts' AND COLUMN_NAME = 'ServiceId');
PREPARE stmt FROM @stmt;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;");
        }
    }
}
