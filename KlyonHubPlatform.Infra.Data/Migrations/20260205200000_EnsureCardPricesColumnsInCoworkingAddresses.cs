using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <summary>
    /// Garante que as colunas CardPrice1x, CardPriceMonthly e CardPrice12x existam em CoworkingAddresses.
    /// Idempotente: só adiciona cada coluna se ela não existir (útil quando AddCardPricesToCoworkingAddress não foi aplicada).
    /// </summary>
    public partial class EnsureCardPricesColumnsInCoworkingAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var addColumnIfNotExists = @"
SET @stmt = (SELECT IF(COUNT(*) = 0, 'ALTER TABLE `CoworkingAddresses` ADD COLUMN `CardPrice1x` DECIMAL(18,2) NULL', 'SELECT 1')
  FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CoworkingAddresses' AND COLUMN_NAME = 'CardPrice1x');
PREPARE stmt FROM @stmt;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;";
            migrationBuilder.Sql(addColumnIfNotExists);

            addColumnIfNotExists = @"
SET @stmt = (SELECT IF(COUNT(*) = 0, 'ALTER TABLE `CoworkingAddresses` ADD COLUMN `CardPriceMonthly` DECIMAL(18,2) NULL', 'SELECT 1')
  FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CoworkingAddresses' AND COLUMN_NAME = 'CardPriceMonthly');
PREPARE stmt FROM @stmt;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;";
            migrationBuilder.Sql(addColumnIfNotExists);

            addColumnIfNotExists = @"
SET @stmt = (SELECT IF(COUNT(*) = 0, 'ALTER TABLE `CoworkingAddresses` ADD COLUMN `CardPrice12x` DECIMAL(18,2) NULL', 'SELECT 1')
  FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CoworkingAddresses' AND COLUMN_NAME = 'CardPrice12x');
PREPARE stmt FROM @stmt;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;";
            migrationBuilder.Sql(addColumnIfNotExists);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não remove as colunas no Down para evitar quebrar se outras migrações dependem delas
        }
    }
}
