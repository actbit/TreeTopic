using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeTopic.Migrations.TenantCatalog
{
    /// <inheritdoc />
    public partial class AddTenantObfuscationKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TenantObfuscationKeyK0",
                table: "Tenants",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TenantObfuscationKeyK1",
                table: "Tenants",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantObfuscationKeyK0",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TenantObfuscationKeyK1",
                table: "Tenants");
        }
    }
}
