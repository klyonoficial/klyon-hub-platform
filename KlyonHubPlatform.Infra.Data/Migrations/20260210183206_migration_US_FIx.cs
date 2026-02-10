using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class migration_US_FIx : Migration
    {
        /// <inheritdoc />
        /// <summary>Corrige typo no nome da coluna: ClicksignEnvelopeld -> ClicksignEnvelopeId (se existir no banco).</summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @exists = (SELECT COUNT(*) FROM information_schema.COLUMNS 
                    WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = 'Documents' AND COLUMN_NAME = 'ClicksignEnvelopeld');
                SET @sql = IF(@exists > 0, 
                    'ALTER TABLE Documents CHANGE COLUMN ClicksignEnvelopeld ClicksignEnvelopeId VARCHAR(64) NULL', 
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @exists = (SELECT COUNT(*) FROM information_schema.COLUMNS 
                    WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = 'Documents' AND COLUMN_NAME = 'ClicksignEnvelopeId');
                SET @sql = IF(@exists > 0, 
                    'ALTER TABLE Documents CHANGE COLUMN ClicksignEnvelopeId ClicksignEnvelopeld VARCHAR(64) NULL', 
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }
    }
}
