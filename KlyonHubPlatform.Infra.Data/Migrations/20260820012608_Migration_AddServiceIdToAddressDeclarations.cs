using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Adiciona ServiceId em AddressDeclarations. Idempotente se a coluna já existir.
    /// </summary>
    public partial class Migration_AddServiceIdToAddressDeclarations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET @stmt = (SELECT IF(COUNT(*) = 0,
  'ALTER TABLE `AddressDeclarations` ADD COLUMN `ServiceId` int NULL',
  'SELECT 1')
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'AddressDeclarations' AND COLUMN_NAME = 'ServiceId');
PREPARE stmt FROM @stmt;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET @stmt = (SELECT IF(COUNT(*) > 0,
  'ALTER TABLE `AddressDeclarations` DROP COLUMN `ServiceId`',
  'SELECT 1')
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'AddressDeclarations' AND COLUMN_NAME = 'ServiceId');
PREPARE stmt FROM @stmt;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;");
        }
    }
}
