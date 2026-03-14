using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPixAutomaticPayloadToSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PixAutomaticConciliationIdentifier",
                table: "Subscriptions",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PixAutomaticPayload",
                table: "Subscriptions",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "PixAutomaticPayloadExpiresAt",
                table: "Subscriptions",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PixAutomaticConciliationIdentifier",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PixAutomaticPayload",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PixAutomaticPayloadExpiresAt",
                table: "Subscriptions");
        }
    }
}
