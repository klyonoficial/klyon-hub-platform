using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHiddenPaymentOptionsConfig : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO SystemConfigurations (`Key`, `Value`, Description, Category, IsSensitive, IsEncrypted, CreatedAt, UpdatedAt, UpdatedBy)
                SELECT 'Payment.HiddenPaymentOptions', '', 'Formas de pagamento a ocultar (separadas por vírgula). Valores: PixAnnual, PIX, Card1x, CardMonthly.', 'Payment', 0, 0, UTC_TIMESTAMP(), NULL, NULL
                WHERE NOT EXISTS (SELECT 1 FROM SystemConfigurations WHERE `Key` = 'Payment.HiddenPaymentOptions');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM SystemConfigurations WHERE `Key` = 'Payment.HiddenPaymentOptions';");
        }
    }
}
