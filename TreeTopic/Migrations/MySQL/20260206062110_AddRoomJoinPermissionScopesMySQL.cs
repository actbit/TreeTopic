using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeTopic.Migrations.MySQL
{
    /// <inheritdoc />
    public partial class AddRoomJoinPermissionScopesMySQL : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoomJoinRolePermissions",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    RoomId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    RoleId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomJoinRolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomJoinRolePermissions_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomJoinRolePermissions_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RoomJoinUserPermissions",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    RoomId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    ApplicationUserId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomJoinUserPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomJoinUserPermissions_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomJoinUserPermissions_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RoomJoinRolePermissions_RoleId",
                table: "RoomJoinRolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomJoinRolePermissions_RoomId_RoleId",
                table: "RoomJoinRolePermissions",
                columns: new[] { "RoomId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomJoinUserPermissions_ApplicationUserId",
                table: "RoomJoinUserPermissions",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomJoinUserPermissions_RoomId_ApplicationUserId",
                table: "RoomJoinUserPermissions",
                columns: new[] { "RoomId", "ApplicationUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomJoinRolePermissions");

            migrationBuilder.DropTable(
                name: "RoomJoinUserPermissions");
        }
    }
}
