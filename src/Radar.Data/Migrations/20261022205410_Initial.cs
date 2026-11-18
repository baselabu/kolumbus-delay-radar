using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stop_events",
                columns: table => new
                {
                    operating_date = table.Column<DateOnly>(type: "date", nullable: false),
                    journey_ref = table.Column<string>(type: "text", nullable: false),
                    stop_order = table.Column<int>(type: "integer", nullable: false),
                    line_ref = table.Column<string>(type: "text", nullable: false),
                    stop_point_ref = table.Column<string>(type: "text", nullable: false),
                    stop_point_name = table.Column<string>(type: "text", nullable: true),
                    aimed_arrival_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expected_arrival_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    actual_arrival_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    aimed_departure_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expected_departure_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    actual_departure_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stop_events", x => new { x.operating_date, x.journey_ref, x.stop_order });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stop_events");
        }
    }
}
