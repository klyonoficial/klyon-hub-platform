using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class migration_AddCardPriceChangeRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CardPricesApprovalStatus",
                table: "CoworkingAddresses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CardPriceChangeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CoworkingAddressId = table.Column<int>(type: "int", nullable: false),
                    RequestedCardPrice1x = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    RequestedCardPriceMonthly = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    RequestedCardPrice12x = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    PreviousCardPrice1x = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    PreviousCardPriceMonthly = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    PreviousCardPrice12x = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    RejectionReason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewedBy = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardPriceChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardPriceChangeRequests_CoworkingAddresses_CoworkingAddressId",
                        column: x => x.CoworkingAddressId,
                        principalTable: "CoworkingAddresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CardPriceChangeRequests_Users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CardPriceChangeRequests_CoworkingAddressId",
                table: "CardPriceChangeRequests",
                column: "CoworkingAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_CardPriceChangeRequests_ReviewedBy",
                table: "CardPriceChangeRequests",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CardPriceChangeRequests_Status",
                table: "CardPriceChangeRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardPriceChangeRequests");

            migrationBuilder.DropColumn(
                name: "CardPricesApprovalStatus",
                table: "CoworkingAddresses");
        }
    }
}
