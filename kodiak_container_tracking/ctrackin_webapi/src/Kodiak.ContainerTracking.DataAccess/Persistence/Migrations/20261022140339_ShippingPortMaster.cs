using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kodiak.ContainerTracking.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShippingPortMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_StateMaster_stateId_countryId",
                schema: "ctracking",
                table: "StateMaster",
                columns: new[] { "stateId", "countryId" });

            migrationBuilder.CreateTable(
                name: "shippingPort",
                schema: "ctracking",
                columns: table => new
                {
                    portId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    countryId = table.Column<int>(type: "integer", nullable: false),
                    stateId = table.Column<int>(type: "integer", nullable: true),
                    city = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    timeZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_shippingPort", x => x.portId);
                    table.ForeignKey(
                        name: "FK_shippingPort_StateMaster_stateId_countryId",
                        columns: x => new { x.stateId, x.countryId },
                        principalSchema: "ctracking",
                        principalTable: "StateMaster",
                        principalColumns: new[] { "stateId", "countryId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shippingPort_conuntryMaster_countryId",
                        column: x => x.countryId,
                        principalSchema: "ctracking",
                        principalTable: "conuntryMaster",
                        principalColumn: "countryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shippingPort_code",
                schema: "ctracking",
                table: "shippingPort",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shippingPort_countryId",
                schema: "ctracking",
                table: "shippingPort",
                column: "countryId");

            migrationBuilder.CreateIndex(
                name: "IX_shippingPort_stateId_countryId",
                schema: "ctracking",
                table: "shippingPort",
                columns: new[] { "stateId", "countryId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shippingPort",
                schema: "ctracking");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_StateMaster_stateId_countryId",
                schema: "ctracking",
                table: "StateMaster");
        }
    }
}
