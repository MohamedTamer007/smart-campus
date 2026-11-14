using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartCampus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCampus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "smart_campus");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Language = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    UserName = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Email = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SecurityStamp = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Audit",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Ar = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    En = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CanReceive = table.Column<bool>(type: "boolean", nullable: false),
                    Maintenance = table.Column<bool>(type: "boolean", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Kind = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputVersion = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ClaimValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ClaimValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "smart_campus",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                schema: "smart_campus",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "smart_campus",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Name = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_RecipientId",
                        column: x => x.RecipientId,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtectedTicket = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AbsoluteExpiry = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UploaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustodianId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustodianDepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Place = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Storage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    State = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.CheckConstraint("custodian_xor", "(\"CustodianId\" IS NULL) <> (\"CustodianDepartmentId\" IS NULL)");
                    table.CheckConstraint("item_state", "\"State\" IN ('available','reserved','delivered')");
                    table.ForeignKey(
                        name: "FK_Items_AspNetUsers_CustodianId",
                        column: x => x.CustodianId,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Items_AspNetUsers_UploaderId",
                        column: x => x.UploaderId,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Items_Departments_CustodianDepartmentId",
                        column: x => x.CustodianDepartmentId,
                        principalSchema: "smart_campus",
                        principalTable: "Departments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Ar = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    En = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    MaintenanceDepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Locations_Departments_MaintenanceDepartmentId",
                        column: x => x.MaintenanceDepartmentId,
                        principalSchema: "smart_campus",
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Locations_Locations_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "smart_campus",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Scopes",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scopes_AspNetUsers_GrantedBy",
                        column: x => x.GrantedBy,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Scopes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Scopes_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "smart_campus",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Claims",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ReservationActive = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Phone = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Place = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Instructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ClosureReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DeliveredBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.Id);
                    table.CheckConstraint("reservation_approved", "NOT \"ReservationActive\" OR \"Status\" = 'approved'");
                    table.ForeignKey(
                        name: "FK_Claims_AspNetUsers_RequesterId",
                        column: x => x.RequesterId,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Claims_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "smart_campus",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Handovers",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromDepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Office = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ReceivedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NotReceivedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    NotReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Handovers", x => x.Id);
                    table.CheckConstraint("source_custodian_xor", "(\"FromUserId\" IS NULL) <> (\"FromDepartmentId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_Handovers_AspNetUsers_FromUserId",
                        column: x => x.FromUserId,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Handovers_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "smart_campus",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Handovers_Departments_FromDepartmentId",
                        column: x => x.FromDepartmentId,
                        principalSchema: "smart_campus",
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Handovers_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "smart_campus",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SearchGrants",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SearchGrants_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SearchGrants_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "smart_campus",
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacultyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    FloorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AssigneeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FaultType = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Priority = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Risk = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                    table.CheckConstraint("maintenance_status", "\"Status\" IN ('submitted','review','repair','resolved','rejected')");
                    table.ForeignKey(
                        name: "FK_Reports_AspNetUsers_AssigneeId",
                        column: x => x.AssigneeId,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reports_AspNetUsers_ReporterId",
                        column: x => x.ReporterId,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reports_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "smart_campus",
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reports_Locations_BuildingId",
                        column: x => x.BuildingId,
                        principalSchema: "smart_campus",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reports_Locations_FacultyId",
                        column: x => x.FacultyId,
                        principalSchema: "smart_campus",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reports_Locations_FloorId",
                        column: x => x.FloorId,
                        principalSchema: "smart_campus",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Assessments",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputVersion = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    FaultType = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Priority = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Risk = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Explanation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Model = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assessments_Reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "smart_campus",
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "History",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AssigneeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_History", x => x.Id);
                    table.ForeignKey(
                        name: "FK_History_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "smart_campus",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_History_Reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "smart_campus",
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Media",
                schema: "smart_campus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Bucket = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Media", x => x.Id);
                    table.CheckConstraint("media_parent_xor", "(\"ReportId\" IS NULL) <> (\"ItemId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_Media_Items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "smart_campus",
                        principalTable: "Items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Media_Reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "smart_campus",
                        principalTable: "Reports",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "smart_campus",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "smart_campus",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "smart_campus",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "smart_campus",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "smart_campus",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "smart_campus",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "smart_campus",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_ReportId_InputVersion",
                schema: "smart_campus",
                table: "Assessments",
                columns: new[] { "ReportId", "InputVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_ItemId",
                schema: "smart_campus",
                table: "Claims",
                column: "ItemId",
                unique: true,
                filter: "\"ReservationActive\"");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_ItemId_RequesterId",
                schema: "smart_campus",
                table: "Claims",
                columns: new[] { "ItemId", "RequesterId" },
                unique: true,
                filter: "\"Status\" IN ('pending','approved')");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_RequesterId",
                schema: "smart_campus",
                table: "Claims",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                schema: "smart_campus",
                table: "Departments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Handovers_DepartmentId",
                schema: "smart_campus",
                table: "Handovers",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Handovers_FromDepartmentId",
                schema: "smart_campus",
                table: "Handovers",
                column: "FromDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Handovers_FromUserId",
                schema: "smart_campus",
                table: "Handovers",
                column: "FromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Handovers_ItemId",
                schema: "smart_campus",
                table: "Handovers",
                column: "ItemId",
                unique: true,
                filter: "\"Status\" IN ('pending','not-received')");

            migrationBuilder.CreateIndex(
                name: "IX_History_ActorId",
                schema: "smart_campus",
                table: "History",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_History_ReportId",
                schema: "smart_campus",
                table: "History",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CustodianDepartmentId",
                schema: "smart_campus",
                table: "Items",
                column: "CustodianDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CustodianId",
                schema: "smart_campus",
                table: "Items",
                column: "CustodianId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_UploaderId",
                schema: "smart_campus",
                table: "Items",
                column: "UploaderId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Key",
                schema: "smart_campus",
                table: "Jobs",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_MaintenanceDepartmentId",
                schema: "smart_campus",
                table: "Locations",
                column: "MaintenanceDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_ParentId",
                schema: "smart_campus",
                table: "Locations",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Media_ItemId",
                schema: "smart_campus",
                table: "Media",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Media_ReportId",
                schema: "smart_campus",
                table: "Media",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientId_ReadAt_CreatedAt",
                schema: "smart_campus",
                table: "Notifications",
                columns: new[] { "RecipientId", "ReadAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_AssigneeId",
                schema: "smart_campus",
                table: "Reports",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_BuildingId",
                schema: "smart_campus",
                table: "Reports",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_DepartmentId_Status",
                schema: "smart_campus",
                table: "Reports",
                columns: new[] { "DepartmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_FacultyId",
                schema: "smart_campus",
                table: "Reports",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_FloorId",
                schema: "smart_campus",
                table: "Reports",
                column: "FloorId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Reference",
                schema: "smart_campus",
                table: "Reports",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReporterId_CreatedAt",
                schema: "smart_campus",
                table: "Reports",
                columns: new[] { "ReporterId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Scopes_DepartmentId",
                schema: "smart_campus",
                table: "Scopes",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Scopes_GrantedBy",
                schema: "smart_campus",
                table: "Scopes",
                column: "GrantedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Scopes_UserId_DepartmentId",
                schema: "smart_campus",
                table: "Scopes",
                columns: new[] { "UserId", "DepartmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchGrants_ItemId",
                schema: "smart_campus",
                table: "SearchGrants",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SearchGrants_UserId_ItemId_ExpiresAt",
                schema: "smart_campus",
                table: "SearchGrants",
                columns: new[] { "UserId", "ItemId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserId",
                schema: "smart_campus",
                table: "Sessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "Assessments",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "Audit",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "Claims",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "Handovers",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "History",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "Jobs",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "Media",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "Scopes",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "SearchGrants",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "Sessions",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "AspNetRoles",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "Reports",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "Items",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "Locations",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "AspNetUsers",
                schema: "smart_campus");

            migrationBuilder.DropTable(
                name: "Departments",
                schema: "smart_campus");
        }
    }
}
