using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContractSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adicionar campos de snapshot (congelamento de dados contratuais)
            // Verificar se as colunas já existem antes de adicionar (podem ter sido criadas em migration anterior)
            
            // SnapshotMonthlyValue
            migrationBuilder.Sql(@"
                SET @exist = (SELECT COUNT(*) FROM information_schema.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() 
                    AND TABLE_NAME = 'Contracts' 
                    AND COLUMN_NAME = 'SnapshotMonthlyValue');
                SET @sqlstmt = IF(@exist = 0, 
                    'ALTER TABLE Contracts ADD COLUMN SnapshotMonthlyValue DECIMAL(18,2) NOT NULL DEFAULT 0', 
                    'SELECT 1');
                PREPARE stmt FROM @sqlstmt;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // SnapshotPlatformCommissionPercentage
            migrationBuilder.Sql(@"
                SET @exist = (SELECT COUNT(*) FROM information_schema.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() 
                    AND TABLE_NAME = 'Contracts' 
                    AND COLUMN_NAME = 'SnapshotPlatformCommissionPercentage');
                SET @sqlstmt = IF(@exist = 0, 
                    'ALTER TABLE Contracts ADD COLUMN SnapshotPlatformCommissionPercentage DECIMAL(18,2) NOT NULL DEFAULT 0', 
                    'SELECT 1');
                PREPARE stmt FROM @sqlstmt;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // SnapshotCoworkingCommissionPercentage
            migrationBuilder.Sql(@"
                SET @exist = (SELECT COUNT(*) FROM information_schema.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() 
                    AND TABLE_NAME = 'Contracts' 
                    AND COLUMN_NAME = 'SnapshotCoworkingCommissionPercentage');
                SET @sqlstmt = IF(@exist = 0, 
                    'ALTER TABLE Contracts ADD COLUMN SnapshotCoworkingCommissionPercentage DECIMAL(18,2) NOT NULL DEFAULT 0', 
                    'SELECT 1');
                PREPARE stmt FROM @sqlstmt;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // SnapshotAddress
            migrationBuilder.Sql(@"
                SET @exist = (SELECT COUNT(*) FROM information_schema.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() 
                    AND TABLE_NAME = 'Contracts' 
                    AND COLUMN_NAME = 'SnapshotAddress');
                SET @sqlstmt = IF(@exist = 0, 
                    'ALTER TABLE Contracts ADD COLUMN SnapshotAddress VARCHAR(1000) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''''', 
                    'SELECT 1');
                PREPARE stmt FROM @sqlstmt;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // SnapshotCommissionPercentage
            migrationBuilder.Sql(@"
                SET @exist = (SELECT COUNT(*) FROM information_schema.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() 
                    AND TABLE_NAME = 'Contracts' 
                    AND COLUMN_NAME = 'SnapshotCommissionPercentage');
                SET @sqlstmt = IF(@exist = 0, 
                    'ALTER TABLE Contracts ADD COLUMN SnapshotCommissionPercentage DECIMAL(18,2) NOT NULL DEFAULT 0', 
                    'SELECT 1');
                PREPARE stmt FROM @sqlstmt;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // SnapshotCapacityConsumed
            migrationBuilder.Sql(@"
                SET @exist = (SELECT COUNT(*) FROM information_schema.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() 
                    AND TABLE_NAME = 'Contracts' 
                    AND COLUMN_NAME = 'SnapshotCapacityConsumed');
                SET @sqlstmt = IF(@exist = 0, 
                    'ALTER TABLE Contracts ADD COLUMN SnapshotCapacityConsumed INT NOT NULL DEFAULT 0', 
                    'SELECT 1');
                PREPARE stmt FROM @sqlstmt;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Para contratos existentes, popular os campos de snapshot com os valores atuais
            // Isso garante retrocompatibilidade
            migrationBuilder.Sql(@"
                UPDATE Contracts c
                INNER JOIN CoworkingAddresses ca ON c.CoworkingAddressId = ca.Id
                SET 
                    c.SnapshotMonthlyValue = c.MonthlyValue,
                    c.SnapshotAddress = CONCAT(
                        ca.Address, ', ', ca.Number,
                        IF(ca.Complement IS NOT NULL AND ca.Complement != '', CONCAT(' - ', ca.Complement), ''),
                        ', ', ca.Neighborhood, ', ', ca.City, '/', ca.State, ', CEP: ', ca.ZipCode
                    ),
                    c.SnapshotCapacityConsumed = ca.CurrentContracts
                WHERE c.SnapshotMonthlyValue = 0;
            ");

            // Para comissões, usar valor padrão de 15% (pode ser ajustado posteriormente se necessário)
            migrationBuilder.Sql(@"
                UPDATE Contracts 
                SET 
                    SnapshotPlatformCommissionPercentage = 15.0,
                    SnapshotCoworkingCommissionPercentage = 85.0,
                    SnapshotCommissionPercentage = 15.0
                WHERE SnapshotCommissionPercentage = 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SnapshotMonthlyValue", table: "Contracts");
            migrationBuilder.DropColumn(name: "SnapshotPlatformCommissionPercentage", table: "Contracts");
            migrationBuilder.DropColumn(name: "SnapshotCoworkingCommissionPercentage", table: "Contracts");
            migrationBuilder.DropColumn(name: "SnapshotAddress", table: "Contracts");
            migrationBuilder.DropColumn(name: "SnapshotCommissionPercentage", table: "Contracts");
            migrationBuilder.DropColumn(name: "SnapshotCapacityConsumed", table: "Contracts");
        }
    }
}
