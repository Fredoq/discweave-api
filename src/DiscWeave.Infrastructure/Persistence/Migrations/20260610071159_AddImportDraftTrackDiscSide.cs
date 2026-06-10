using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscWeave.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImportDraftTrackDiscSide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "disc",
                table: "release_import_draft_tracks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "side",
                table: "release_import_draft_tracks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "disc",
                table: "release_import_draft_tracks");

            migrationBuilder.DropColumn(
                name: "side",
                table: "release_import_draft_tracks");
        }
    }
}
