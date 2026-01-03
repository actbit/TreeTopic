using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeTopic.Migrations.TenantCatalog
{
    /// <inheritdoc />
    public partial class FixOpenIdConnectColumnNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OpenIdConnecClientSecret",
                table: "TenantDetails",
                newName: "OpenIdConnectClientSecret");

            migrationBuilder.RenameColumn(
                name: "OpenIdConnecClientId",
                table: "TenantDetails",
                newName: "OpenIdConnectClientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OpenIdConnectClientSecret",
                table: "TenantDetails",
                newName: "OpenIdConnecClientSecret");

            migrationBuilder.RenameColumn(
                name: "OpenIdConnectClientId",
                table: "TenantDetails",
                newName: "OpenIdConnecClientId");
        }
    }
}
