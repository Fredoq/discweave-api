using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cratebase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReleaseImportTrackCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "artist_credits_json",
                table: "release_import_draft_tracks",
                type: "character varying(8192)",
                maxLength: 8192,
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "artist_credits_json",
                table: "release_import_draft_tracks");
        }
    }
}
