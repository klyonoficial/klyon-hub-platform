using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlyonHubPlatform.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoworkingRoomsAndPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoworkingRooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CoworkingAddressId = table.Column<int>(type: "int", nullable: false),
                    RoomType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    CreditsPerHour = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoworkingRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoworkingRooms_CoworkingAddresses_CoworkingAddressId",
                        column: x => x.CoworkingAddressId,
                        principalTable: "CoworkingAddresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CoworkingRoomPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CoworkingRoomId = table.Column<int>(type: "int", nullable: false),
                    FilePath = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsMain = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoworkingRoomPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoworkingRoomPhotos_CoworkingRooms_CoworkingRoomId",
                        column: x => x.CoworkingRoomId,
                        principalTable: "CoworkingRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "CoworkingRoomId",
                table: "Reservations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoworkingRooms_CoworkingAddressId",
                table: "CoworkingRooms",
                column: "CoworkingAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_CoworkingRoomPhotos_CoworkingRoomId",
                table: "CoworkingRoomPhotos",
                column: "CoworkingRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_CoworkingRoomId",
                table: "Reservations",
                column: "CoworkingRoomId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_CoworkingRooms_CoworkingRoomId",
                table: "Reservations",
                column: "CoworkingRoomId",
                principalTable: "CoworkingRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_CoworkingRooms_CoworkingRoomId",
                table: "Reservations");

            migrationBuilder.DropTable(name: "CoworkingRoomPhotos");
            migrationBuilder.DropTable(name: "CoworkingRooms");

            migrationBuilder.DropIndex(name: "IX_Reservations_CoworkingRoomId", table: "Reservations");
            migrationBuilder.DropColumn(name: "CoworkingRoomId", table: "Reservations");
        }
    }
}
