using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kodiak.ContainerTracking.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShippingPortAutoCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "masterCodeCounters",
                schema: "ctracking",
                columns: table => new
                {
                    kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    lastNumber = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_masterCodeCounters", x => x.kind);
                });

            migrationBuilder.InsertData(
                schema: "ctracking",
                table: "masterCodeCounters",
                columns: new[] { "kind", "lastNumber" },
                values: new object[] { "ShippingPorts", 0L });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "masterCodeCounters",
                schema: "ctracking");
        }
    }
}
