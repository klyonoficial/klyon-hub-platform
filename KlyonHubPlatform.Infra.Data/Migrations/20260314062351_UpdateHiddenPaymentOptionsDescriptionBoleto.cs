using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateHiddenPaymentOptionsDescriptionBoleto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE SystemConfigurations
                SET Description = 'Formas de pagamento a ocultar (separadas por vírgula). Valores: PixAnnual, PIX, Card1x, CardMonthly, Boleto. Ex.: Boleto oculta boleto bancário; PIX oculta PIX (pagamento único).'
                WHERE `Key` = 'Payment.HiddenPaymentOptions';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE SystemConfigurations
                SET Description = 'Formas de pagamento a ocultar (separadas por vírgula). Valores: PixAnnual, PIX, Card1x, CardMonthly.'
                WHERE `Key` = 'Payment.HiddenPaymentOptions';
            ");
        }
    }
}
