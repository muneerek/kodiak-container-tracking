using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kodiak.ContainerTracking.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceRequiredPartyDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE ctracking.\"Shippers\" ADD CONSTRAINT \"ck_shippers_required_details\" CHECK (\"companyName\" IS NOT NULL AND length(trim(\"companyName\")) > 0 AND \"contactPerson\" IS NOT NULL AND length(trim(\"contactPerson\")) > 0 AND \"address\" IS NOT NULL AND length(trim(\"address\")) > 0 AND \"emailId\" IS NOT NULL AND length(trim(\"emailId\")) > 0 AND \"consigneeId\" IS NOT NULL) NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE ctracking.\"Employees\" ADD CONSTRAINT \"ck_employees_required_details\" CHECK (\"firstName\" IS NOT NULL AND length(trim(\"firstName\")) > 0 AND \"lastName\" IS NOT NULL AND length(trim(\"lastName\")) > 0 AND \"gender\" IS NOT NULL AND length(trim(\"gender\")) > 0 AND \"emailId\" IS NOT NULL AND length(trim(\"emailId\")) > 0 AND \"mobileNumber\" IS NOT NULL AND length(trim(\"mobileNumber\")) > 0 AND \"address\" IS NOT NULL AND length(trim(\"address\")) > 0 AND \"dob\" IS NOT NULL) NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE ctracking.\"Consignees\" ADD CONSTRAINT \"ck_consignees_required_details\" CHECK (\"companyName\" IS NOT NULL AND length(trim(\"companyName\")) > 0 AND \"contactName\" IS NOT NULL AND length(trim(\"contactName\")) > 0 AND \"address\" IS NOT NULL AND length(trim(\"address\")) > 0 AND \"emailId\" IS NOT NULL AND length(trim(\"emailId\")) > 0) NOT VALID;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_shippers_required_details",
                schema: "ctracking",
                table: "Shippers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_employees_required_details",
                schema: "ctracking",
                table: "Employees");

            migrationBuilder.DropCheckConstraint(
                name: "ck_consignees_required_details",
                schema: "ctracking",
                table: "Consignees");
        }
    }
}
