using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeTopic.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddMessageUnreadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_TopicId",
                table: "Messages");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TopicId_CreatedAt_Id",
                table: "Messages",
                columns: new[] { "TopicId", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_TopicId_CreatedAt_Id",
                table: "Messages");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TopicId",
                table: "Messages",
                column: "TopicId");
        }
    }
}
