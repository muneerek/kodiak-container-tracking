using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kodiak.ContainerTracking.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContainerRegistrationsAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "containerRegistrationNumbers",
                schema: "ctracking");

            migrationBuilder.CreateTable(
                name: "containerRegistration",
                schema: "ctracking",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    shipperId = table.Column<int>(type: "integer", nullable: false),
                    consigneeId = table.Column<int>(type: "integer", nullable: true),
                    containerNumber = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    containerType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cargo = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    weightKg = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    packages = table.Column<int>(type: "integer", nullable: false),
                    originPortId = table.Column<int>(type: "integer", nullable: false),
                    destinationPortId = table.Column<int>(type: "integer", nullable: false),
                    departure = table.Column<DateOnly>(type: "date", nullable: false),
                    expectedArrival = table.Column<DateOnly>(type: "date", nullable: false),
                    reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    createOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    updatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_containerRegistration", x => x.id);
                    table.ForeignKey(
                        name: "FK_containerRegistration_consigneeDetails_consigneeId",
                        column: x => x.consigneeId,
                        principalSchema: "ctracking",
                        principalTable: "consigneeDetails",
                        principalColumn: "consigneeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_containerRegistration_shipperDetails_shipperId",
                        column: x => x.shipperId,
                        principalSchema: "ctracking",
                        principalTable: "shipperDetails",
                        principalColumn: "shipperId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_containerRegistration_shippingPort_destinationPortId",
                        column: x => x.destinationPortId,
                        principalSchema: "ctracking",
                        principalTable: "shippingPort",
                        principalColumn: "portId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_containerRegistration_shippingPort_originPortId",
                        column: x => x.originPortId,
                        principalSchema: "ctracking",
                        principalTable: "shippingPort",
                        principalColumn: "portId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "containerAttachment",
                schema: "ctracking",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    containerRegistrationId = table.Column<int>(type: "integer", nullable: false),
                    fileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    relativePath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    isDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    createOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    createdBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    updatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updatedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_containerAttachment", x => x.id);
                    table.ForeignKey(
                        name: "FK_containerAttachment_containerRegistration_containerRegistra~",
                        column: x => x.containerRegistrationId,
                        principalSchema: "ctracking",
                        principalTable: "containerRegistration",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_containerAttachment_containerRegistrationId",
                schema: "ctracking",
                table: "containerAttachment",
                column: "containerRegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_containerAttachment_relativePath",
                schema: "ctracking",
                table: "containerAttachment",
                column: "relativePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_containerRegistration_code",
                schema: "ctracking",
                table: "containerRegistration",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_containerRegistration_consigneeId",
                schema: "ctracking",
                table: "containerRegistration",
                column: "consigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_containerRegistration_containerNumber",
                schema: "ctracking",
                table: "containerRegistration",
                column: "containerNumber",
                unique: true,
                filter: "\"containerNumber\" IS NOT NULL AND \"status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_containerRegistration_destinationPortId",
                schema: "ctracking",
                table: "containerRegistration",
                column: "destinationPortId");

            migrationBuilder.CreateIndex(
                name: "IX_containerRegistration_originPortId",
                schema: "ctracking",
                table: "containerRegistration",
                column: "originPortId");

            migrationBuilder.CreateIndex(
                name: "IX_containerRegistration_shipperId",
                schema: "ctracking",
                table: "containerRegistration",
                column: "shipperId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "containerAttachment",
                schema: "ctracking");

            migrationBuilder.DropTable(
                name: "containerRegistration",
                schema: "ctracking");

            migrationBuilder.DropSequence(
                name: "containerRegistrationNumbers",
                schema: "ctracking");
        }
    }
}
