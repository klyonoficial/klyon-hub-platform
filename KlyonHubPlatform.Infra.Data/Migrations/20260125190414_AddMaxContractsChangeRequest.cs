using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxContractsChangeRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaxContractsApprovalStatus",
                table: "CoworkingAddresses",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "None")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "RequestedMaxContracts",
                table: "CoworkingAddresses",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "MaxContractsChangeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CoworkingAddressId = table.Column<int>(type: "int", nullable: false),
                    RequestedMaxContracts = table.Column<int>(type: "int", nullable: false),
                    PreviousMaxContracts = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Justification = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RejectionReason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewedBy = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaxContractsChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaxContractsChangeRequests_CoworkingAddresses_CoworkingAddre~",
                        column: x => x.CoworkingAddressId,
                        principalTable: "CoworkingAddresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaxContractsChangeRequests_Users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MaxContractsChangeRequests_CoworkingAddressId",
                table: "MaxContractsChangeRequests",
                column: "CoworkingAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_MaxContractsChangeRequests_ReviewedBy",
                table: "MaxContractsChangeRequests",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_MaxContractsChangeRequests_Status",
                table: "MaxContractsChangeRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaxContractsChangeRequests");

            migrationBuilder.DropColumn(
                name: "MaxContractsApprovalStatus",
                table: "CoworkingAddresses");

            migrationBuilder.DropColumn(
                name: "RequestedMaxContracts",
                table: "CoworkingAddresses");
        }
    }
}
