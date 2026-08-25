using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Inscrição Imobiliária e Representante Legal do parceiro, usados no contrato/declaração PDF.
    /// </summary>
    public partial class Migration_AddPartnerContractFieldsToCoworking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegalRepresentative",
                table: "Coworkings",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PropertyRegistration",
                table: "Coworkings",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LegalRepresentative",
                table: "Coworkings");

            migrationBuilder.DropColumn(
                name: "PropertyRegistration",
                table: "Coworkings");
        }
    }
}
