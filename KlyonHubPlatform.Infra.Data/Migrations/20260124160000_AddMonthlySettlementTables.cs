using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlySettlementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MonthlySettlements table
            migrationBuilder.CreateTable(
                name: "MonthlySettlements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CoworkingId = table.Column<int>(type: "int", nullable: false),
                    ReferenceMonth = table.Column<int>(type: "int", nullable: false),
                    ReferenceYear = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    TotalGrossAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    TotalPlatformCommission = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    CommissionCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClosedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ScheduledTransferDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    InvoiceDeadline = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TransferredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExternalTransferId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PixKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureReason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApprovedBy = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Notes = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlySettlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthlySettlements_Coworkings_CoworkingId",
                        column: x => x.CoworkingId,
                        principalTable: "Coworkings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MonthlySettlements_Users_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // SettlementCommissions table
            migrationBuilder.CreateTable(
                name: "SettlementCommissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    MonthlySettlementId = table.Column<int>(type: "int", nullable: false),
                    CommissionId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementCommissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettlementCommissions_MonthlySettlements_MonthlySettlementId",
                        column: x => x.MonthlySettlementId,
                        principalTable: "MonthlySettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SettlementCommissions_Commissions_CommissionId",
                        column: x => x.CommissionId,
                        principalTable: "Commissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // PartnerInvoices table
            migrationBuilder.CreateTable(
                name: "PartnerInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    MonthlySettlementId = table.Column<int>(type: "int", nullable: false),
                    CoworkingId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InvoiceAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InvoiceSeries = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssueDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IssuerCnpj = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IssuerName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AccessKey = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VerificationCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PdfFilePath = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    XmlFilePath = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    XmlContent = table.Column<string>(type: "MEDIUMTEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubmittedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Deadline = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AutoValidationPassed = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    AutoValidationMessage = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ValidatedBy = table.Column<int>(type: "int", nullable: true),
                    ValidatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RejectionReason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsAutoGenerated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FocusNFeRef = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PartnerNotes = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AdminNotes = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerInvoices_MonthlySettlements_MonthlySettlementId",
                        column: x => x.MonthlySettlementId,
                        principalTable: "MonthlySettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartnerInvoices_Coworkings_CoworkingId",
                        column: x => x.CoworkingId,
                        principalTable: "Coworkings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartnerInvoices_Users_ValidatedBy",
                        column: x => x.ValidatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Indexes for MonthlySettlements
            migrationBuilder.CreateIndex(
                name: "IX_MonthlySettlements_CoworkingId_ReferenceYear_ReferenceMonth",
                table: "MonthlySettlements",
                columns: new[] { "CoworkingId", "ReferenceYear", "ReferenceMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySettlements_Status",
                table: "MonthlySettlements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySettlements_ScheduledTransferDate",
                table: "MonthlySettlements",
                column: "ScheduledTransferDate");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySettlements_ExternalTransferId",
                table: "MonthlySettlements",
                column: "ExternalTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySettlements_ApprovedBy",
                table: "MonthlySettlements",
                column: "ApprovedBy");

            // Indexes for SettlementCommissions
            migrationBuilder.CreateIndex(
                name: "IX_SettlementCommissions_MonthlySettlementId_CommissionId",
                table: "SettlementCommissions",
                columns: new[] { "MonthlySettlementId", "CommissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettlementCommissions_CommissionId",
                table: "SettlementCommissions",
                column: "CommissionId");

            // Indexes for PartnerInvoices
            migrationBuilder.CreateIndex(
                name: "IX_PartnerInvoices_MonthlySettlementId",
                table: "PartnerInvoices",
                column: "MonthlySettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerInvoices_CoworkingId",
                table: "PartnerInvoices",
                column: "CoworkingId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerInvoices_Status",
                table: "PartnerInvoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerInvoices_ValidatedBy",
                table: "PartnerInvoices",
                column: "ValidatedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PartnerInvoices");
            migrationBuilder.DropTable(name: "SettlementCommissions");
            migrationBuilder.DropTable(name: "MonthlySettlements");
        }
    }
}
