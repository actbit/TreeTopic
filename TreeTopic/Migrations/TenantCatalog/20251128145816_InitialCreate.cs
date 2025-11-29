using System;
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
                    DbProvider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TenantEncryptionKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ConnectionString = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    OpenIdConnectMetadataAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OpenIdConnectAuthority = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OpenIdConnectAuthorizationEndpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OpenIdConnectTokenEndpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OpenIdConnectJwksUri = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OpenIdConnectEndSessionEndpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OpenIdConnecClientId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OpenIdConnecClientSecret = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RoleClaimName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantObfuscationKeyK0 = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TenantObfuscationKeyK1 = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SetupTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApplicationTenantInfoId = table.Column<string>(type: "character varying(64)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetupTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetupTokens_Tenants_ApplicationTenantInfoId",
                        column: x => x.ApplicationTenantInfoId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SetupTokens_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SetupTokens_ApplicationTenantInfoId",
                table: "SetupTokens",
                column: "ApplicationTenantInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupTokens_TenantId",
                table: "SetupTokens",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupTokens_TokenHash",
                table: "SetupTokens",
                column: "TokenHash",
                unique: true);

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
                name: "SetupTokens");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
