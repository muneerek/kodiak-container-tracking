using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kodiak.ContainerTracking.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TimeZoneDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "timeZone",
                schema: "ctracking",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    abbreviation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    utcOffsetSeconds = table.Column<int>(type: "integer", nullable: false),
                    isDaylightSaving = table.Column<bool>(type: "boolean", nullable: false),
                    observedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_timeZone", x => x.name);
                });
            migrationBuilder.Sql("""
                INSERT INTO ctracking."timeZone" (name, abbreviation, "utcOffsetSeconds", "isDaylightSaving", "observedAt")
                SELECT name, abbrev, EXTRACT(EPOCH FROM utc_offset)::integer, is_dst, CURRENT_TIMESTAMP
                FROM pg_timezone_names
                ON CONFLICT (name) DO UPDATE SET
                    abbreviation = EXCLUDED.abbreviation,
                    "utcOffsetSeconds" = EXCLUDED."utcOffsetSeconds",
                    "isDaylightSaving" = EXCLUDED."isDaylightSaving",
                    "observedAt" = EXCLUDED."observedAt";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "timeZone",
                schema: "ctracking");
        }
    }
}
