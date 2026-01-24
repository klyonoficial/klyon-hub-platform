using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalCapacityToCoworkingAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adicionar campos de capacidade fiscal em CoworkingAddresses
            migrationBuilder.AddColumn<int>(
                name: "RequestedFiscalCapacity",
                table: "CoworkingAddresses",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedFiscalCapacity",
                table: "CoworkingAddresses",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "CapacityApprovalStatus",
                table: "CoworkingAddresses",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "None")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Inicializar capacidade aprovada com MaxContracts para endereços existentes
            migrationBuilder.Sql("UPDATE CoworkingAddresses SET ApprovedFiscalCapacity = MaxContracts, RequestedFiscalCapacity = MaxContracts WHERE 1 = 1;");

            // Criar tabela CapacityChangeRequests
            migrationBuilder.CreateTable(
                name: "CapacityChangeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CoworkingAddressId = table.Column<int>(type: "int", nullable: false),
                    RequestedCapacity = table.Column<int>(type: "int", nullable: false),
                    PreviousCapacity = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_CapacityChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CapacityChangeRequests_CoworkingAddresses_CoworkingAddressId",
                        column: x => x.CoworkingAddressId,
                        principalTable: "CoworkingAddresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CapacityChangeRequests_Users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CapacityChangeRequests_CoworkingAddressId",
                table: "CapacityChangeRequests",
                column: "CoworkingAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_CapacityChangeRequests_Status",
                table: "CapacityChangeRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CapacityChangeRequests");

            migrationBuilder.DropColumn(
                name: "RequestedFiscalCapacity",
                table: "CoworkingAddresses");

            migrationBuilder.DropColumn(
                name: "ApprovedFiscalCapacity",
                table: "CoworkingAddresses");

            migrationBuilder.DropColumn(
                name: "CapacityApprovalStatus",
                table: "CoworkingAddresses");
        }
    }
}
