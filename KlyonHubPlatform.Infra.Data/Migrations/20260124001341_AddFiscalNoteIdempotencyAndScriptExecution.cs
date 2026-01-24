using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalNoteIdempotencyAndScriptExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotência: chave única evita NF duplicada para mesmo reserva/contrato + tipo
            // Verificar e adicionar coluna apenas se não existir (idempotente)
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = @dbname
                    AND TABLE_NAME = 'FiscalNotes'
                    AND COLUMN_NAME = 'IdempotencyKey');
                SET @sql = IF(@col_exists = 0,
                    'ALTER TABLE `FiscalNotes` ADD COLUMN `IdempotencyKey` VARCHAR(64) CHARACTER SET utf8mb4 NULL',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;");

            // Verificar e criar índice apenas se não existir (idempotente)
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
                    WHERE TABLE_SCHEMA = @dbname
                    AND TABLE_NAME = 'FiscalNotes'
                    AND INDEX_NAME = 'IX_FiscalNotes_IdempotencyKey');
                SET @sql = IF(@idx_exists = 0,
                    'CREATE UNIQUE INDEX `IX_FiscalNotes_IdempotencyKey` ON `FiscalNotes` (`IdempotencyKey`)',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;");

            // Registro de execução do script (data, usuário, quantidade de NFs)
            // Verificar se a tabela já existe antes de criar (idempotente)
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @table_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_SCHEMA = @dbname
                    AND TABLE_NAME = 'FiscalNoteScriptExecutions');
                SET @sql = IF(@table_exists = 0,
                    'CREATE TABLE `FiscalNoteScriptExecutions` (
                        `Id` INT NOT NULL AUTO_INCREMENT,
                        `ExecutedAt` DATETIME(6) NOT NULL,
                        `ExecutedByUserId` INT NULL,
                        `ExecutedByUserName` VARCHAR(255) NOT NULL,
                        `NoteType` VARCHAR(20) NOT NULL,
                        `StartDate` DATETIME(6) NOT NULL,
                        `EndDate` DATETIME(6) NOT NULL,
                        `NotesCreated` INT NOT NULL,
                        `NotesEmitted` INT NOT NULL,
                        `ErrorsCount` INT NOT NULL,
                        `UnsupportedStatesCount` INT NOT NULL,
                        `ErrorDetails` VARCHAR(4000) NULL,
                        PRIMARY KEY (`Id`),
                        CONSTRAINT `FK_FiscalNoteScriptExecutions_Users_ExecutedByUserId` 
                            FOREIGN KEY (`ExecutedByUserId`) REFERENCES `Users` (`Id`) ON DELETE SET NULL
                    ) DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;");

            // Criar índices apenas se a tabela foi criada (ou se os índices não existem)
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
                    WHERE TABLE_SCHEMA = @dbname
                    AND TABLE_NAME = 'FiscalNoteScriptExecutions'
                    AND INDEX_NAME = 'IX_FiscalNoteScriptExecutions_ExecutedAt');
                SET @sql = IF(@idx_exists = 0,
                    'CREATE INDEX `IX_FiscalNoteScriptExecutions_ExecutedAt` ON `FiscalNoteScriptExecutions` (`ExecutedAt`)',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
                    WHERE TABLE_SCHEMA = @dbname
                    AND TABLE_NAME = 'FiscalNoteScriptExecutions'
                    AND INDEX_NAME = 'IX_FiscalNoteScriptExecutions_ExecutedByUserId');
                SET @sql = IF(@idx_exists = 0,
                    'CREATE INDEX `IX_FiscalNoteScriptExecutions_ExecutedByUserId` ON `FiscalNoteScriptExecutions` (`ExecutedByUserId`)',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FiscalNoteScriptExecutions");

            migrationBuilder.DropIndex(
                name: "IX_FiscalNotes_IdempotencyKey",
                table: "FiscalNotes");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "FiscalNotes");
        }
    }
}
