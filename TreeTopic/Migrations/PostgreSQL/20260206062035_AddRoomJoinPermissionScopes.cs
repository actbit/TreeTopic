using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeTopic.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddRoomJoinPermissionScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoomJoinRolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "RoomJoinUserPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
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
                });

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
