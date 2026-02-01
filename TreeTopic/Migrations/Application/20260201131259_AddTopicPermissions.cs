using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeTopic.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddTopicPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TopicRolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicRolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TopicRolePermissions_RoomRoles_RoomRoleId",
                        column: x => x.RoomRoleId,
                        principalTable: "RoomRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TopicRolePermissions_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TopicUserPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicUserPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TopicUserPermissions_RoomUsers_RoomUserId",
                        column: x => x.RoomUserId,
                        principalTable: "RoomUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TopicUserPermissions_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TopicRolePermissions_RoomRoleId",
                table: "TopicRolePermissions",
                column: "RoomRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicRolePermissions_TopicId_RoomRoleId_Name",
                table: "TopicRolePermissions",
                columns: new[] { "TopicId", "RoomRoleId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopicUserPermissions_RoomUserId",
                table: "TopicUserPermissions",
                column: "RoomUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicUserPermissions_TopicId_RoomUserId_Name",
                table: "TopicUserPermissions",
                columns: new[] { "TopicId", "RoomUserId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TopicRolePermissions");

            migrationBuilder.DropTable(
                name: "TopicUserPermissions");
        }
    }
}
