using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cratebase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReleaseImportCreditsAndLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "artist_credits_json",
                table: "release_import_drafts",
                type: "character varying(16384)",
                maxLength: 16384,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "labels_json",
                table: "release_import_drafts",
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
                table: "release_import_drafts");

            migrationBuilder.DropColumn(
                name: "labels_json",
                table: "release_import_drafts");
        }
    }
}
