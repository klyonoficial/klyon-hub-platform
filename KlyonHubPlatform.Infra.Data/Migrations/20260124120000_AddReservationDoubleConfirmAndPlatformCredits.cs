using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationDoubleConfirmAndPlatformCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Tabela PlatformCreditBalances (conta do administrador)
            migrationBuilder.CreateTable(
                name: "PlatformCreditBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", "MySql:Identity"),
                    TotalCredits = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_PlatformCreditBalances", x => x.Id))
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@"
                INSERT INTO PlatformCreditBalances (TotalCredits, UpdatedAt) 
                VALUES (0, UTC_TIMESTAMP());
            ");

            // 2) Colunas em Reservations: confirmação dupla
            migrationBuilder.AddColumn<DateTime>(
                name: "ClientConfirmedAt",
                table: "Reservations",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoworkingConfirmedAt",
                table: "Reservations",
                type: "datetime(6)",
                nullable: true);

            // 3) ReservationId em Transfers
            migrationBuilder.AddColumn<int>(
                name: "ReservationId",
                table: "Transfers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_ReservationId",
                table: "Transfers",
                column: "ReservationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transfers_Reservations_ReservationId",
                table: "Transfers",
                column: "ReservationId",
                principalTable: "Reservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // 4) Tabela ReservationCommissions
            migrationBuilder.CreateTable(
                name: "ReservationCommissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", "MySql:Identity"),
                    ReservationId = table.Column<int>(type: "int", nullable: false),
                    TotalCredits = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    CommissionPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    CoworkingAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationCommissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservationCommissions_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationCommissions_ReservationId",
                table: "ReservationCommissions",
                column: "ReservationId");

            // 5) Parâmetro de comissão (se não existir)
            migrationBuilder.Sql(@"
                INSERT INTO SystemConfigurations (`Key`, `Value`, `Description`, `Category`, `IsEncrypted`, `IsSensitive`, `CreatedAt`)
                SELECT 'Platform.CommissionPercentage', '20', 'Percentual de comissão da plataforma sobre reservas (ex: 20 = 20%). Alterável em Configurações do Sistema.', 'Platform', 0, 0, UTC_TIMESTAMP()
                FROM DUAL
                WHERE NOT EXISTS (SELECT 1 FROM SystemConfigurations WHERE `Key` = 'Platform.CommissionPercentage');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ReservationCommissions");
            migrationBuilder.DropForeignKey(name: "FK_Transfers_Reservations_ReservationId", table: "Transfers");
            migrationBuilder.DropIndex(name: "IX_Transfers_ReservationId", table: "Transfers");
            migrationBuilder.DropColumn(name: "ReservationId", table: "Transfers");
            migrationBuilder.DropColumn(name: "ClientConfirmedAt", table: "Reservations");
            migrationBuilder.DropColumn(name: "CoworkingConfirmedAt", table: "Reservations");
            migrationBuilder.DropTable(name: "PlatformCreditBalances");

            migrationBuilder.Sql("DELETE FROM SystemConfigurations WHERE `Key` = 'Platform.CommissionPercentage';");
        }
    }
}
