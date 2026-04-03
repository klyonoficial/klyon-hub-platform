using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class Onboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FiscalOnboardingLeadId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FiscalOnboardingStatus",
                table: "Clients",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FiscalOnboardingLeads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CoworkingId = table.Column<int>(type: "int", nullable: false),
                    CoworkingAddressId = table.Column<int>(type: "int", nullable: false),
                    DeclaresHasCnpj = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TaxIdNormalized = table.Column<string>(type: "varchar(14)", maxLength: 14, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContactEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContactPhone = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CnpjRazaoSocialSnapshot = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CnpjCnaePrincipalSnapshot = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AsaasCustomerId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PublicPaymentToken = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalOnboardingLeads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalOnboardingLeads_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FiscalOnboardingLeads_CoworkingAddresses_CoworkingAddressId",
                        column: x => x.CoworkingAddressId,
                        principalTable: "CoworkingAddresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FiscalOnboardingLeads_Coworkings_CoworkingId",
                        column: x => x.CoworkingId,
                        principalTable: "Coworkings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FiscalOnboardingLeads_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClientMagicLoginTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FiscalOnboardingLeadId = table.Column<int>(type: "int", nullable: false),
                    Token = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientMagicLoginTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientMagicLoginTokens_FiscalOnboardingLeads_FiscalOnboardin~",
                        column: x => x.FiscalOnboardingLeadId,
                        principalTable: "FiscalOnboardingLeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_FiscalOnboardingLeadId",
                table: "Payments",
                column: "FiscalOnboardingLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientMagicLoginTokens_FiscalOnboardingLeadId",
                table: "ClientMagicLoginTokens",
                column: "FiscalOnboardingLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientMagicLoginTokens_Token",
                table: "ClientMagicLoginTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiscalOnboardingLeads_ClientId",
                table: "FiscalOnboardingLeads",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalOnboardingLeads_CoworkingAddressId",
                table: "FiscalOnboardingLeads",
                column: "CoworkingAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalOnboardingLeads_CoworkingId",
                table: "FiscalOnboardingLeads",
                column: "CoworkingId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalOnboardingLeads_PublicPaymentToken",
                table: "FiscalOnboardingLeads",
                column: "PublicPaymentToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiscalOnboardingLeads_TaxIdNormalized_CoworkingAddressId_Sta~",
                table: "FiscalOnboardingLeads",
                columns: new[] { "TaxIdNormalized", "CoworkingAddressId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalOnboardingLeads_UserId",
                table: "FiscalOnboardingLeads",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_FiscalOnboardingLeads_FiscalOnboardingLeadId",
                table: "Payments",
                column: "FiscalOnboardingLeadId",
                principalTable: "FiscalOnboardingLeads",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_FiscalOnboardingLeads_FiscalOnboardingLeadId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "ClientMagicLoginTokens");

            migrationBuilder.DropTable(
                name: "FiscalOnboardingLeads");

            migrationBuilder.DropIndex(
                name: "IX_Payments_FiscalOnboardingLeadId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "FiscalOnboardingLeadId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "FiscalOnboardingStatus",
                table: "Clients");
        }
    }
}
