using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Highlights.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_Highlights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "highlights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    team = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    player = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    summary = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    thumbnail_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_highlights", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_highlights_match_id",
                table: "highlights",
                column: "match_id");

            migrationBuilder.CreateIndex(
                name: "IX_highlights_match_id_event_type",
                table: "highlights",
                columns: new[] { "match_id", "event_type" });

            migrationBuilder.CreateIndex(
                name: "IX_highlights_occurred_at",
                table: "highlights",
                column: "occurred_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "highlights");
        }
    }
}
