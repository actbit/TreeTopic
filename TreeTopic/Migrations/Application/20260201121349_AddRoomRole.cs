using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeTopic.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddRoomRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RoomRoleId",
                table: "RoomUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RoomRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoomRolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomRolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomRolePermissions_RoomRoles_RoomRoleId",
                        column: x => x.RoomRoleId,
                        principalTable: "RoomRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoomUsers_RoomRoleId",
                table: "RoomUsers",
                column: "RoomRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomRolePermissions_RoomRoleId",
                table: "RoomRolePermissions",
                column: "RoomRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_RoomUsers_RoomRoles_RoomRoleId",
                table: "RoomUsers",
                column: "RoomRoleId",
                principalTable: "RoomRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoomUsers_RoomRoles_RoomRoleId",
                table: "RoomUsers");

            migrationBuilder.DropTable(
                name: "RoomRolePermissions");

            migrationBuilder.DropTable(
                name: "RoomRoles");

            migrationBuilder.DropIndex(
                name: "IX_RoomUsers_RoomRoleId",
                table: "RoomUsers");

            migrationBuilder.DropColumn(
                name: "RoomRoleId",
                table: "RoomUsers");
        }
    }
}
