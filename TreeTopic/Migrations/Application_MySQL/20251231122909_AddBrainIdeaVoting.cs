using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeTopic.Migrations.Application_MySQL
{
    /// <inheritdoc />
    public partial class AddBrainIdeaVoting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrainIdeaVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "BINARY(16)", nullable: false),
                    BrainIdeaId = table.Column<Guid>(type: "BINARY(16)", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "BINARY(16)", nullable: true),
                    VoteType = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrainIdeaVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrainIdeaVotes_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BrainIdeaVotes_BrainIdeas_BrainIdeaId",
                        column: x => x.BrainIdeaId,
                        principalTable: "BrainIdeas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrainIdeaVotes_ApplicationUserId",
                table: "BrainIdeaVotes",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BrainIdeaVotes_BrainIdeaId",
                table: "BrainIdeaVotes",
                column: "BrainIdeaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrainIdeaVotes");
        }
    }
}
