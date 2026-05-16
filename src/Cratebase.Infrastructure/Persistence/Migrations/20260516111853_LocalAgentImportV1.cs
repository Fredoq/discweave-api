using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cratebase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LocalAgentImportV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_patterns",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_pattern_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    template = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_builtin = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_patterns", x => x.id);
                    table.UniqueConstraint("import_pattern_id", x => x.import_pattern_id);
                    table.ForeignKey(
                        name: "FK_import_patterns_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "collection_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "local_agent_import_tokens",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_agent_import_token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_local_agent_import_tokens", x => x.id);
                    table.UniqueConstraint("local_agent_import_token_id", x => x.local_agent_import_token_id);
                    table.ForeignKey(
                        name: "FK_local_agent_import_tokens_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "collection_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "release_import_sessions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_import_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_root = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    draft_count = table.Column<int>(type: "integer", nullable: false),
                    track_count = table.Column<int>(type: "integer", nullable: false),
                    ignored_file_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_release_import_sessions", x => x.id);
                    table.UniqueConstraint("release_import_session_id", x => x.release_import_session_id);
                    table.ForeignKey(
                        name: "FK_release_import_sessions_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "collection_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "release_import_drafts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_import_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_import_draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_path = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    relative_path = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    release_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    catalog_number = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    label_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    release_date = table.Column<DateOnly>(type: "date", nullable: true),
                    release_year = table.Column<int>(type: "integer", nullable: true),
                    is_various_artists = table.Column<bool>(type: "boolean", nullable: false),
                    is_not_on_label = table.Column<bool>(type: "boolean", nullable: false),
                    cover_path = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    cover_file_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    cover_extension = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    cover_content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    cover_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    cover_content = table.Column<byte[]>(type: "bytea", nullable: true),
                    confirmed_release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    artist_names_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    genres_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    issues_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    selected_artist_ids_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    tags_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_release_import_drafts", x => x.id);
                    table.UniqueConstraint("release_import_draft_id", x => x.release_import_draft_id);
                    table.ForeignKey(
                        name: "FK_release_import_drafts_release_import_sessions_release_impor~",
                        column: x => x.release_import_session_id,
                        principalTable: "release_import_sessions",
                        principalColumn: "release_import_session_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "release_import_draft_tracks",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_import_draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_import_draft_track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_path = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    relative_path = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    audio_file_format = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    last_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    position_number = table.Column<int>(type: "integer", nullable: true),
                    title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    is_skipped = table.Column<bool>(type: "boolean", nullable: false),
                    selected_track_id = table.Column<Guid>(type: "uuid", nullable: true),
                    artist_names_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    issues_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    selected_artist_ids_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_release_import_draft_tracks", x => x.id);
                    table.UniqueConstraint("release_import_draft_track_id", x => x.release_import_draft_track_id);
                    table.ForeignKey(
                        name: "FK_release_import_draft_tracks_release_import_drafts_release_i~",
                        column: x => x.release_import_draft_id,
                        principalTable: "release_import_drafts",
                        principalColumn: "release_import_draft_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_import_patterns_collection_id_kind_sort_order",
                table: "import_patterns",
                columns: new[] { "collection_id", "kind", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_local_agent_import_tokens_collection_id_created_at",
                table: "local_agent_import_tokens",
                columns: new[] { "collection_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_local_agent_import_tokens_token_hash",
                table: "local_agent_import_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_release_import_draft_tracks_collection_id",
                table: "release_import_draft_tracks",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_import_draft_tracks_release_import_draft_id",
                table: "release_import_draft_tracks",
                column: "release_import_draft_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_import_drafts_collection_id",
                table: "release_import_drafts",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_import_drafts_release_import_session_id",
                table: "release_import_drafts",
                column: "release_import_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_import_sessions_collection_id",
                table: "release_import_sessions",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_import_sessions_collection_id_created_at",
                table: "release_import_sessions",
                columns: new[] { "collection_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_patterns");

            migrationBuilder.DropTable(
                name: "local_agent_import_tokens");

            migrationBuilder.DropTable(
                name: "release_import_draft_tracks");

            migrationBuilder.DropTable(
                name: "release_import_drafts");

            migrationBuilder.DropTable(
                name: "release_import_sessions");
        }
    }
}
