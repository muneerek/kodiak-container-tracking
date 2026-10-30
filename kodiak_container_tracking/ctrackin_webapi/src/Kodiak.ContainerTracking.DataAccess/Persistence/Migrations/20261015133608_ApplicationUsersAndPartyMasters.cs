using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Kodiak.ContainerTracking.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationUsersAndPartyMasters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_userRoleMapping_userDetails_createdBy",
                schema: "ctracking",
                table: "userRoleMapping");

            migrationBuilder.DropForeignKey(
                name: "FK_userRoleMapping_userDetails_fkUserId_userType",
                schema: "ctracking",
                table: "userRoleMapping");

            migrationBuilder.DropForeignKey(
                name: "FK_userRoleMapping_userDetails_updatedBy",
                schema: "ctracking",
                table: "userRoleMapping");

            migrationBuilder.DropForeignKey(
                name: "FK_userRoles_userDetails_createdBy",
                schema: "ctracking",
                table: "userRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_userRoles_userDetails_updatedBy",
                schema: "ctracking",
                table: "userRoles");

            // Rename in place to retain account IDs and role mappings.
            migrationBuilder.RenameTable(name: "userDetails", schema: "ctracking", newName: "applicationUsers");
            migrationBuilder.Sql("ALTER TABLE ctracking.\"applicationUsers\" RENAME CONSTRAINT \"PK_userDetails\" TO \"PK_applicationUsers\";");
            migrationBuilder.Sql("ALTER TABLE ctracking.\"applicationUsers\" RENAME CONSTRAINT \"AK_userDetails_userId_userType\" TO \"AK_applicationUsers_userId_userType\";");
            migrationBuilder.Sql("ALTER TABLE ctracking.\"applicationUsers\" RENAME CONSTRAINT \"FK_userDetails_userDetails_createdBy\" TO \"FK_applicationUsers_applicationUsers_createdBy\";");
            migrationBuilder.Sql("ALTER TABLE ctracking.\"applicationUsers\" RENAME CONSTRAINT \"FK_userDetails_userDetails_updatedBy\" TO \"FK_applicationUsers_applicationUsers_updatedBy\";");
            migrationBuilder.DropCheckConstraint(name: "ck_user_status", schema: "ctracking", table: "applicationUsers");

            migrationBuilder.CreateTable(
                name: "partyReferences",
                schema: "ctracking",
                columns: table => new
                {
                    uniqueId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userType = table.Column<int>(type: "integer", nullable: false),
                    uniqueCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partyReferences", x => x.uniqueId);
                    table.UniqueConstraint("AK_partyReferences_uniqueId_userType_uniqueCode", x => new { x.uniqueId, x.userType, x.uniqueCode });
                    table.CheckConstraint("ck_party_reference_type", "\"userType\" IN (1, 2, 3)");
                });

            migrationBuilder.CreateTable(
                name: "uniqueCodeCounters",
                schema: "ctracking",
                columns: table => new
                {
                    userType = table.Column<int>(type: "integer", nullable: false),
                    lastNumber = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uniqueCodeCounters", x => x.userType);
                });

            migrationBuilder.AddColumn<long>(name: "uniqueId", schema: "ctracking", table: "applicationUsers", type: "bigint", nullable: true);
            migrationBuilder.AddColumn<string>(name: "uniqueCode", schema: "ctracking", table: "applicationUsers", type: "character varying(100)", maxLength: 100, nullable: true);

            migrationBuilder.CreateTable(
                name: "Consignees",
                schema: "ctracking",
                columns: table => new
                {
                    consigneeId = table.Column<long>(type: "bigint", nullable: false),
                    contactName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    createOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<long>(type: "bigint", nullable: true),
                    updatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedBy = table.Column<long>(type: "bigint", nullable: true),
                    userType = table.Column<int>(type: "integer", nullable: false),
                    uniqueCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    emailId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    phoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consignees", x => x.consigneeId);
                    table.CheckConstraint("ck_Consignees_type", "\"userType\" = 2");
                    table.ForeignKey(
                        name: "FK_Consignees_applicationUsers_createdBy",
                        column: x => x.createdBy,
                        principalSchema: "ctracking",
                        principalTable: "applicationUsers",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Consignees_applicationUsers_updatedBy",
                        column: x => x.updatedBy,
                        principalSchema: "ctracking",
                        principalTable: "applicationUsers",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Consignees_partyReferences_consigneeId_userType_uniqueCode",
                        columns: x => new { x.consigneeId, x.userType, x.uniqueCode },
                        principalSchema: "ctracking",
                        principalTable: "partyReferences",
                        principalColumns: new[] { "uniqueId", "userType", "uniqueCode" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "ctracking",
                columns: table => new
                {
                    employeeId = table.Column<long>(type: "bigint", nullable: false),
                    firstName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    lastName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    dob = table.Column<DateOnly>(type: "date", nullable: true),
                    createOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<long>(type: "bigint", nullable: true),
                    updatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedBy = table.Column<long>(type: "bigint", nullable: true),
                    userType = table.Column<int>(type: "integer", nullable: false),
                    uniqueCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    emailId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    phoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.employeeId);
                    table.CheckConstraint("ck_Employees_type", "\"userType\" = 1");
                    table.ForeignKey(
                        name: "FK_Employees_applicationUsers_createdBy",
                        column: x => x.createdBy,
                        principalSchema: "ctracking",
                        principalTable: "applicationUsers",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_applicationUsers_updatedBy",
                        column: x => x.updatedBy,
                        principalSchema: "ctracking",
                        principalTable: "applicationUsers",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_partyReferences_employeeId_userType_uniqueCode",
                        columns: x => new { x.employeeId, x.userType, x.uniqueCode },
                        principalSchema: "ctracking",
                        principalTable: "partyReferences",
                        principalColumns: new[] { "uniqueId", "userType", "uniqueCode" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Shippers",
                schema: "ctracking",
                columns: table => new
                {
                    shipperId = table.Column<long>(type: "bigint", nullable: false),
                    contactName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    createOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<long>(type: "bigint", nullable: true),
                    updatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedBy = table.Column<long>(type: "bigint", nullable: true),
                    userType = table.Column<int>(type: "integer", nullable: false),
                    uniqueCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    emailId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    phoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shippers", x => x.shipperId);
                    table.CheckConstraint("ck_Shippers_type", "\"userType\" = 3");
                    table.ForeignKey(
                        name: "FK_Shippers_applicationUsers_createdBy",
                        column: x => x.createdBy,
                        principalSchema: "ctracking",
                        principalTable: "applicationUsers",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shippers_applicationUsers_updatedBy",
                        column: x => x.updatedBy,
                        principalSchema: "ctracking",
                        principalTable: "applicationUsers",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shippers_partyReferences_shipperId_userType_uniqueCode",
                        columns: x => new { x.shipperId, x.userType, x.uniqueCode },
                        principalSchema: "ctracking",
                        principalTable: "partyReferences",
                        principalColumns: new[] { "uniqueId", "userType", "uniqueCode" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "ctracking",
                table: "uniqueCodeCounters",
                columns: new[] { "userType", "lastNumber" },
                values: new object[,]
                {
                    { 1, 0L },
                    { 2, 0L },
                    { 3, 0L }
                });

            migrationBuilder.RenameIndex(name: "IX_userDetails_createdBy", schema: "ctracking", table: "applicationUsers", newName: "IX_applicationUsers_createdBy");

            migrationBuilder.RenameIndex(name: "IX_userDetails_normalizedUserName", schema: "ctracking", table: "applicationUsers", newName: "IX_applicationUsers_normalizedUserName");

            migrationBuilder.CreateIndex(
                name: "IX_applicationUsers_uniqueCode",
                schema: "ctracking",
                table: "applicationUsers",
                column: "uniqueCode");

            migrationBuilder.CreateIndex(
                name: "IX_applicationUsers_uniqueId_userType_uniqueCode",
                schema: "ctracking",
                table: "applicationUsers",
                columns: new[] { "uniqueId", "userType", "uniqueCode" });

            migrationBuilder.RenameIndex(name: "IX_userDetails_updatedBy", schema: "ctracking", table: "applicationUsers", newName: "IX_applicationUsers_updatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Consignees_consigneeId_userType_uniqueCode",
                schema: "ctracking",
                table: "Consignees",
                columns: new[] { "consigneeId", "userType", "uniqueCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Consignees_createdBy",
                schema: "ctracking",
                table: "Consignees",
                column: "createdBy");

            migrationBuilder.CreateIndex(
                name: "IX_Consignees_uniqueCode",
                schema: "ctracking",
                table: "Consignees",
                column: "uniqueCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Consignees_updatedBy",
                schema: "ctracking",
                table: "Consignees",
                column: "updatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_createdBy",
                schema: "ctracking",
                table: "Employees",
                column: "createdBy");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_employeeId_userType_uniqueCode",
                schema: "ctracking",
                table: "Employees",
                columns: new[] { "employeeId", "userType", "uniqueCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_uniqueCode",
                schema: "ctracking",
                table: "Employees",
                column: "uniqueCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_updatedBy",
                schema: "ctracking",
                table: "Employees",
                column: "updatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_partyReferences_uniqueCode",
                schema: "ctracking",
                table: "partyReferences",
                column: "uniqueCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shippers_createdBy",
                schema: "ctracking",
                table: "Shippers",
                column: "createdBy");

            migrationBuilder.CreateIndex(
                name: "IX_Shippers_shipperId_userType_uniqueCode",
                schema: "ctracking",
                table: "Shippers",
                columns: new[] { "shipperId", "userType", "uniqueCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shippers_uniqueCode",
                schema: "ctracking",
                table: "Shippers",
                column: "uniqueCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shippers_updatedBy",
                schema: "ctracking",
                table: "Shippers",
                column: "updatedBy");

            migrationBuilder.Sql(ReadBackfillSql());
            migrationBuilder.AlterColumn<long>(name: "uniqueId", schema: "ctracking", table: "applicationUsers", type: "bigint", nullable: false, oldClrType: typeof(long), oldType: "bigint", oldNullable: true);
            migrationBuilder.AlterColumn<string>(name: "uniqueCode", schema: "ctracking", table: "applicationUsers", type: "character varying(100)", maxLength: 100, nullable: false, oldClrType: typeof(string), oldType: "character varying(100)", oldMaxLength: 100, oldNullable: true);
            migrationBuilder.AddForeignKey(name: "FK_applicationUsers_partyReferences_uniqueId_userType_uniqueCo~", schema: "ctracking", table: "applicationUsers",
                columns: new[] { "uniqueId", "userType", "uniqueCode" }, principalSchema: "ctracking", principalTable: "partyReferences",
                principalColumns: new[] { "uniqueId", "userType", "uniqueCode" }, onDelete: ReferentialAction.Restrict);
            migrationBuilder.DropIndex(name: "IX_userDetails_normalizedEmail", schema: "ctracking", table: "applicationUsers");
            foreach (var column in new[] { "firstName", "lastName", "dob", "emailId", "normalizedEmail", "userStatus", "emailConfirmed" })
                migrationBuilder.DropColumn(name: column, schema: "ctracking", table: "applicationUsers");

            migrationBuilder.AddForeignKey(
                name: "FK_userRoleMapping_applicationUsers_createdBy",
                schema: "ctracking",
                table: "userRoleMapping",
                column: "createdBy",
                principalSchema: "ctracking",
                principalTable: "applicationUsers",
                principalColumn: "userId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_userRoleMapping_applicationUsers_fkUserId_userType",
                schema: "ctracking",
                table: "userRoleMapping",
                columns: new[] { "fkUserId", "userType" },
                principalSchema: "ctracking",
                principalTable: "applicationUsers",
                principalColumns: new[] { "userId", "userType" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_userRoleMapping_applicationUsers_updatedBy",
                schema: "ctracking",
                table: "userRoleMapping",
                column: "updatedBy",
                principalSchema: "ctracking",
                principalTable: "applicationUsers",
                principalColumn: "userId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_userRoles_applicationUsers_createdBy",
                schema: "ctracking",
                table: "userRoles",
                column: "createdBy",
                principalSchema: "ctracking",
                principalTable: "applicationUsers",
                principalColumn: "userId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_userRoles_applicationUsers_updatedBy",
                schema: "ctracking",
                table: "userRoles",
                column: "updatedBy",
                principalSchema: "ctracking",
                principalTable: "applicationUsers",
                principalColumn: "userId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("This migration removes obsolete account fields. Restore a pre-upgrade backup to roll back safely.");

        private static string ReadBackfillSql()
        {
            using var stream = typeof(ApplicationUsersAndPartyMasters).Assembly.GetManifestResourceStream(
                "Kodiak.ContainerTracking.DataAccess.Persistence.Migrations.Sql.BackfillApplicationUsers.sql")
                ?? throw new InvalidOperationException("Embedded migration SQL was not found.");
            using var reader = new System.IO.StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
