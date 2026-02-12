using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreeTopic.Migrations.MySQL
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConcurrencyStamp = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    DisplayName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IconFileName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Sub = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsBanned = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    BannedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    BannedBy = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BanReason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedUserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmailConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PasswordHash = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SecurityStamp = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConcurrencyStamp = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PhoneNumber = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PhoneNumberConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RoomRoles",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomRoles", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RoleId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    ClaimType = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClaimValue = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RoleId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Permissions_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    ClaimType = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClaimValue = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderKey = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderDisplayName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    RoleId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    LoginProvider = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Value = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    UserId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    Endpoint = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    P256dhKey = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AuthKey = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PushSubscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    JoinPolicy = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedUserId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rooms_AspNetUsers_CreatedUserId",
                        column: x => x.CreatedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RoomRolePermissions",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    RoomRoleId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    PermissionName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomRolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomRolePermissions_RoomRoles_RoomRoleId",
                        column: x => x.RoomRoleId,
                        principalTable: "RoomRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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

            migrationBuilder.CreateTable(
                name: "RoomUsers",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    ApplicationUserId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    RoomId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UseMainName = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IconFileName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UseMainIcon = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomUsers_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoomUsers_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RoomPermissions",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    RoomUserId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomPermissions_RoomUsers_RoomUserId",
                        column: x => x.RoomUserId,
                        principalTable: "RoomUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RoomUserRoomRoles",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    RoomUserId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    RoomRoleId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomUserRoomRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomUserRoomRoles_RoomRoles_RoomRoleId",
                        column: x => x.RoomRoleId,
                        principalTable: "RoomRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomUserRoomRoles_RoomUsers_RoomUserId",
                        column: x => x.RoomUserId,
                        principalTable: "RoomUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BrainBoards",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    TopicId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsSign = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrainBoards", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BrainIdeas",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    BrainBoardId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    TopicId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    RoomUserId = table.Column<byte[]>(type: "BINARY(16)", nullable: true),
                    Idea = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PositionTop = table.Column<double>(type: "double", nullable: false),
                    PositionLeft = table.Column<double>(type: "double", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrainIdeas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrainIdeas_BrainBoards_BrainBoardId",
                        column: x => x.BrainBoardId,
                        principalTable: "BrainBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BrainIdeas_RoomUsers_RoomUserId",
                        column: x => x.RoomUserId,
                        principalTable: "RoomUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BrainIdeaVotes",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    BrainIdeaId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    RoomUserId = table.Column<byte[]>(type: "BINARY(16)", nullable: true),
                    VoteType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Value = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrainIdeaVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrainIdeaVotes_BrainIdeas_BrainIdeaId",
                        column: x => x.BrainIdeaId,
                        principalTable: "BrainIdeas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BrainIdeaVotes_RoomUsers_RoomUserId",
                        column: x => x.RoomUserId,
                        principalTable: "RoomUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Files",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    SourceFileId = table.Column<byte[]>(type: "BINARY(16)", nullable: true),
                    MessageId = table.Column<byte[]>(type: "BINARY(16)", nullable: true),
                    FileName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SaveFileName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsLatest = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Files_Files_SourceFileId",
                        column: x => x.SourceFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    TopicId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    RoomUserId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    Header = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Body = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReplyId = table.Column<byte[]>(type: "BINARY(16)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Messages_ReplyId",
                        column: x => x.ReplyId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Messages_RoomUsers_RoomUserId",
                        column: x => x.RoomUserId,
                        principalTable: "RoomUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    RoomId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    ParentId = table.Column<byte[]>(type: "BINARY(16)", nullable: true),
                    SourceMessageId = table.Column<byte[]>(type: "BINARY(16)", nullable: true),
                    Title = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Topics_Messages_SourceMessageId",
                        column: x => x.SourceMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Topics_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Topics_Topics_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ShareItems",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    RoomId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    TopicId = table.Column<byte[]>(type: "BINARY(16)", nullable: true),
                    BrainBoardId = table.Column<byte[]>(type: "BINARY(16)", nullable: true),
                    Kind = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedByRoomUserId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    CreatedByName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceMessageId = table.Column<byte[]>(type: "BINARY(16)", nullable: true),
                    SourceFileId = table.Column<byte[]>(type: "BINARY(16)", nullable: true),
                    SourceShareItemId = table.Column<byte[]>(type: "BINARY(16)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShareItems_BrainBoards_BrainBoardId",
                        column: x => x.BrainBoardId,
                        principalTable: "BrainBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShareItems_Files_SourceFileId",
                        column: x => x.SourceFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShareItems_Messages_SourceMessageId",
                        column: x => x.SourceMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShareItems_RoomUsers_CreatedByRoomUserId",
                        column: x => x.CreatedByRoomUserId,
                        principalTable: "RoomUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShareItems_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShareItems_ShareItems_SourceShareItemId",
                        column: x => x.SourceShareItemId,
                        principalTable: "ShareItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShareItems_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TopicRolePermissions",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    TopicId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    RoomRoleId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    Name = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TopicUserPermissions",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    TopicId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    RoomUserId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    Name = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserTopics",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    UserId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    TopicId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    LastReadMessageId = table.Column<byte[]>(type: "BINARY(16)", nullable: true),
                    LastAccessAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsAccessible = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTopics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTopics_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ShareItemFiles",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    ShareItemId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    FileId = table.Column<byte[]>(type: "BINARY(16)", nullable: false),
                    IsCurrent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareItemFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShareItemFiles_Files_FileId",
                        column: x => x.FileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShareItemFiles_ShareItems_ShareItemId",
                        column: x => x.ShareItemId,
                        principalTable: "ShareItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TenantId_Sub",
                table: "AspNetUsers",
                columns: new[] { "TenantId", "Sub" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrainBoards_TopicId",
                table: "BrainBoards",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_BrainIdeas_BrainBoardId",
                table: "BrainIdeas",
                column: "BrainBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_BrainIdeas_RoomUserId",
                table: "BrainIdeas",
                column: "RoomUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BrainIdeas_TopicId",
                table: "BrainIdeas",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_BrainIdeaVotes_BrainIdeaId",
                table: "BrainIdeaVotes",
                column: "BrainIdeaId");

            migrationBuilder.CreateIndex(
                name: "IX_BrainIdeaVotes_RoomUserId",
                table: "BrainIdeaVotes",
                column: "RoomUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_MessageId",
                table: "Files",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_SourceFileId",
                table: "Files",
                column: "SourceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReplyId",
                table: "Messages",
                column: "ReplyId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_RoomUserId",
                table: "Messages",
                column: "RoomUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TopicId_CreatedAt_Id",
                table: "Messages",
                columns: new[] { "TopicId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_RoleId",
                table: "Permissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_TenantId_UserId_Endpoint",
                table: "PushSubscriptions",
                columns: new[] { "TenantId", "UserId", "Endpoint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_UserId",
                table: "PushSubscriptions",
                column: "UserId");

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

            migrationBuilder.CreateIndex(
                name: "IX_RoomPermissions_RoomUserId",
                table: "RoomPermissions",
                column: "RoomUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomRolePermissions_RoomRoleId",
                table: "RoomRolePermissions",
                column: "RoomRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_CreatedUserId",
                table: "Rooms",
                column: "CreatedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomUserRoomRoles_RoomRoleId",
                table: "RoomUserRoomRoles",
                column: "RoomRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomUserRoomRoles_RoomUserId_RoomRoleId",
                table: "RoomUserRoomRoles",
                columns: new[] { "RoomUserId", "RoomRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomUsers_ApplicationUserId",
                table: "RoomUsers",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomUsers_RoomId_ApplicationUserId",
                table: "RoomUsers",
                columns: new[] { "RoomId", "ApplicationUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShareItemFiles_FileId",
                table: "ShareItemFiles",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareItemFiles_ShareItemId",
                table: "ShareItemFiles",
                column: "ShareItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareItems_BrainBoardId",
                table: "ShareItems",
                column: "BrainBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareItems_CreatedByRoomUserId",
                table: "ShareItems",
                column: "CreatedByRoomUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareItems_RoomId",
                table: "ShareItems",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareItems_SourceFileId",
                table: "ShareItems",
                column: "SourceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareItems_SourceMessageId",
                table: "ShareItems",
                column: "SourceMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareItems_SourceShareItemId",
                table: "ShareItems",
                column: "SourceShareItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareItems_TopicId",
                table: "ShareItems",
                column: "TopicId");

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
                name: "IX_Topics_ParentId",
                table: "Topics",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_RoomId",
                table: "Topics",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_SourceMessageId",
                table: "Topics",
                column: "SourceMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicUserPermissions_RoomUserId",
                table: "TopicUserPermissions",
                column: "RoomUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicUserPermissions_TopicId_RoomUserId_Name",
                table: "TopicUserPermissions",
                columns: new[] { "TopicId", "RoomUserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTopics_TopicId",
                table: "UserTopics",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTopics_UserId_TopicId",
                table: "UserTopics",
                columns: new[] { "UserId", "TopicId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BrainBoards_Topics_TopicId",
                table: "BrainBoards",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BrainIdeas_Topics_TopicId",
                table: "BrainIdeas",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Messages_MessageId",
                table: "Files",
                column: "MessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Topics_TopicId",
                table: "Messages",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rooms_AspNetUsers_CreatedUserId",
                table: "Rooms");

            migrationBuilder.DropForeignKey(
                name: "FK_RoomUsers_AspNetUsers_ApplicationUserId",
                table: "RoomUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Topics_TopicId",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "BrainIdeaVotes");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "PushSubscriptions");

            migrationBuilder.DropTable(
                name: "RoomJoinRolePermissions");

            migrationBuilder.DropTable(
                name: "RoomJoinUserPermissions");

            migrationBuilder.DropTable(
                name: "RoomPermissions");

            migrationBuilder.DropTable(
                name: "RoomRolePermissions");

            migrationBuilder.DropTable(
                name: "RoomUserRoomRoles");

            migrationBuilder.DropTable(
                name: "ShareItemFiles");

            migrationBuilder.DropTable(
                name: "TopicRolePermissions");

            migrationBuilder.DropTable(
                name: "TopicUserPermissions");

            migrationBuilder.DropTable(
                name: "UserTopics");

            migrationBuilder.DropTable(
                name: "BrainIdeas");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "ShareItems");

            migrationBuilder.DropTable(
                name: "RoomRoles");

            migrationBuilder.DropTable(
                name: "BrainBoards");

            migrationBuilder.DropTable(
                name: "Files");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Topics");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "RoomUsers");

            migrationBuilder.DropTable(
                name: "Rooms");
        }
    }
}
