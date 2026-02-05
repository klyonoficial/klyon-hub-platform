using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCardPricesToCoworkingAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CardPrice1x",
                table: "CoworkingAddresses",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CardPriceMonthly",
                table: "CoworkingAddresses",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CardPrice12x",
                table: "CoworkingAddresses",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CardPrice1x", table: "CoworkingAddresses");
            migrationBuilder.DropColumn(name: "CardPriceMonthly", table: "CoworkingAddresses");
            migrationBuilder.DropColumn(name: "CardPrice12x", table: "CoworkingAddresses");
        }
    }
}
