using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientOnboardingDocumentsAndPartnerTrustLicenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DocumentsApproved",
                table: "Contracts",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE Contracts SET DocumentsApproved = 1 WHERE Status IN (0, 5);");

            migrationBuilder.AddColumn<string>(
                name: "DocumentsRejectionReason",
                table: "Contracts",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "DocumentsReviewedAt",
                table: "Contracts",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentsReviewedByUserId",
                table: "Contracts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClientOnboardingDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "varchar(260)", maxLength: 260, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FilePath = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RejectionReason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientOnboardingDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientOnboardingDocuments_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientOnboardingDocuments_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientOnboardingDocuments_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_DocumentsReviewedByUserId",
                table: "Contracts",
                column: "DocumentsReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientOnboardingDocuments_ClientId",
                table: "ClientOnboardingDocuments",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientOnboardingDocuments_ContractId_Type",
                table: "ClientOnboardingDocuments",
                columns: new[] { "ContractId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientOnboardingDocuments_ReviewedByUserId",
                table: "ClientOnboardingDocuments",
                column: "ReviewedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Users_DocumentsReviewedByUserId",
                table: "Contracts",
                column: "DocumentsReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Users_DocumentsReviewedByUserId",
                table: "Contracts");

            migrationBuilder.DropTable(
                name: "ClientOnboardingDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_DocumentsReviewedByUserId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DocumentsApproved",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DocumentsRejectionReason",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DocumentsReviewedAt",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DocumentsReviewedByUserId",
                table: "Contracts");
        }
    }
}
