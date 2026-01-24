using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRemovalRequestFieldsToPartnerServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RemovalRequestStatus",
                table: "PartnerServices",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "None")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "RemovalRequestedAt",
                table: "PartnerServices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RemovalReviewedAt",
                table: "PartnerServices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemovalReviewedBy",
                table: "PartnerServices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemovalRejectionReason",
                table: "PartnerServices",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RemovalRequestStatus", table: "PartnerServices");
            migrationBuilder.DropColumn(name: "RemovalRequestedAt", table: "PartnerServices");
            migrationBuilder.DropColumn(name: "RemovalReviewedAt", table: "PartnerServices");
            migrationBuilder.DropColumn(name: "RemovalReviewedBy", table: "PartnerServices");
            migrationBuilder.DropColumn(name: "RemovalRejectionReason", table: "PartnerServices");
        }
    }
}
