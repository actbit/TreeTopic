using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeTopic.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddTopicSourceMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceMessageId",
                table: "Topics",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Topics_SourceMessageId",
                table: "Topics",
                column: "SourceMessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Topics_Messages_SourceMessageId",
                table: "Topics",
                column: "SourceMessageId",
                principalTable: "Messages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Messages_SourceMessageId",
                table: "Topics");

            migrationBuilder.DropIndex(
                name: "IX_Topics_SourceMessageId",
                table: "Topics");

            migrationBuilder.DropColumn(
                name: "SourceMessageId",
                table: "Topics");
        }
    }
}
