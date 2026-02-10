using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentInstallmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InstallmentCount",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InstallmentValue",
                table: "Payments",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InstallmentCount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "InstallmentValue",
                table: "Payments");
        }
    }
}
