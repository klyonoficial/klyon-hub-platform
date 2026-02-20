using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFocusNFeWebhookLogTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FocusNFeWebhookLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Ref = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CnpjEmitente = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DocumentType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Evento = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Numero = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CodigoVerificacao = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CaminhoXml = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FiscalNoteId = table.Column<int>(type: "int", nullable: true),
                    ProcessingStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RawPayload = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceIp = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReceivedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ProcessingAttempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FocusNFeWebhookLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FocusNFeWebhookLogs_FiscalNotes_FiscalNoteId",
                        column: x => x.FiscalNoteId,
                        principalTable: "FiscalNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FocusNFeWebhookLogs_CnpjEmitente",
                table: "FocusNFeWebhookLogs",
                column: "CnpjEmitente");

            migrationBuilder.CreateIndex(
                name: "IX_FocusNFeWebhookLogs_FiscalNoteId",
                table: "FocusNFeWebhookLogs",
                column: "FiscalNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_FocusNFeWebhookLogs_ProcessingStatus",
                table: "FocusNFeWebhookLogs",
                column: "ProcessingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_FocusNFeWebhookLogs_ReceivedAt",
                table: "FocusNFeWebhookLogs",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FocusNFeWebhookLogs_Ref",
                table: "FocusNFeWebhookLogs",
                column: "Ref");

            migrationBuilder.CreateIndex(
                name: "IX_FocusNFeWebhookLogs_Ref_Status_Evento",
                table: "FocusNFeWebhookLogs",
                columns: new[] { "Ref", "Status", "Evento" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FocusNFeWebhookLogs");
        }
    }
}
