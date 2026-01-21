using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoworkingApprovalFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "Coworkings",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "UnderReview")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "Coworkings",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Coworkings",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "Coworkings",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RejectedBy",
                table: "Coworkings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Coworkings",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedBy",
                table: "Coworkings",
                type: "int",
                nullable: true);

            // Aprovar cadastros existentes que já estavam ativos e como parceiro (mantém no marketplace)
            migrationBuilder.Sql(@"
                UPDATE Coworkings 
                SET ApprovalStatus = 'Approved', 
                    ApprovedAt = COALESCE(UpdatedAt, CreatedAt),
                    SubmittedAt = CreatedAt
                WHERE IsActive = 1 AND IsPartner = 1;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ApprovalStatus", table: "Coworkings");
            migrationBuilder.DropColumn(name: "SubmittedAt", table: "Coworkings");
            migrationBuilder.DropColumn(name: "RejectionReason", table: "Coworkings");
            migrationBuilder.DropColumn(name: "RejectedAt", table: "Coworkings");
            migrationBuilder.DropColumn(name: "RejectedBy", table: "Coworkings");
            migrationBuilder.DropColumn(name: "ApprovedAt", table: "Coworkings");
            migrationBuilder.DropColumn(name: "ApprovedBy", table: "Coworkings");
        }
    }
}
