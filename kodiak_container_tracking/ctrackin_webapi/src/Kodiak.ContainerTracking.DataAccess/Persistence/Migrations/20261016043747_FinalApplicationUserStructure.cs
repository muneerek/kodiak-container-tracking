using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kodiak.ContainerTracking.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinalApplicationUserStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE ctracking."legacyStructureArchive" (
                    "archiveId" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    "sourceTable" text NOT NULL, "sourceId" bigint NOT NULL, "data" jsonb NOT NULL, "archivedOn" timestamptz NOT NULL DEFAULT now());
                INSERT INTO ctracking."legacyStructureArchive" ("sourceTable", "sourceId", "data") SELECT 'Employees', "employeeId", to_jsonb(e) FROM ctracking."Employees" e;
                INSERT INTO ctracking."legacyStructureArchive" ("sourceTable", "sourceId", "data") SELECT 'Consignees', "consigneeId", to_jsonb(e) FROM ctracking."Consignees" e;
                INSERT INTO ctracking."legacyStructureArchive" ("sourceTable", "sourceId", "data") SELECT 'Shippers', "shipperId", to_jsonb(e) FROM ctracking."Shippers" e;
                INSERT INTO ctracking."legacyStructureArchive" ("sourceTable", "sourceId", "data") SELECT 'applicationUsers', "userId", to_jsonb(e) - 'userPassword' FROM ctracking."applicationUsers" e;
                DO $guard$ BEGIN
                    IF EXISTS(SELECT 1 FROM ctracking."applicationUsers" WHERE "userId" > 2147483647 OR "uniqueId" > 2147483647 OR length("uniqueCode") > 50)
                        OR EXISTS(SELECT 1 FROM ctracking."partyReferences" WHERE "uniqueId" > 2147483647 OR length("uniqueCode") > 50)
                        OR EXISTS(SELECT 1 FROM ctracking."userRoleMapping" WHERE "mappingId" > 2147483647 OR "fkUserId" > 2147483647)
                        OR EXISTS(SELECT 1 FROM ctracking."Consignees" WHERE length("companyName") > 300)
                        OR EXISTS(SELECT 1 FROM ctracking."Shippers" WHERE length("companyName") > 300)
                    THEN RAISE EXCEPTION 'Existing IDs/codes/names exceed the final schema limits. Resolve them before migration; no data was truncated.';
                    END IF;
                END $guard$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_applicationUsers_applicationUsers_createdBy",
                schema: "ctracking",
                table: "applicationUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_applicationUsers_applicationUsers_updatedBy",
                schema: "ctracking",
                table: "applicationUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_applicationUsers_partyReferences_uniqueId_userType_uniqueCo~",
                schema: "ctracking",
                table: "applicationUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Consignees_applicationUsers_createdBy",
                schema: "ctracking",
                table: "Consignees");

            migrationBuilder.DropForeignKey(
                name: "FK_Consignees_applicationUsers_updatedBy",
                schema: "ctracking",
                table: "Consignees");

            migrationBuilder.DropForeignKey(
                name: "FK_Consignees_partyReferences_consigneeId_userType_uniqueCode",
                schema: "ctracking",
                table: "Consignees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_applicationUsers_createdBy",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_applicationUsers_updatedBy",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_partyReferences_employeeId_userType_uniqueCode",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Shippers_Consignees_consigneeId",
                schema: "ctracking",
                table: "Shippers");

            migrationBuilder.DropForeignKey(
                name: "FK_Shippers_applicationUsers_createdBy",
                schema: "ctracking",
                table: "Shippers");

            migrationBuilder.DropForeignKey(
                name: "FK_Shippers_applicationUsers_updatedBy",
                schema: "ctracking",
                table: "Shippers");

            migrationBuilder.DropForeignKey(
                name: "FK_Shippers_partyReferences_shipperId_userType_uniqueCode",
                schema: "ctracking",
                table: "Shippers");

            migrationBuilder.DropForeignKey(
                name: "FK_userRoleMapping_applicationUsers_createdBy",
                schema: "ctracking",
                table: "userRoleMapping");

            migrationBuilder.DropForeignKey(
                name: "FK_userRoleMapping_applicationUsers_fkUserId_userType",
                schema: "ctracking",
                table: "userRoleMapping");

            migrationBuilder.DropForeignKey(
                name: "FK_userRoleMapping_applicationUsers_updatedBy",
                schema: "ctracking",
                table: "userRoleMapping");

            migrationBuilder.DropForeignKey(
                name: "FK_userRoleMapping_userRoles_fkRoleId_userType",
                schema: "ctracking",
                table: "userRoleMapping");

            migrationBuilder.DropForeignKey(
                name: "FK_userRoles_applicationUsers_createdBy",
                schema: "ctracking",
                table: "userRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_userRoles_applicationUsers_updatedBy",
                schema: "ctracking",
                table: "userRoles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_userRoles_roleId_userType",
                schema: "ctracking",
                table: "userRoles");

            migrationBuilder.DropIndex(
                name: "IX_userRoles_createdBy",
                schema: "ctracking",
                table: "userRoles");

            migrationBuilder.DropIndex(
                name: "IX_userRoles_updatedBy",
                schema: "ctracking",
                table: "userRoles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_role_type",
                schema: "ctracking",
                table: "userRoles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_partyReferences_uniqueId_userType_uniqueCode",
                schema: "ctracking",
                table: "partyReferences");

            migrationBuilder.DropCheckConstraint(
                name: "ck_party_reference_type",
                schema: "ctracking",
                table: "partyReferences");

            migrationBuilder.DropPrimaryKey(
                name: "PK_userRoleMapping",
                schema: "ctracking",
                table: "userRoleMapping");

            migrationBuilder.DropIndex(
                name: "IX_userRoleMapping_createdBy",
                schema: "ctracking",
                table: "userRoleMapping");

            migrationBuilder.DropIndex(
                name: "IX_userRoleMapping_fkRoleId_userType",
                schema: "ctracking",
                table: "userRoleMapping");

            migrationBuilder.DropIndex(
                name: "IX_userRoleMapping_fkUserId_fkRoleId",
                schema: "ctracking",
                table: "userRoleMapping");

            migrationBuilder.DropIndex(
                name: "IX_userRoleMapping_fkUserId_userType",
                schema: "ctracking",
                table: "userRoleMapping");

            migrationBuilder.DropIndex(
                name: "IX_userRoleMapping_updatedBy",
                schema: "ctracking",
                table: "userRoleMapping");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Shippers",
                schema: "ctracking",
                table: "Shippers");

            migrationBuilder.DropIndex(
                name: "IX_Shippers_createdBy",
                schema: "ctracking",
                table: "Shippers");

            migrationBuilder.DropIndex(
                name: "IX_Shippers_shipperId_userType_uniqueCode",
                schema: "ctracking",
                table: "Shippers");

            migrationBuilder.DropIndex(
                name: "IX_Shippers_updatedBy",
                schema: "ctracking",
                table: "Shippers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shippers_required_details",
                schema: "ctracking",
                table: "Shippers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_Shippers_type",
                schema: "ctracking",
                table: "Shippers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Employees",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_createdBy",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_employeeId_userType_uniqueCode",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_employeeNumber",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_updatedBy",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "ck_employees_required_details",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "ck_Employees_type",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Consignees",
                schema: "ctracking",
                table: "Consignees");

            migrationBuilder.DropIndex(
                name: "IX_Consignees_consigneeId_userType_uniqueCode",
                schema: "ctracking",
                table: "Consignees");

            migrationBuilder.DropIndex(
                name: "IX_Consignees_createdBy",
                schema: "ctracking",
                table: "Consignees");

            migrationBuilder.DropIndex(
                name: "IX_Consignees_updatedBy",
                schema: "ctracking",
                table: "Consignees");

            migrationBuilder.DropCheckConstraint(
                name: "ck_consignees_required_details",
                schema: "ctracking",
                table: "Consignees");

            migrationBuilder.DropCheckConstraint(
                name: "ck_Consignees_type",
                schema: "ctracking",
                table: "Consignees");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_applicationUsers_userId_userType",
                schema: "ctracking",
                table: "applicationUsers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_applicationUsers",
                schema: "ctracking",
                table: "applicationUsers");

            migrationBuilder.DropIndex(
                name: "IX_applicationUsers_createdBy",
                schema: "ctracking",
                table: "applicationUsers");

            migrationBuilder.DropIndex(
                name: "IX_applicationUsers_uniqueCode",
                schema: "ctracking",
                table: "applicationUsers");

            migrationBuilder.DropIndex(
                name: "IX_applicationUsers_uniqueId_userType_uniqueCode",
                schema: "ctracking",
                table: "applicationUsers");

            migrationBuilder.DropIndex(
                name: "IX_applicationUsers_updatedBy",
                schema: "ctracking",
                table: "applicationUsers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_type",
                schema: "ctracking",
                table: "applicationUsers");

            migrationBuilder.DropColumn(
                name: "userType",
                schema: "ctracking",
                table: "userRoles");

            

            

            migrationBuilder.DropColumn(
                name: "address",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "department",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "designation",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "employeeNumber",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "gender",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "joiningDate",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "mobileNumber",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "name",
                schema: "ctracking",
                table: "Employees");

            

            

            

            migrationBuilder.RenameTable(
                name: "userRoleMapping",
                schema: "ctracking",
                newName: "userRolesmapin",
                newSchema: "ctracking");

            migrationBuilder.RenameTable(
                name: "Shippers",
                schema: "ctracking",
                newName: "shipperDetails",
                newSchema: "ctracking");

            migrationBuilder.RenameTable(
                name: "Employees",
                schema: "ctracking",
                newName: "employeeDetails",
                newSchema: "ctracking");

            migrationBuilder.RenameTable(
                name: "Consignees",
                schema: "ctracking",
                newName: "consigneeDetails",
                newSchema: "ctracking");

            migrationBuilder.RenameTable(
                name: "applicationUsers",
                schema: "ctracking",
                newName: "applicationUser",
                newSchema: "ctracking");
            migrationBuilder.RenameColumn(name: "userId", schema: "ctracking", table: "applicationUser", newName: "appUserId");
            migrationBuilder.RenameColumn(name: "uniqueId", schema: "ctracking", table: "applicationUser", newName: "linkedUniqueId");
            migrationBuilder.RenameColumn(name: "uniqueCode", schema: "ctracking", table: "applicationUser", newName: "linkedUniqueCode");
            migrationBuilder.AlterColumn<int>(name: "appUserId", schema: "ctracking", table: "applicationUser", type: "integer", nullable: false, oldClrType: typeof(long), oldType: "bigint");
            migrationBuilder.AlterColumn<int>(name: "linkedUniqueId", schema: "ctracking", table: "applicationUser", type: "integer", nullable: false, oldClrType: typeof(long), oldType: "bigint");
            migrationBuilder.AlterColumn<string>(name: "linkedUniqueCode", schema: "ctracking", table: "applicationUser", type: "character varying(50)", maxLength: 50, nullable: false, oldClrType: typeof(string), oldType: "character varying(100)", oldMaxLength: 100);
            // Previous 1 meant internal, not administrator. Do not promote legacy accounts.
            migrationBuilder.Sql("UPDATE ctracking.\"applicationUser\" SET \"userType\" = 0 WHERE \"userType\" = 1;");

            migrationBuilder.RenameColumn(
                name: "userType",
                schema: "ctracking",
                table: "uniqueCodeCounters",
                newName: "partyType");

            migrationBuilder.RenameColumn(
                name: "userType",
                schema: "ctracking",
                table: "partyReferences",
                newName: "partyType");

            migrationBuilder.RenameColumn(name: "fkUserId", schema: "ctracking", table: "userRolesmapin", newName: "fkAppUserId");
            migrationBuilder.RenameColumn(name: "mappingId", schema: "ctracking", table: "userRolesmapin", newName: "mapinRoleId");
            migrationBuilder.DropColumn(name: "userType", schema: "ctracking", table: "userRolesmapin");
            migrationBuilder.AlterColumn<int>(name: "fkAppUserId", schema: "ctracking", table: "userRolesmapin", type: "integer", nullable: false, oldClrType: typeof(long), oldType: "bigint");
            migrationBuilder.AlterColumn<int>(name: "mapinRoleId", schema: "ctracking", table: "userRolesmapin", type: "integer", nullable: false, oldClrType: typeof(long), oldType: "bigint");

            migrationBuilder.RenameIndex(
                name: "IX_userRoleMapping_fkRoleId",
                schema: "ctracking",
                table: "userRolesmapin",
                newName: "IX_userRolesmapin_fkRoleId");

            migrationBuilder.RenameColumn(name: "userType", schema: "ctracking", table: "shipperDetails", newName: "partyType");
            migrationBuilder.AddColumn<int>(name: "status", schema: "ctracking", table: "shipperDetails", type: "integer", nullable: false, defaultValue: 1);

            migrationBuilder.RenameColumn(
                name: "contactPerson",
                schema: "ctracking",
                table: "shipperDetails",
                newName: "lastName");

            migrationBuilder.RenameIndex(
                name: "IX_Shippers_uniqueCode",
                schema: "ctracking",
                table: "shipperDetails",
                newName: "IX_shipperDetails_uniqueCode");

            migrationBuilder.RenameIndex(
                name: "IX_Shippers_consigneeId",
                schema: "ctracking",
                table: "shipperDetails",
                newName: "IX_shipperDetails_consigneeId");

            migrationBuilder.RenameColumn(name: "userType", schema: "ctracking", table: "employeeDetails", newName: "partyType");
            migrationBuilder.AddColumn<int>(name: "userStatus", schema: "ctracking", table: "employeeDetails", type: "integer", nullable: false, defaultValue: 1);

            migrationBuilder.RenameIndex(
                name: "IX_Employees_uniqueCode",
                schema: "ctracking",
                table: "employeeDetails",
                newName: "IX_employeeDetails_uniqueCode");

            migrationBuilder.RenameColumn(name: "userType", schema: "ctracking", table: "consigneeDetails", newName: "partyType");
            migrationBuilder.AddColumn<int>(name: "status", schema: "ctracking", table: "consigneeDetails", type: "integer", nullable: false, defaultValue: 1);

            migrationBuilder.RenameColumn(
                name: "contactName",
                schema: "ctracking",
                table: "consigneeDetails",
                newName: "lastName");

            migrationBuilder.RenameIndex(
                name: "IX_Consignees_uniqueCode",
                schema: "ctracking",
                table: "consigneeDetails",
                newName: "IX_consigneeDetails_uniqueCode");

            migrationBuilder.RenameIndex(
                name: "IX_applicationUsers_normalizedUserName",
                schema: "ctracking",
                table: "applicationUser",
                newName: "IX_applicationUser_normalizedUserName");

            migrationBuilder.AlterColumn<string>(
                name: "updatedBy",
                schema: "ctracking",
                table: "userRoles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "createdBy",
                schema: "ctracking",
                table: "userRoles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "uniqueCode",
                schema: "ctracking",
                table: "partyReferences",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<int>(
                name: "uniqueId",
                schema: "ctracking",
                table: "partyReferences",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<string>(
                name: "updatedBy",
                schema: "ctracking",
                table: "userRolesmapin",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "createdBy",
                schema: "ctracking",
                table: "userRolesmapin",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            

            migrationBuilder.AlterColumn<string>(
                name: "updatedBy",
                schema: "ctracking",
                table: "shipperDetails",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "uniqueCode",
                schema: "ctracking",
                table: "shipperDetails",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "createdBy",
                schema: "ctracking",
                table: "shipperDetails",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "consigneeId",
                schema: "ctracking",
                table: "shipperDetails",
                type: "integer",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "companyName",
                schema: "ctracking",
                table: "shipperDetails",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(600)",
                oldMaxLength: 600);

            migrationBuilder.AlterColumn<int>(
                name: "shipperId",
                schema: "ctracking",
                table: "shipperDetails",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<int>(
                name: "businessCategoryId",
                schema: "ctracking",
                table: "shipperDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "countryId",
                schema: "ctracking",
                table: "shipperDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo",
                schema: "ctracking",
                table: "shipperDetails",
                type: "text",
                nullable: true);

            

            migrationBuilder.AlterColumn<string>(
                name: "updatedBy",
                schema: "ctracking",
                table: "employeeDetails",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "uniqueCode",
                schema: "ctracking",
                table: "employeeDetails",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "createdBy",
                schema: "ctracking",
                table: "employeeDetails",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "employeeId",
                schema: "ctracking",
                table: "employeeDetails",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<int>(
                name: "designationId",
                schema: "ctracking",
                table: "employeeDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo",
                schema: "ctracking",
                table: "employeeDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "orgId",
                schema: "ctracking",
                table: "employeeDetails",
                type: "integer",
                nullable: true);

            

            migrationBuilder.AlterColumn<string>(
                name: "updatedBy",
                schema: "ctracking",
                table: "consigneeDetails",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "uniqueCode",
                schema: "ctracking",
                table: "consigneeDetails",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "createdBy",
                schema: "ctracking",
                table: "consigneeDetails",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "companyName",
                schema: "ctracking",
                table: "consigneeDetails",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(600)",
                oldMaxLength: 600);

            migrationBuilder.AlterColumn<int>(
                name: "consigneeId",
                schema: "ctracking",
                table: "consigneeDetails",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<int>(
                name: "countryId",
                schema: "ctracking",
                table: "consigneeDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo",
                schema: "ctracking",
                table: "consigneeDetails",
                type: "text",
                nullable: true);

            

            migrationBuilder.AlterColumn<string>(
                name: "updatedBy",
                schema: "ctracking",
                table: "applicationUser",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "createdBy",
                schema: "ctracking",
                table: "applicationUser",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            

            migrationBuilder.AddColumn<bool>(
                name: "isActive",
                schema: "ctracking",
                table: "applicationUser",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            

            

            migrationBuilder.AddUniqueConstraint(
                name: "AK_partyReferences_uniqueId_partyType_uniqueCode",
                schema: "ctracking",
                table: "partyReferences",
                columns: new[] { "uniqueId", "partyType", "uniqueCode" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_partyReferences_uniqueId_uniqueCode",
                schema: "ctracking",
                table: "partyReferences",
                columns: new[] { "uniqueId", "uniqueCode" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_userRolesmapin",
                schema: "ctracking",
                table: "userRolesmapin",
                column: "mapinRoleId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_shipperDetails",
                schema: "ctracking",
                table: "shipperDetails",
                column: "shipperId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_employeeDetails",
                schema: "ctracking",
                table: "employeeDetails",
                column: "employeeId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_consigneeDetails",
                schema: "ctracking",
                table: "consigneeDetails",
                column: "consigneeId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_applicationUser",
                schema: "ctracking",
                table: "applicationUser",
                column: "appUserId");

            migrationBuilder.CreateTable(
                name: "businessCategory",
                schema: "ctracking",
                columns: table => new
                {
                    businessCategoryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    createOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    updatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_businessCategory", x => x.businessCategoryId);
                });

            migrationBuilder.CreateTable(
                name: "conuntryMaster",
                schema: "ctracking",
                columns: table => new
                {
                    countryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    createOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    updatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conuntryMaster", x => x.countryId);
                });

            migrationBuilder.CreateTable(
                name: "designation",
                schema: "ctracking",
                columns: table => new
                {
                    designationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    createOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    updatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_designation", x => x.designationId);
                });

            migrationBuilder.CreateTable(
                name: "userLoginDetails",
                schema: "ctracking",
                columns: table => new
                {
                    loginId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fkAppUserId = table.Column<int>(type: "integer", nullable: false),
                    loginTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    logoutTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userLoginDetails", x => x.loginId);
                    table.CheckConstraint("ck_login_times", "\"logoutTime\" IS NULL OR \"logoutTime\" >= \"loginTime\"");
                    table.ForeignKey(
                        name: "FK_userLoginDetails_applicationUser_fkAppUserId",
                        column: x => x.fkAppUserId,
                        principalSchema: "ctracking",
                        principalTable: "applicationUser",
                        principalColumn: "appUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "userTokens",
                schema: "ctracking",
                columns: table => new
                {
                    tokenId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fkAppUserId = table.Column<int>(type: "integer", nullable: false),
                    purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expiresOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    usedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revokedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    createOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    updatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userTokens", x => x.tokenId);
                    table.ForeignKey(
                        name: "FK_userTokens_applicationUser_fkAppUserId",
                        column: x => x.fkAppUserId,
                        principalSchema: "ctracking",
                        principalTable: "applicationUser",
                        principalColumn: "appUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StateMaster",
                schema: "ctracking",
                columns: table => new
                {
                    stateId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    countryId = table.Column<int>(type: "integer", nullable: false),
                    createOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    updatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StateMaster", x => x.stateId);
                    table.ForeignKey(
                        name: "FK_StateMaster_conuntryMaster_countryId",
                        column: x => x.countryId,
                        principalSchema: "ctracking",
                        principalTable: "conuntryMaster",
                        principalColumn: "countryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "company",
                schema: "ctracking",
                columns: table => new
                {
                    companyId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    logo = table.Column<string>(type: "text", nullable: true),
                    emailId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    phoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    address = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    countryId = table.Column<int>(type: "integer", nullable: true),
                    stateId = table.Column<int>(type: "integer", nullable: true),
                    createOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    updatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company", x => x.companyId);
                    table.ForeignKey(
                        name: "FK_company_StateMaster_stateId",
                        column: x => x.stateId,
                        principalSchema: "ctracking",
                        principalTable: "StateMaster",
                        principalColumn: "stateId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_conuntryMaster_countryId",
                        column: x => x.countryId,
                        principalSchema: "ctracking",
                        principalTable: "conuntryMaster",
                        principalColumn: "countryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organizationHirarchy",
                schema: "ctracking",
                columns: table => new
                {
                    orgId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    companyId = table.Column<int>(type: "integer", nullable: false),
                    parentOrgId = table.Column<int>(type: "integer", nullable: true),
                    createOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    updatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    isActive = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizationHirarchy", x => x.orgId);
                    table.ForeignKey(
                        name: "FK_organizationHirarchy_company_companyId",
                        column: x => x.companyId,
                        principalSchema: "ctracking",
                        principalTable: "company",
                        principalColumn: "companyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_organizationHirarchy_organizationHirarchy_parentOrgId",
                        column: x => x.parentOrgId,
                        principalSchema: "ctracking",
                        principalTable: "organizationHirarchy",
                        principalColumn: "orgId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_party_reference_type",
                schema: "ctracking",
                table: "partyReferences",
                sql: "\"partyType\" IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_userRolesmapin_fkAppUserId_fkRoleId",
                schema: "ctracking",
                table: "userRolesmapin",
                columns: new[] { "fkAppUserId", "fkRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipperDetails_businessCategoryId",
                schema: "ctracking",
                table: "shipperDetails",
                column: "businessCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_shipperDetails_countryId",
                schema: "ctracking",
                table: "shipperDetails",
                column: "countryId");

            migrationBuilder.CreateIndex(
                name: "IX_shipperDetails_shipperId_partyType_uniqueCode",
                schema: "ctracking",
                table: "shipperDetails",
                columns: new[] { "shipperId", "partyType", "uniqueCode" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_shipper_status",
                schema: "ctracking",
                table: "shipperDetails",
                sql: "\"status\" IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shipperDetails_type",
                schema: "ctracking",
                table: "shipperDetails",
                sql: "\"partyType\" = 3");

            migrationBuilder.CreateIndex(
                name: "IX_employeeDetails_designationId",
                schema: "ctracking",
                table: "employeeDetails",
                column: "designationId");

            migrationBuilder.CreateIndex(
                name: "IX_employeeDetails_employeeId_partyType_uniqueCode",
                schema: "ctracking",
                table: "employeeDetails",
                columns: new[] { "employeeId", "partyType", "uniqueCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employeeDetails_orgId",
                schema: "ctracking",
                table: "employeeDetails",
                column: "orgId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_employee_status",
                schema: "ctracking",
                table: "employeeDetails",
                sql: "\"userStatus\" IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_employeeDetails_type",
                schema: "ctracking",
                table: "employeeDetails",
                sql: "\"partyType\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_consigneeDetails_consigneeId_partyType_uniqueCode",
                schema: "ctracking",
                table: "consigneeDetails",
                columns: new[] { "consigneeId", "partyType", "uniqueCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_consigneeDetails_countryId",
                schema: "ctracking",
                table: "consigneeDetails",
                column: "countryId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_consignee_status",
                schema: "ctracking",
                table: "consigneeDetails",
                sql: "\"status\" IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_consigneeDetails_type",
                schema: "ctracking",
                table: "consigneeDetails",
                sql: "\"partyType\" = 2");

            migrationBuilder.CreateIndex(
                name: "IX_applicationUser_linkedUniqueCode",
                schema: "ctracking",
                table: "applicationUser",
                column: "linkedUniqueCode");

            migrationBuilder.CreateIndex(
                name: "IX_applicationUser_linkedUniqueId_linkedUniqueCode",
                schema: "ctracking",
                table: "applicationUser",
                columns: new[] { "linkedUniqueId", "linkedUniqueCode" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_type",
                schema: "ctracking",
                table: "applicationUser",
                sql: "\"userType\" IN (0, 1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_businessCategory_code",
                schema: "ctracking",
                table: "businessCategory",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_code",
                schema: "ctracking",
                table: "company",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_countryId",
                schema: "ctracking",
                table: "company",
                column: "countryId");

            migrationBuilder.CreateIndex(
                name: "IX_company_stateId",
                schema: "ctracking",
                table: "company",
                column: "stateId");

            migrationBuilder.CreateIndex(
                name: "IX_conuntryMaster_code",
                schema: "ctracking",
                table: "conuntryMaster",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_designation_code",
                schema: "ctracking",
                table: "designation",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organizationHirarchy_code",
                schema: "ctracking",
                table: "organizationHirarchy",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organizationHirarchy_companyId",
                schema: "ctracking",
                table: "organizationHirarchy",
                column: "companyId");

            migrationBuilder.CreateIndex(
                name: "IX_organizationHirarchy_parentOrgId",
                schema: "ctracking",
                table: "organizationHirarchy",
                column: "parentOrgId");

            migrationBuilder.CreateIndex(
                name: "IX_StateMaster_code",
                schema: "ctracking",
                table: "StateMaster",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StateMaster_countryId",
                schema: "ctracking",
                table: "StateMaster",
                column: "countryId");

            migrationBuilder.CreateIndex(
                name: "IX_userLoginDetails_fkAppUserId_loginTime",
                schema: "ctracking",
                table: "userLoginDetails",
                columns: new[] { "fkAppUserId", "loginTime" });

            migrationBuilder.CreateIndex(
                name: "IX_userTokens_fkAppUserId",
                schema: "ctracking",
                table: "userTokens",
                column: "fkAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_userTokens_tokenHash",
                schema: "ctracking",
                table: "userTokens",
                column: "tokenHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_applicationUser_partyReferences_linkedUniqueId_linkedUnique~",
                schema: "ctracking",
                table: "applicationUser",
                columns: new[] { "linkedUniqueId", "linkedUniqueCode" },
                principalSchema: "ctracking",
                principalTable: "partyReferences",
                principalColumns: new[] { "uniqueId", "uniqueCode" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_consigneeDetails_conuntryMaster_countryId",
                schema: "ctracking",
                table: "consigneeDetails",
                column: "countryId",
                principalSchema: "ctracking",
                principalTable: "conuntryMaster",
                principalColumn: "countryId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_consigneeDetails_partyReferences_consigneeId_partyType_uniq~",
                schema: "ctracking",
                table: "consigneeDetails",
                columns: new[] { "consigneeId", "partyType", "uniqueCode" },
                principalSchema: "ctracking",
                principalTable: "partyReferences",
                principalColumns: new[] { "uniqueId", "partyType", "uniqueCode" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_employeeDetails_designation_designationId",
                schema: "ctracking",
                table: "employeeDetails",
                column: "designationId",
                principalSchema: "ctracking",
                principalTable: "designation",
                principalColumn: "designationId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_employeeDetails_organizationHirarchy_orgId",
                schema: "ctracking",
                table: "employeeDetails",
                column: "orgId",
                principalSchema: "ctracking",
                principalTable: "organizationHirarchy",
                principalColumn: "orgId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_employeeDetails_partyReferences_employeeId_partyType_unique~",
                schema: "ctracking",
                table: "employeeDetails",
                columns: new[] { "employeeId", "partyType", "uniqueCode" },
                principalSchema: "ctracking",
                principalTable: "partyReferences",
                principalColumns: new[] { "uniqueId", "partyType", "uniqueCode" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shipperDetails_businessCategory_businessCategoryId",
                schema: "ctracking",
                table: "shipperDetails",
                column: "businessCategoryId",
                principalSchema: "ctracking",
                principalTable: "businessCategory",
                principalColumn: "businessCategoryId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shipperDetails_consigneeDetails_consigneeId",
                schema: "ctracking",
                table: "shipperDetails",
                column: "consigneeId",
                principalSchema: "ctracking",
                principalTable: "consigneeDetails",
                principalColumn: "consigneeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shipperDetails_conuntryMaster_countryId",
                schema: "ctracking",
                table: "shipperDetails",
                column: "countryId",
                principalSchema: "ctracking",
                principalTable: "conuntryMaster",
                principalColumn: "countryId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shipperDetails_partyReferences_shipperId_partyType_uniqueCo~",
                schema: "ctracking",
                table: "shipperDetails",
                columns: new[] { "shipperId", "partyType", "uniqueCode" },
                principalSchema: "ctracking",
                principalTable: "partyReferences",
                principalColumns: new[] { "uniqueId", "partyType", "uniqueCode" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_userRolesmapin_applicationUser_fkAppUserId",
                schema: "ctracking",
                table: "userRolesmapin",
                column: "fkAppUserId",
                principalSchema: "ctracking",
                principalTable: "applicationUser",
                principalColumn: "appUserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_userRolesmapin_userRoles_fkRoleId",
                schema: "ctracking",
                table: "userRolesmapin",
                column: "fkRoleId",
                principalSchema: "ctracking",
                principalTable: "userRoles",
                principalColumn: "roleId",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("Restore a pre-upgrade backup to reverse the final structure migration.");
    }
}
