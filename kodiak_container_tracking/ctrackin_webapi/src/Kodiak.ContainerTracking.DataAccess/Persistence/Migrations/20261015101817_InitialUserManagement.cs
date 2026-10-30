using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kodiak.ContainerTracking.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialUserManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ctracking");

            migrationBuilder.CreateTable(
                name: "accessAuditEvents",
                schema: "ctracking",
                columns: table => new
                {
                    eventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurredOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actorSubject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    changes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accessAuditEvents", x => x.eventId);
                });

            migrationBuilder.CreateTable(
                name: "userDetails",
                schema: "ctracking",
                columns: table => new
                {
                    userId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userType = table.Column<int>(type: "integer", nullable: false),
                    firstName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    lastName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    dob = table.Column<DateOnly>(type: "date", nullable: true),
                    emailId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalizedEmail = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    userName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    normalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    userPassword = table.Column<string>(type: "text", nullable: true),
                    emailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    userStatus = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    createOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<long>(type: "bigint", nullable: true),
                    updatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userDetails", x => x.userId);
                    table.UniqueConstraint("AK_userDetails_userId_userType", x => new { x.userId, x.userType });
                    table.CheckConstraint("ck_user_status", "\"userStatus\" IN (1, 2, 3)");
                    table.CheckConstraint("ck_user_type", "\"userType\" IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_userDetails_userDetails_createdBy",
                        column: x => x.createdBy,
                        principalSchema: "ctracking",
                        principalTable: "userDetails",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_userDetails_userDetails_updatedBy",
                        column: x => x.updatedBy,
                        principalSchema: "ctracking",
                        principalTable: "userDetails",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "userRoles",
                schema: "ctracking",
                columns: table => new
                {
                    roleId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalizedCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    roleName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    userType = table.Column<int>(type: "integer", nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    createOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<long>(type: "bigint", nullable: true),
                    updatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userRoles", x => x.roleId);
                    table.UniqueConstraint("AK_userRoles_roleId_userType", x => new { x.roleId, x.userType });
                    table.CheckConstraint("ck_role_type", "\"userType\" IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_userRoles_userDetails_createdBy",
                        column: x => x.createdBy,
                        principalSchema: "ctracking",
                        principalTable: "userDetails",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_userRoles_userDetails_updatedBy",
                        column: x => x.updatedBy,
                        principalSchema: "ctracking",
                        principalTable: "userDetails",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "userRoleMapping",
                schema: "ctracking",
                columns: table => new
                {
                    mappingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fkUserId = table.Column<long>(type: "bigint", nullable: false),
                    fkRoleId = table.Column<int>(type: "integer", nullable: false),
                    userType = table.Column<int>(type: "integer", nullable: false),
                    createOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<long>(type: "bigint", nullable: true),
                    updatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userRoleMapping", x => x.mappingId);
                    table.ForeignKey(
                        name: "FK_userRoleMapping_userDetails_createdBy",
                        column: x => x.createdBy,
                        principalSchema: "ctracking",
                        principalTable: "userDetails",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_userRoleMapping_userDetails_fkUserId_userType",
                        columns: x => new { x.fkUserId, x.userType },
                        principalSchema: "ctracking",
                        principalTable: "userDetails",
                        principalColumns: new[] { "userId", "userType" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_userRoleMapping_userDetails_updatedBy",
                        column: x => x.updatedBy,
                        principalSchema: "ctracking",
                        principalTable: "userDetails",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_userRoleMapping_userRoles_fkRoleId_userType",
                        columns: x => new { x.fkRoleId, x.userType },
                        principalSchema: "ctracking",
                        principalTable: "userRoles",
                        principalColumns: new[] { "roleId", "userType" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_userDetails_createdBy",
                schema: "ctracking",
                table: "userDetails",
                column: "createdBy");

            migrationBuilder.CreateIndex(
                name: "IX_userDetails_normalizedEmail",
                schema: "ctracking",
                table: "userDetails",
                column: "normalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_userDetails_normalizedUserName",
                schema: "ctracking",
                table: "userDetails",
                column: "normalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_userDetails_updatedBy",
                schema: "ctracking",
                table: "userDetails",
                column: "updatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_userRoleMapping_createdBy",
                schema: "ctracking",
                table: "userRoleMapping",
                column: "createdBy");

            migrationBuilder.CreateIndex(
                name: "IX_userRoleMapping_fkRoleId",
                schema: "ctracking",
                table: "userRoleMapping",
                column: "fkRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_userRoleMapping_fkRoleId_userType",
                schema: "ctracking",
                table: "userRoleMapping",
                columns: new[] { "fkRoleId", "userType" });

            migrationBuilder.CreateIndex(
                name: "IX_userRoleMapping_fkUserId_fkRoleId",
                schema: "ctracking",
                table: "userRoleMapping",
                columns: new[] { "fkUserId", "fkRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_userRoleMapping_fkUserId_userType",
                schema: "ctracking",
                table: "userRoleMapping",
                columns: new[] { "fkUserId", "userType" });

            migrationBuilder.CreateIndex(
                name: "IX_userRoleMapping_updatedBy",
                schema: "ctracking",
                table: "userRoleMapping",
                column: "updatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_userRoles_createdBy",
                schema: "ctracking",
                table: "userRoles",
                column: "createdBy");

            migrationBuilder.CreateIndex(
                name: "IX_userRoles_normalizedCode",
                schema: "ctracking",
                table: "userRoles",
                column: "normalizedCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_userRoles_updatedBy",
                schema: "ctracking",
                table: "userRoles",
                column: "updatedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accessAuditEvents",
                schema: "ctracking");

            migrationBuilder.DropTable(
                name: "userRoleMapping",
                schema: "ctracking");

            migrationBuilder.DropTable(
                name: "userRoles",
                schema: "ctracking");

            migrationBuilder.DropTable(
                name: "userDetails",
                schema: "ctracking");
        }
    }
}
