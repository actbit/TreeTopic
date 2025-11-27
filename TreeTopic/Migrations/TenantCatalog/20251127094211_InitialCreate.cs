using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeTopic.Migrations.TenantCatalog
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Identifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    DbProvider = table.Column<string>(type: "text", nullable: true),
                    ConnectionString = table.Column<string>(type: "text", nullable: true),
                    OpenIdConnctAuthority = table.Column<string>(type: "text", nullable: true),
                    OpenIdConnecClientId = table.Column<string>(type: "text", nullable: true),
                    OpenIdConnecClientSecret = table.Column<string>(type: "text", nullable: true),
                    RoleClaimName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantObfuscationKeyK0 = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TenantObfuscationKeyK1 = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Name",
                table: "Tenants",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
