using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cratebase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "collections",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collections", x => x.id);
                    table.UniqueConstraint("collection_id", x => x.collection_id);
                });

            migrationBuilder.CreateTable(
                name: "search_documents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    subtitle = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    summary = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    search_text = table.Column<string>(type: "text", nullable: false),
                    search_vector = table.Column<string>(type: "tsvector", nullable: false, computedColumnSql: "to_tsvector('simple', coalesce(search_text, ''))", stored: true),
                    matched_fields = table.Column<string>(type: "text", nullable: false),
                    snippets = table.Column<string>(type: "text", nullable: false),
                    role_facet = table.Column<string>(type: "text", nullable: false),
                    media_facet = table.Column<string>(type: "text", nullable: false),
                    status_facet = table.Column<string>(type: "text", nullable: false),
                    tag_facet = table.Column<string>(type: "text", nullable: false),
                    label_id = table.Column<Guid>(type: "uuid", nullable: true),
                    collector_signal_facet = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_search_documents_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "collection_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "artists",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    artist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    artist_type = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artists", x => x.id);
                    table.UniqueConstraint("ak_artists_collection_artist_id", x => new { x.collection_id, x.artist_id });
                    table.UniqueConstraint("artist_id", x => x.artist_id);
                    table.ForeignKey(
                        name: "FK_artists_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "collection_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultCollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_collections_DefaultCollectionId",
                        column: x => x.DefaultCollectionId,
                        principalTable: "collections",
                        principalColumn: "collection_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "collection_dictionary_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    dictionary_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_builtin = table.Column<bool>(type: "boolean", nullable: false),
                    media_profile = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_dictionary_entries", x => x.id);
                    table.UniqueConstraint("dictionary_entry_id", x => x.dictionary_entry_id);
                    table.ForeignKey(
                        name: "FK_collection_dictionary_entries_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "collection_id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "labels",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_labels", x => x.id);
                    table.UniqueConstraint("ak_labels_collection_label_id", x => new { x.collection_id, x.label_id });
                    table.UniqueConstraint("label_id", x => x.label_id);
                    table.ForeignKey(
                        name: "FK_labels_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "collection_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rating_criteria",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    rating_criterion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_builtin = table.Column<bool>(type: "boolean", nullable: false),
                    is_protected = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rating_criteria", x => x.id);
                    table.UniqueConstraint("ak_rating_criteria_collection_criterion_id", x => new { x.collection_id, x.rating_criterion_id });
                    table.UniqueConstraint("rating_criterion_id", x => x.rating_criterion_id);
                    table.ForeignKey(
                        name: "FK_rating_criteria_collections_collection_id",
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
                    table.UniqueConstraint("ak_release_import_sessions_collection_session_id", x => new { x.collection_id, x.release_import_session_id });
                    table.UniqueConstraint("release_import_session_id", x => x.release_import_session_id);
                    table.ForeignKey(
                        name: "FK_release_import_sessions_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "collection_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "releases",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_various_artists = table.Column<bool>(type: "boolean", nullable: false),
                    is_not_on_label = table.Column<bool>(type: "boolean", nullable: false),
                    title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    cover_image_metadata = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    label_id = table.Column<Guid>(type: "uuid", nullable: true),
                    release_date = table.Column<DateOnly>(type: "date", nullable: true),
                    release_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    release_year = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_releases", x => x.id);
                    table.UniqueConstraint("ak_releases_collection_release_id", x => new { x.collection_id, x.release_id });
                    table.UniqueConstraint("release_id", x => x.release_id);
                    table.ForeignKey(
                        name: "FK_releases_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "collection_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tracks",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    duration_ticks = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracks", x => x.id);
                    table.UniqueConstraint("ak_tracks_collection_track_id", x => new { x.collection_id, x.track_id });
                    table.UniqueConstraint("track_id", x => x.track_id);
                    table.ForeignKey(
                        name: "FK_tracks_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "collection_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "artist_relations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    artist_relation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_artist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_artist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    period_end_year = table.Column<int>(type: "integer", nullable: true),
                    period_start_year = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artist_relations", x => x.id);
                    table.UniqueConstraint("artist_relation_id", x => x.artist_relation_id);
                    table.ForeignKey(
                        name: "FK_artist_relations_artists_collection_id_source_artist_id",
                        columns: x => new { x.collection_id, x.source_artist_id },
                        principalTable: "artists",
                        principalColumns: new[] { "collection_id", "artist_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_artist_relations_artists_collection_id_target_artist_id",
                        columns: x => new { x.collection_id, x.target_artist_id },
                        principalTable: "artists",
                        principalColumns: new[] { "collection_id", "artist_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_artist_relations_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "collection_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rating_criterion_targets",
                columns: table => new
                {
                    target_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rating_criterion_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rating_criterion_targets", x => new { x.rating_criterion_id, x.target_type });
                    table.ForeignKey(
                        name: "FK_rating_criterion_targets_rating_criteria_rating_criterion_id",
                        column: x => x.rating_criterion_id,
                        principalTable: "rating_criteria",
                        principalColumn: "rating_criterion_id",
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
                    artist_credits_json = table.Column<string>(type: "character varying(16384)", maxLength: 16384, nullable: false),
                    artist_names_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    genres_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    issues_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    labels_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    selected_artist_ids_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    tags_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_release_import_drafts", x => x.id);
                    table.UniqueConstraint("ak_release_import_drafts_collection_draft_id", x => new { x.collection_id, x.release_import_draft_id });
                    table.UniqueConstraint("release_import_draft_id", x => x.release_import_draft_id);
                    table.ForeignKey(
                        name: "FK_release_import_drafts_release_import_sessions_collection_id~",
                        columns: x => new { x.collection_id, x.release_import_session_id },
                        principalTable: "release_import_sessions",
                        principalColumns: new[] { "collection_id", "release_import_session_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "release_genres",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_release_genres", x => new { x.release_id, x.name });
                    table.ForeignKey(
                        name: "FK_release_genres_releases_release_id",
                        column: x => x.release_id,
                        principalTable: "releases",
                        principalColumn: "release_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "release_labels",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    label_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_number = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    has_no_catalog_number = table.Column<bool>(type: "boolean", nullable: false),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_release_labels", x => x.id);
                    table.CheckConstraint("ck_release_labels_catalog_number_consistency", "catalog_number IS NULL OR has_no_catalog_number = false");
                    table.ForeignKey(
                        name: "FK_release_labels_labels_collection_id_label_id",
                        columns: x => new { x.collection_id, x.label_id },
                        principalTable: "labels",
                        principalColumns: new[] { "collection_id", "label_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_release_labels_releases_collection_id_release_id",
                        columns: x => new { x.collection_id, x.release_id },
                        principalTable: "releases",
                        principalColumns: new[] { "collection_id", "release_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "release_tags",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_release_tags", x => new { x.release_id, x.name });
                    table.ForeignKey(
                        name: "FK_release_tags_releases_release_id",
                        column: x => x.release_id,
                        principalTable: "releases",
                        principalColumn: "release_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credits",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    contributor_artist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contributor_name = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    target_release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_track_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credits", x => x.id);
                    table.UniqueConstraint("credit_id", x => x.credit_id);
                    table.CheckConstraint("ck_credits_target_consistency", "(target_type = 'release' AND target_release_id IS NOT NULL AND target_track_id IS NULL) OR (target_type = 'track' AND target_track_id IS NOT NULL AND target_release_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_credits_artists_collection_id_contributor_artist_id",
                        columns: x => new { x.collection_id, x.contributor_artist_id },
                        principalTable: "artists",
                        principalColumns: new[] { "collection_id", "artist_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credits_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "collection_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_credits_releases_collection_id_target_release_id",
                        columns: x => new { x.collection_id, x.target_release_id },
                        principalTable: "releases",
                        principalColumns: new[] { "collection_id", "release_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credits_tracks_collection_id_target_track_id",
                        columns: x => new { x.collection_id, x.target_track_id },
                        principalTable: "tracks",
                        principalColumns: new[] { "collection_id", "track_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "owned_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owned_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cassette_tape_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    compact_disc_count = table.Column<int>(type: "integer", nullable: true),
                    condition = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    digital_file_format = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    digital_file_path = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    import_identity_content_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    import_identity_last_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    import_identity_path = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    import_identity_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    medium_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    other_medium_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ownership_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    storage_location = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    target_release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_track_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    vinyl_format_description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_owned_items", x => x.id);
                    table.UniqueConstraint("owned_item_id", x => x.owned_item_id);
                    table.CheckConstraint("ck_owned_items_target_consistency", "(target_type = 'release' AND target_release_id IS NOT NULL AND target_track_id IS NULL) OR (target_type = 'track' AND target_track_id IS NOT NULL AND target_release_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_owned_items_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "collection_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_owned_items_releases_collection_id_target_release_id",
                        columns: x => new { x.collection_id, x.target_release_id },
                        principalTable: "releases",
                        principalColumns: new[] { "collection_id", "release_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_owned_items_tracks_collection_id_target_track_id",
                        columns: x => new { x.collection_id, x.target_track_id },
                        principalTable: "tracks",
                        principalColumns: new[] { "collection_id", "track_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rating_values",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating_value_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criterion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    target_artist_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_label_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_track_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rating_values", x => x.id);
                    table.UniqueConstraint("rating_value_id", x => x.rating_value_id);
                    table.CheckConstraint("ck_rating_values_target_consistency", "(target_type = 'artist' AND target_artist_id IS NOT NULL AND target_release_id IS NULL AND target_track_id IS NULL AND target_label_id IS NULL) OR (target_type = 'release' AND target_release_id IS NOT NULL AND target_artist_id IS NULL AND target_track_id IS NULL AND target_label_id IS NULL) OR (target_type = 'track' AND target_track_id IS NOT NULL AND target_artist_id IS NULL AND target_release_id IS NULL AND target_label_id IS NULL) OR (target_type = 'label' AND target_label_id IS NOT NULL AND target_artist_id IS NULL AND target_release_id IS NULL AND target_track_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_rating_values_artists_collection_id_target_artist_id",
                        columns: x => new { x.collection_id, x.target_artist_id },
                        principalTable: "artists",
                        principalColumns: new[] { "collection_id", "artist_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rating_values_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "collection_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rating_values_labels_collection_id_target_label_id",
                        columns: x => new { x.collection_id, x.target_label_id },
                        principalTable: "labels",
                        principalColumns: new[] { "collection_id", "label_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rating_values_rating_criteria_collection_id_criterion_id",
                        columns: x => new { x.collection_id, x.criterion_id },
                        principalTable: "rating_criteria",
                        principalColumns: new[] { "collection_id", "rating_criterion_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_rating_values_releases_collection_id_target_release_id",
                        columns: x => new { x.collection_id, x.target_release_id },
                        principalTable: "releases",
                        principalColumns: new[] { "collection_id", "release_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rating_values_tracks_collection_id_target_track_id",
                        columns: x => new { x.collection_id, x.target_track_id },
                        principalTable: "tracks",
                        principalColumns: new[] { "collection_id", "track_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "release_tracks",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_number = table.Column<int>(type: "integer", nullable: false),
                    position_disc = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    position_side = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    title_override = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    version_note = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_release_tracks", x => x.id);
                    table.ForeignKey(
                        name: "FK_release_tracks_releases_collection_id_release_id",
                        columns: x => new { x.collection_id, x.release_id },
                        principalTable: "releases",
                        principalColumns: new[] { "collection_id", "release_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_release_tracks_tracks_collection_id_track_id",
                        columns: x => new { x.collection_id, x.track_id },
                        principalTable: "tracks",
                        principalColumns: new[] { "collection_id", "track_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "track_genres",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    track_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_track_genres", x => new { x.track_id, x.name });
                    table.ForeignKey(
                        name: "FK_track_genres_tracks_track_id",
                        column: x => x.track_id,
                        principalTable: "tracks",
                        principalColumn: "track_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "track_relations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    track_relation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relation_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_track_relations", x => x.id);
                    table.UniqueConstraint("track_relation_id", x => x.track_relation_id);
                    table.ForeignKey(
                        name: "FK_track_relations_collections_collection_id",
                        column: x => x.collection_id,
                        principalTable: "collections",
                        principalColumn: "collection_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_track_relations_tracks_collection_id_source_track_id",
                        columns: x => new { x.collection_id, x.source_track_id },
                        principalTable: "tracks",
                        principalColumns: new[] { "collection_id", "track_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_track_relations_tracks_collection_id_target_track_id",
                        columns: x => new { x.collection_id, x.target_track_id },
                        principalTable: "tracks",
                        principalColumns: new[] { "collection_id", "track_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "track_tags",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    track_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_track_tags", x => new { x.track_id, x.name });
                    table.ForeignKey(
                        name: "FK_track_tags_tracks_track_id",
                        column: x => x.track_id,
                        principalTable: "tracks",
                        principalColumn: "track_id",
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
                    artist_credits_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    artist_names_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    issues_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    selected_artist_ids_json = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_release_import_draft_tracks", x => x.id);
                    table.UniqueConstraint("release_import_draft_track_id", x => x.release_import_draft_track_id);
                    table.ForeignKey(
                        name: "FK_release_import_draft_tracks_release_import_drafts_collectio~",
                        columns: x => new { x.collection_id, x.release_import_draft_id },
                        principalTable: "release_import_drafts",
                        principalColumns: new[] { "collection_id", "release_import_draft_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_artist_relations_collection_id",
                table: "artist_relations",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "IX_artist_relations_collection_id_source_artist_id",
                table: "artist_relations",
                columns: new[] { "collection_id", "source_artist_id" });

            migrationBuilder.CreateIndex(
                name: "IX_artist_relations_collection_id_target_artist_id",
                table: "artist_relations",
                columns: new[] { "collection_id", "target_artist_id" });

            migrationBuilder.CreateIndex(
                name: "IX_artist_relations_source_artist_id",
                table: "artist_relations",
                column: "source_artist_id");

            migrationBuilder.CreateIndex(
                name: "IX_artist_relations_target_artist_id",
                table: "artist_relations",
                column: "target_artist_id");

            migrationBuilder.CreateIndex(
                name: "IX_artists_collection_id",
                table: "artists",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DefaultCollectionId",
                table: "AspNetUsers",
                column: "DefaultCollectionId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_collection_dictionary_entries_collection_id",
                table: "collection_dictionary_entries",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_dictionary_entries_collection_kind_code",
                table: "collection_dictionary_entries",
                columns: new[] { "collection_id", "kind", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_collections_owner_user_id",
                table: "collections",
                column: "owner_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credits_collection_id",
                table: "credits",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "IX_credits_collection_id_contributor_artist_id",
                table: "credits",
                columns: new[] { "collection_id", "contributor_artist_id" });

            migrationBuilder.CreateIndex(
                name: "IX_credits_collection_id_target_release_id",
                table: "credits",
                columns: new[] { "collection_id", "target_release_id" });

            migrationBuilder.CreateIndex(
                name: "IX_credits_collection_id_target_track_id",
                table: "credits",
                columns: new[] { "collection_id", "target_track_id" });

            migrationBuilder.CreateIndex(
                name: "IX_credits_contributor_artist_id",
                table: "credits",
                column: "contributor_artist_id");

            migrationBuilder.CreateIndex(
                name: "IX_credits_role",
                table: "credits",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "IX_credits_target_release_id",
                table: "credits",
                column: "target_release_id");

            migrationBuilder.CreateIndex(
                name: "IX_credits_target_track_id",
                table: "credits",
                column: "target_track_id");

            migrationBuilder.CreateIndex(
                name: "IX_import_patterns_collection_id_kind_sort_order",
                table: "import_patterns",
                columns: new[] { "collection_id", "kind", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ux_import_patterns_collection_kind_template_builtin",
                table: "import_patterns",
                columns: new[] { "collection_id", "kind", "template", "is_builtin" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_labels_collection_id",
                table: "labels",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "IX_owned_items_collection_id",
                table: "owned_items",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "IX_owned_items_collection_id_target_release_id",
                table: "owned_items",
                columns: new[] { "collection_id", "target_release_id" });

            migrationBuilder.CreateIndex(
                name: "IX_owned_items_collection_id_target_track_id",
                table: "owned_items",
                columns: new[] { "collection_id", "target_track_id" });

            migrationBuilder.CreateIndex(
                name: "ix_owned_items_import_identity",
                table: "owned_items",
                columns: new[] { "collection_id", "import_identity_path", "import_identity_size_bytes", "import_identity_last_modified_at", "import_identity_content_hash" },
                unique: true,
                filter: "import_identity_path IS NOT NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_owned_items_medium_type",
                table: "owned_items",
                column: "medium_type");

            migrationBuilder.CreateIndex(
                name: "IX_owned_items_ownership_status",
                table: "owned_items",
                column: "ownership_status");

            migrationBuilder.CreateIndex(
                name: "IX_owned_items_target_release_id",
                table: "owned_items",
                column: "target_release_id");

            migrationBuilder.CreateIndex(
                name: "IX_owned_items_target_track_id",
                table: "owned_items",
                column: "target_track_id");

            migrationBuilder.CreateIndex(
                name: "IX_rating_criteria_collection_id",
                table: "rating_criteria",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "IX_rating_criteria_collection_id_code",
                table: "rating_criteria",
                columns: new[] { "collection_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rating_values_collection_id",
                table: "rating_values",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "IX_rating_values_collection_id_criterion_id_target_type_targe~1",
                table: "rating_values",
                columns: new[] { "collection_id", "criterion_id", "target_type", "target_label_id" },
                unique: true,
                filter: "target_type = 'label'");

            migrationBuilder.CreateIndex(
                name: "IX_rating_values_collection_id_criterion_id_target_type_targe~2",
                table: "rating_values",
                columns: new[] { "collection_id", "criterion_id", "target_type", "target_release_id" },
                unique: true,
                filter: "target_type = 'release'");

            migrationBuilder.CreateIndex(
                name: "IX_rating_values_collection_id_criterion_id_target_type_targe~3",
                table: "rating_values",
                columns: new[] { "collection_id", "criterion_id", "target_type", "target_track_id" },
                unique: true,
                filter: "target_type = 'track'");

            migrationBuilder.CreateIndex(
                name: "IX_rating_values_collection_id_criterion_id_target_type_target~",
                table: "rating_values",
                columns: new[] { "collection_id", "criterion_id", "target_type", "target_artist_id" },
                unique: true,
                filter: "target_type = 'artist'");

            migrationBuilder.CreateIndex(
                name: "IX_rating_values_collection_id_target_artist_id",
                table: "rating_values",
                columns: new[] { "collection_id", "target_artist_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rating_values_collection_id_target_label_id",
                table: "rating_values",
                columns: new[] { "collection_id", "target_label_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rating_values_collection_id_target_release_id",
                table: "rating_values",
                columns: new[] { "collection_id", "target_release_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rating_values_collection_id_target_track_id",
                table: "rating_values",
                columns: new[] { "collection_id", "target_track_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rating_values_criterion_id",
                table: "rating_values",
                column: "criterion_id");

            migrationBuilder.CreateIndex(
                name: "IX_rating_values_target_artist_id",
                table: "rating_values",
                column: "target_artist_id");

            migrationBuilder.CreateIndex(
                name: "IX_rating_values_target_label_id",
                table: "rating_values",
                column: "target_label_id");

            migrationBuilder.CreateIndex(
                name: "IX_rating_values_target_release_id",
                table: "rating_values",
                column: "target_release_id");

            migrationBuilder.CreateIndex(
                name: "IX_rating_values_target_track_id",
                table: "rating_values",
                column: "target_track_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_import_draft_tracks_collection_id",
                table: "release_import_draft_tracks",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_import_draft_tracks_collection_id_release_import_dr~",
                table: "release_import_draft_tracks",
                columns: new[] { "collection_id", "release_import_draft_id" });

            migrationBuilder.CreateIndex(
                name: "IX_release_import_draft_tracks_release_import_draft_id",
                table: "release_import_draft_tracks",
                column: "release_import_draft_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_import_drafts_collection_id",
                table: "release_import_drafts",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_import_drafts_collection_id_release_import_session_~",
                table: "release_import_drafts",
                columns: new[] { "collection_id", "release_import_session_id" });

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

            migrationBuilder.CreateIndex(
                name: "IX_release_labels_collection_id",
                table: "release_labels",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_labels_collection_id_label_id",
                table: "release_labels",
                columns: new[] { "collection_id", "label_id" });

            migrationBuilder.CreateIndex(
                name: "IX_release_labels_collection_id_release_id",
                table: "release_labels",
                columns: new[] { "collection_id", "release_id" });

            migrationBuilder.CreateIndex(
                name: "IX_release_labels_label_id",
                table: "release_labels",
                column: "label_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_labels_release_id",
                table: "release_labels",
                column: "release_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_tracks_collection_id",
                table: "release_tracks",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_tracks_collection_id_release_id",
                table: "release_tracks",
                columns: new[] { "collection_id", "release_id" });

            migrationBuilder.CreateIndex(
                name: "IX_release_tracks_collection_id_track_id",
                table: "release_tracks",
                columns: new[] { "collection_id", "track_id" });

            migrationBuilder.CreateIndex(
                name: "IX_release_tracks_release_id",
                table: "release_tracks",
                column: "release_id");

            migrationBuilder.CreateIndex(
                name: "IX_release_tracks_track_id",
                table: "release_tracks",
                column: "track_id");

            migrationBuilder.CreateIndex(
                name: "IX_releases_collection_id",
                table: "releases",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "IX_track_relations_collection_id",
                table: "track_relations",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "IX_track_relations_collection_id_source_track_id",
                table: "track_relations",
                columns: new[] { "collection_id", "source_track_id" });

            migrationBuilder.CreateIndex(
                name: "IX_track_relations_collection_id_target_track_id",
                table: "track_relations",
                columns: new[] { "collection_id", "target_track_id" });

            migrationBuilder.CreateIndex(
                name: "IX_track_relations_source_track_id",
                table: "track_relations",
                column: "source_track_id");

            migrationBuilder.CreateIndex(
                name: "IX_track_relations_target_track_id",
                table: "track_relations",
                column: "target_track_id");

            migrationBuilder.CreateIndex(
                name: "IX_tracks_collection_id",
                table: "tracks",
                column: "collection_id");

            migrationBuilder.CreateIndex(
                name: "ix_search_documents_collection_entity",
                table: "search_documents",
                columns: new[] { "collection_id", "entity_type", "entity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_search_documents_collection_entity_type",
                table: "search_documents",
                columns: new[] { "collection_id", "entity_type" });

            migrationBuilder.CreateIndex(
                name: "ix_search_documents_collection_label_id",
                table: "search_documents",
                columns: new[] { "collection_id", "label_id" });

            migrationBuilder.Sql("CREATE INDEX ix_search_documents_search_vector ON search_documents USING GIN (search_vector)");
            migrationBuilder.Sql("CREATE INDEX ix_search_documents_search_text_trgm ON search_documents USING GIN (search_text gin_trgm_ops)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_documents");

            migrationBuilder.DropTable(
                name: "artist_relations");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "collection_dictionary_entries");

            migrationBuilder.DropTable(
                name: "credits");

            migrationBuilder.DropTable(
                name: "import_patterns");

            migrationBuilder.DropTable(
                name: "owned_items");

            migrationBuilder.DropTable(
                name: "rating_criterion_targets");

            migrationBuilder.DropTable(
                name: "rating_values");

            migrationBuilder.DropTable(
                name: "release_genres");

            migrationBuilder.DropTable(
                name: "release_import_draft_tracks");

            migrationBuilder.DropTable(
                name: "release_labels");

            migrationBuilder.DropTable(
                name: "release_tags");

            migrationBuilder.DropTable(
                name: "release_tracks");

            migrationBuilder.DropTable(
                name: "track_genres");

            migrationBuilder.DropTable(
                name: "track_relations");

            migrationBuilder.DropTable(
                name: "track_tags");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "artists");

            migrationBuilder.DropTable(
                name: "rating_criteria");

            migrationBuilder.DropTable(
                name: "release_import_drafts");

            migrationBuilder.DropTable(
                name: "labels");

            migrationBuilder.DropTable(
                name: "releases");

            migrationBuilder.DropTable(
                name: "tracks");

            migrationBuilder.DropTable(
                name: "release_import_sessions");

            migrationBuilder.DropTable(
                name: "collections");
        }
    }
}
