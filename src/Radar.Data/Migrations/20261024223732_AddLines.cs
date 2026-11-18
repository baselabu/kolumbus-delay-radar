using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Radar.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lines",
                columns: table => new
                {
                    line_ref = table.Column<string>(type: "text", nullable: false),
                    public_code = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true),
                    transport_mode = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lines", x => x.line_ref);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lines");
        }
    }
}
