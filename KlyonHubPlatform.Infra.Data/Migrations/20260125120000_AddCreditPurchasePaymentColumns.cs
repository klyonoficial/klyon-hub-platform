using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <summary>
    /// Adiciona colunas de pagamento (PIX, Boleto, Cartão) em CreditPurchases.
    /// Necessário quando a tabela foi criada pelo Database Manager (CreateCreditsTables) com o schema antigo.
    /// </summary>
    public partial class AddCreditPurchasePaymentColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "CreditPurchases",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ExternalPaymentId",
                table: "CreditPurchases",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PixCode",
                table: "CreditPurchases",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "PixExpiresAt",
                table: "CreditPurchases",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoletoCode",
                table: "CreditPurchases",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BoletoBarcode",
                table: "CreditPurchases",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "BoletoExpiresAt",
                table: "CreditPurchases",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionId",
                table: "CreditPurchases",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PaymentMethod", table: "CreditPurchases");
            migrationBuilder.DropColumn(name: "ExternalPaymentId", table: "CreditPurchases");
            migrationBuilder.DropColumn(name: "PixCode", table: "CreditPurchases");
            migrationBuilder.DropColumn(name: "PixExpiresAt", table: "CreditPurchases");
            migrationBuilder.DropColumn(name: "BoletoCode", table: "CreditPurchases");
            migrationBuilder.DropColumn(name: "BoletoBarcode", table: "CreditPurchases");
            migrationBuilder.DropColumn(name: "BoletoExpiresAt", table: "CreditPurchases");
            migrationBuilder.DropColumn(name: "TransactionId", table: "CreditPurchases");
        }
    }
}
