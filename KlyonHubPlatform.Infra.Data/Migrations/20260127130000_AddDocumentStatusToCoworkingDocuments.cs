using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentStatusToCoworkingDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "CoworkingDocuments",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "CoworkingDocuments",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Documentos existentes são considerados aprovados (retrocompatibilidade)
            migrationBuilder.Sql("UPDATE CoworkingDocuments SET Status = 'Approved' WHERE 1 = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Status", table: "CoworkingDocuments");
            migrationBuilder.DropColumn(name: "RejectionReason", table: "CoworkingDocuments");
        }
    }
}
