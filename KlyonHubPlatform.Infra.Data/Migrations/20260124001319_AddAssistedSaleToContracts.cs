using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistedSaleToContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssistedSaleAdminId",
                table: "Contracts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAssistedSale",
                table: "Contracts",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_AssistedSaleAdminId",
                table: "Contracts",
                column: "AssistedSaleAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Users_AssistedSaleAdminId",
                table: "Contracts",
                column: "AssistedSaleAdminId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Users_AssistedSaleAdminId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_AssistedSaleAdminId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "AssistedSaleAdminId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "IsAssistedSale",
                table: "Contracts");
        }
    }
}
