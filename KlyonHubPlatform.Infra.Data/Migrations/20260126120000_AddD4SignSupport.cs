using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddD4SignSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "D4SignDocumentUuid",
                table: "Documents",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Inserir configurações D4Sign se não existirem (parametrizáveis na tela de administrador)
            migrationBuilder.Sql(@"
                INSERT INTO SystemConfigurations (`Key`, `Value`, `Description`, `Category`, `IsEncrypted`, `IsSensitive`, `CreatedAt`)
                SELECT 'D4Sign.TokenAPI', '', 'Token da API D4Sign (menu Dev API na conta D4Sign)', 'D4Sign', 0, 1, UTC_TIMESTAMP()
                FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM SystemConfigurations WHERE `Key` = 'D4Sign.TokenAPI');
            ");
            migrationBuilder.Sql(@"
                INSERT INTO SystemConfigurations (`Key`, `Value`, `Description`, `Category`, `IsEncrypted`, `IsSensitive`, `CreatedAt`)
                SELECT 'D4Sign.CryptKey', '', 'Chave criptográfica D4Sign (obrigatória se habilitada na conta)', 'D4Sign', 0, 1, UTC_TIMESTAMP()
                FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM SystemConfigurations WHERE `Key` = 'D4Sign.CryptKey');
            ");
            migrationBuilder.Sql(@"
                INSERT INTO SystemConfigurations (`Key`, `Value`, `Description`, `Category`, `IsEncrypted`, `IsSensitive`, `CreatedAt`)
                SELECT 'D4Sign.UseSandbox', 'true', 'Usar ambiente sandbox D4Sign (true/false). Produção: false.', 'D4Sign', 0, 0, UTC_TIMESTAMP()
                FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM SystemConfigurations WHERE `Key` = 'D4Sign.UseSandbox');
            ");
            migrationBuilder.Sql(@"
                INSERT INTO SystemConfigurations (`Key`, `Value`, `Description`, `Category`, `IsEncrypted`, `IsSensitive`, `CreatedAt`)
                SELECT 'D4Sign.SafeUuid', '', 'UUID do cofre D4Sign onde os contratos serão armazenados (GET /safes)', 'D4Sign', 0, 0, UTC_TIMESTAMP()
                FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM SystemConfigurations WHERE `Key` = 'D4Sign.SafeUuid');
            ");
            migrationBuilder.Sql(@"
                INSERT INTO SystemConfigurations (`Key`, `Value`, `Description`, `Category`, `IsEncrypted`, `IsSensitive`, `CreatedAt`)
                SELECT 'Upload.MaxPhotoSizeMB', '5', 'Tamanho máximo por foto (MB) em uploads: galeria, sócios, salas, logo. Ex.: 5 ou 10.', 'Upload', 0, 0, UTC_TIMESTAMP()
                FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM SystemConfigurations WHERE `Key` = 'Upload.MaxPhotoSizeMB');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "D4SignDocumentUuid",
                table: "Documents");

            migrationBuilder.Sql("DELETE FROM SystemConfigurations WHERE `Key` IN ('D4Sign.TokenAPI', 'D4Sign.CryptKey', 'D4Sign.UseSandbox', 'D4Sign.SafeUuid', 'Upload.MaxPhotoSizeMB');");
        }
    }
}
