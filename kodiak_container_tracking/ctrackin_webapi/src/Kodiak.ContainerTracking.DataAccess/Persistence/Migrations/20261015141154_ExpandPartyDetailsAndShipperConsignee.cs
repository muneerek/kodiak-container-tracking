using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kodiak.ContainerTracking.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandPartyDetailsAndShipperConsignee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "name",
                schema: "ctracking",
                table: "Shippers",
                newName: "companyName");

            migrationBuilder.RenameColumn(
                name: "contactName",
                schema: "ctracking",
                table: "Shippers",
                newName: "contactPerson");

            migrationBuilder.RenameColumn(
                name: "phoneNumber",
                schema: "ctracking",
                table: "Employees",
                newName: "mobileNumber");

            migrationBuilder.RenameColumn(
                name: "name",
                schema: "ctracking",
                table: "Consignees",
                newName: "companyName");

            migrationBuilder.AddColumn<string>(
                name: "address",
                schema: "ctracking",
                table: "Shippers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "consigneeId",
                schema: "ctracking",
                table: "Shippers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address",
                schema: "ctracking",
                table: "Employees",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "department",
                schema: "ctracking",
                table: "Employees",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "designation",
                schema: "ctracking",
                table: "Employees",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "employeeNumber",
                schema: "ctracking",
                table: "Employees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gender",
                schema: "ctracking",
                table: "Employees",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "joiningDate",
                schema: "ctracking",
                table: "Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address",
                schema: "ctracking",
                table: "Consignees",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shippers_consigneeId",
                schema: "ctracking",
                table: "Shippers",
                column: "consigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_employeeNumber",
                schema: "ctracking",
                table: "Employees",
                column: "employeeNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Shippers_Consignees_consigneeId",
                schema: "ctracking",
                table: "Shippers",
                column: "consigneeId",
                principalSchema: "ctracking",
                principalTable: "Consignees",
                principalColumn: "consigneeId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shippers_Consignees_consigneeId",
                schema: "ctracking",
                table: "Shippers");

            migrationBuilder.DropIndex(
                name: "IX_Shippers_consigneeId",
                schema: "ctracking",
                table: "Shippers");

            migrationBuilder.DropIndex(
                name: "IX_Employees_employeeNumber",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "address",
                schema: "ctracking",
                table: "Shippers");

            migrationBuilder.DropColumn(
                name: "consigneeId",
                schema: "ctracking",
                table: "Shippers");

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
                name: "address",
                schema: "ctracking",
                table: "Consignees");

            migrationBuilder.RenameColumn(
                name: "companyName",
                schema: "ctracking",
                table: "Shippers",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "contactPerson",
                schema: "ctracking",
                table: "Shippers",
                newName: "contactName");

            migrationBuilder.RenameColumn(
                name: "mobileNumber",
                schema: "ctracking",
                table: "Employees",
                newName: "phoneNumber");

            migrationBuilder.RenameColumn(
                name: "companyName",
                schema: "ctracking",
                table: "Consignees",
                newName: "name");
        }
    }
}
