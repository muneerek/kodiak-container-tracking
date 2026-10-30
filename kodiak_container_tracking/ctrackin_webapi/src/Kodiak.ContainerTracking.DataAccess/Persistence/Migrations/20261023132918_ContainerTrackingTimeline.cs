using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kodiak.ContainerTracking.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContainerTrackingTimeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "isOnHold",
                schema: "ctracking",
                table: "containerRegistration",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "trackingStatus",
                schema: "ctracking",
                table: "containerRegistration",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "trackingEventId",
                schema: "ctracking",
                table: "containerAttachment",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "containerTrackingEvent",
                schema: "ctracking",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    containerRegistrationId = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    eventAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    vesselName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    expectedArrival = table.Column<DateOnly>(type: "date", nullable: false),
                    isOnHold = table.Column<bool>(type: "boolean", nullable: false),
                    createOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    updatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_containerTrackingEvent", x => x.id);
                    table.UniqueConstraint("AK_containerTrackingEvent_id_containerRegistrationId", x => new { x.id, x.containerRegistrationId });
                    table.ForeignKey(
                        name: "FK_containerTrackingEvent_containerRegistration_containerRegis~",
                        column: x => x.containerRegistrationId,
                        principalSchema: "ctracking",
                        principalTable: "containerRegistration",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_containerAttachment_trackingEventId_containerRegistrationId",
                schema: "ctracking",
                table: "containerAttachment",
                columns: new[] { "trackingEventId", "containerRegistrationId" });

            migrationBuilder.CreateIndex(
                name: "IX_containerTrackingEvent_containerRegistrationId_eventAt",
                schema: "ctracking",
                table: "containerTrackingEvent",
                columns: new[] { "containerRegistrationId", "eventAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_containerAttachment_containerTrackingEvent_trackingEventId_~",
                schema: "ctracking",
                table: "containerAttachment",
                columns: new[] { "trackingEventId", "containerRegistrationId" },
                principalSchema: "ctracking",
                principalTable: "containerTrackingEvent",
                principalColumns: new[] { "id", "containerRegistrationId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_containerAttachment_containerTrackingEvent_trackingEventId_~",
                schema: "ctracking",
                table: "containerAttachment");

            migrationBuilder.DropTable(
                name: "containerTrackingEvent",
                schema: "ctracking");

            migrationBuilder.DropIndex(
                name: "IX_containerAttachment_trackingEventId_containerRegistrationId",
                schema: "ctracking",
                table: "containerAttachment");

            migrationBuilder.DropColumn(
                name: "isOnHold",
                schema: "ctracking",
                table: "containerRegistration");

            migrationBuilder.DropColumn(
                name: "trackingStatus",
                schema: "ctracking",
                table: "containerRegistration");

            migrationBuilder.DropColumn(
                name: "trackingEventId",
                schema: "ctracking",
                table: "containerAttachment");
        }
    }
}
