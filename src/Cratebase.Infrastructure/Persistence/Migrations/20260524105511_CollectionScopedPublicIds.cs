using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cratebase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CollectionScopedPublicIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rating_criterion_targets_rating_criteria_rating_criterion_id",
                table: "rating_criterion_targets");

            migrationBuilder.DropForeignKey(
                name: "FK_release_genres_releases_release_id",
                table: "release_genres");

            migrationBuilder.DropForeignKey(
                name: "FK_release_tags_releases_release_id",
                table: "release_tags");

            migrationBuilder.DropForeignKey(
                name: "FK_track_genres_tracks_track_id",
                table: "track_genres");

            migrationBuilder.DropForeignKey(
                name: "FK_track_tags_tracks_track_id",
                table: "track_tags");

            migrationBuilder.DropUniqueConstraint(
                name: "track_id",
                table: "tracks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_track_tags",
                table: "track_tags");

            migrationBuilder.DropUniqueConstraint(
                name: "track_relation_id",
                table: "track_relations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_track_genres",
                table: "track_genres");

            migrationBuilder.DropUniqueConstraint(
                name: "release_id",
                table: "releases");

            migrationBuilder.DropPrimaryKey(
                name: "PK_release_tags",
                table: "release_tags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_release_genres",
                table: "release_genres");

            migrationBuilder.DropUniqueConstraint(
                name: "rating_value_id",
                table: "rating_values");

            migrationBuilder.DropPrimaryKey(
                name: "PK_rating_criterion_targets",
                table: "rating_criterion_targets");

            migrationBuilder.DropUniqueConstraint(
                name: "rating_criterion_id",
                table: "rating_criteria");

            migrationBuilder.DropUniqueConstraint(
                name: "playlist_id",
                table: "playlists");

            migrationBuilder.DropUniqueConstraint(
                name: "owned_item_id",
                table: "owned_items");

            migrationBuilder.DropUniqueConstraint(
                name: "label_id",
                table: "labels");

            migrationBuilder.DropUniqueConstraint(
                name: "import_pattern_id",
                table: "import_patterns");

            migrationBuilder.DropUniqueConstraint(
                name: "credit_id",
                table: "credits");

            migrationBuilder.DropUniqueConstraint(
                name: "dictionary_entry_id",
                table: "collection_dictionary_entries");

            migrationBuilder.DropUniqueConstraint(
                name: "artist_id",
                table: "artists");

            migrationBuilder.DropUniqueConstraint(
                name: "artist_relation_id",
                table: "artist_relations");

            migrationBuilder.AddColumn<Guid>(
                name: "collection_id",
                table: "track_tags",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>(
                name: "collection_id",
                table: "track_genres",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>(
                name: "collection_id",
                table: "release_tags",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>(
                name: "collection_id",
                table: "release_genres",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>(
                name: "collection_id",
                table: "rating_criterion_targets",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.Sql(
                """
                UPDATE track_tags AS owned
                SET collection_id = tracks.collection_id
                FROM tracks
                WHERE owned.track_id = tracks.track_id;

                UPDATE track_genres AS owned
                SET collection_id = tracks.collection_id
                FROM tracks
                WHERE owned.track_id = tracks.track_id;

                UPDATE release_tags AS owned
                SET collection_id = releases.collection_id
                FROM releases
                WHERE owned.release_id = releases.release_id;

                UPDATE release_genres AS owned
                SET collection_id = releases.collection_id
                FROM releases
                WHERE owned.release_id = releases.release_id;

                UPDATE rating_criterion_targets AS owned
                SET collection_id = rating_criteria.collection_id
                FROM rating_criteria
                WHERE owned.rating_criterion_id = rating_criteria.rating_criterion_id;

                ALTER TABLE track_tags ALTER COLUMN collection_id DROP DEFAULT;
                ALTER TABLE track_genres ALTER COLUMN collection_id DROP DEFAULT;
                ALTER TABLE release_tags ALTER COLUMN collection_id DROP DEFAULT;
                ALTER TABLE release_genres ALTER COLUMN collection_id DROP DEFAULT;
                ALTER TABLE rating_criterion_targets ALTER COLUMN collection_id DROP DEFAULT;
                """);

            migrationBuilder.AddPrimaryKey(
                name: "PK_track_tags",
                table: "track_tags",
                columns: new[] { "collection_id", "track_id", "name" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_track_relations_collection_relation_id",
                table: "track_relations",
                columns: new[] { "collection_id", "track_relation_id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_track_genres",
                table: "track_genres",
                columns: new[] { "collection_id", "track_id", "name" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_release_tags",
                table: "release_tags",
                columns: new[] { "collection_id", "release_id", "name" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_release_genres",
                table: "release_genres",
                columns: new[] { "collection_id", "release_id", "name" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_rating_values_collection_rating_value_id",
                table: "rating_values",
                columns: new[] { "collection_id", "rating_value_id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_rating_criterion_targets",
                table: "rating_criterion_targets",
                columns: new[] { "collection_id", "rating_criterion_id", "target_type" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_owned_items_collection_owned_item_id",
                table: "owned_items",
                columns: new[] { "collection_id", "owned_item_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_import_patterns_collection_pattern_id",
                table: "import_patterns",
                columns: new[] { "collection_id", "import_pattern_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_credits_collection_credit_id",
                table: "credits",
                columns: new[] { "collection_id", "credit_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_collection_dictionary_entries_collection_entry_id",
                table: "collection_dictionary_entries",
                columns: new[] { "collection_id", "dictionary_entry_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_artist_relations_collection_relation_id",
                table: "artist_relations",
                columns: new[] { "collection_id", "artist_relation_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_rating_criterion_targets_rating_criteria_collection_id_rati~",
                table: "rating_criterion_targets",
                columns: new[] { "collection_id", "rating_criterion_id" },
                principalTable: "rating_criteria",
                principalColumns: new[] { "collection_id", "rating_criterion_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_release_genres_releases_collection_id_release_id",
                table: "release_genres",
                columns: new[] { "collection_id", "release_id" },
                principalTable: "releases",
                principalColumns: new[] { "collection_id", "release_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_release_tags_releases_collection_id_release_id",
                table: "release_tags",
                columns: new[] { "collection_id", "release_id" },
                principalTable: "releases",
                principalColumns: new[] { "collection_id", "release_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_track_genres_tracks_collection_id_track_id",
                table: "track_genres",
                columns: new[] { "collection_id", "track_id" },
                principalTable: "tracks",
                principalColumns: new[] { "collection_id", "track_id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_track_tags_tracks_collection_id_track_id",
                table: "track_tags",
                columns: new[] { "collection_id", "track_id" },
                principalTable: "tracks",
                principalColumns: new[] { "collection_id", "track_id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rating_criterion_targets_rating_criteria_collection_id_rati~",
                table: "rating_criterion_targets");

            migrationBuilder.DropForeignKey(
                name: "FK_release_genres_releases_collection_id_release_id",
                table: "release_genres");

            migrationBuilder.DropForeignKey(
                name: "FK_release_tags_releases_collection_id_release_id",
                table: "release_tags");

            migrationBuilder.DropForeignKey(
                name: "FK_track_genres_tracks_collection_id_track_id",
                table: "track_genres");

            migrationBuilder.DropForeignKey(
                name: "FK_track_tags_tracks_collection_id_track_id",
                table: "track_tags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_track_tags",
                table: "track_tags");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_track_relations_collection_relation_id",
                table: "track_relations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_track_genres",
                table: "track_genres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_release_tags",
                table: "release_tags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_release_genres",
                table: "release_genres");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_rating_values_collection_rating_value_id",
                table: "rating_values");

            migrationBuilder.DropPrimaryKey(
                name: "PK_rating_criterion_targets",
                table: "rating_criterion_targets");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_owned_items_collection_owned_item_id",
                table: "owned_items");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_import_patterns_collection_pattern_id",
                table: "import_patterns");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_credits_collection_credit_id",
                table: "credits");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_collection_dictionary_entries_collection_entry_id",
                table: "collection_dictionary_entries");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_artist_relations_collection_relation_id",
                table: "artist_relations");

            migrationBuilder.DropColumn(
                name: "collection_id",
                table: "track_tags");

            migrationBuilder.DropColumn(
                name: "collection_id",
                table: "track_genres");

            migrationBuilder.DropColumn(
                name: "collection_id",
                table: "release_tags");

            migrationBuilder.DropColumn(
                name: "collection_id",
                table: "release_genres");

            migrationBuilder.DropColumn(
                name: "collection_id",
                table: "rating_criterion_targets");

            migrationBuilder.AddUniqueConstraint(
                name: "track_id",
                table: "tracks",
                column: "track_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_track_tags",
                table: "track_tags",
                columns: new[] { "track_id", "name" });

            migrationBuilder.AddUniqueConstraint(
                name: "track_relation_id",
                table: "track_relations",
                column: "track_relation_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_track_genres",
                table: "track_genres",
                columns: new[] { "track_id", "name" });

            migrationBuilder.AddUniqueConstraint(
                name: "release_id",
                table: "releases",
                column: "release_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_release_tags",
                table: "release_tags",
                columns: new[] { "release_id", "name" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_release_genres",
                table: "release_genres",
                columns: new[] { "release_id", "name" });

            migrationBuilder.AddUniqueConstraint(
                name: "rating_value_id",
                table: "rating_values",
                column: "rating_value_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_rating_criterion_targets",
                table: "rating_criterion_targets",
                columns: new[] { "rating_criterion_id", "target_type" });

            migrationBuilder.AddUniqueConstraint(
                name: "rating_criterion_id",
                table: "rating_criteria",
                column: "rating_criterion_id");

            migrationBuilder.AddUniqueConstraint(
                name: "playlist_id",
                table: "playlists",
                column: "playlist_id");

            migrationBuilder.AddUniqueConstraint(
                name: "owned_item_id",
                table: "owned_items",
                column: "owned_item_id");

            migrationBuilder.AddUniqueConstraint(
                name: "label_id",
                table: "labels",
                column: "label_id");

            migrationBuilder.AddUniqueConstraint(
                name: "import_pattern_id",
                table: "import_patterns",
                column: "import_pattern_id");

            migrationBuilder.AddUniqueConstraint(
                name: "credit_id",
                table: "credits",
                column: "credit_id");

            migrationBuilder.AddUniqueConstraint(
                name: "dictionary_entry_id",
                table: "collection_dictionary_entries",
                column: "dictionary_entry_id");

            migrationBuilder.AddUniqueConstraint(
                name: "artist_id",
                table: "artists",
                column: "artist_id");

            migrationBuilder.AddUniqueConstraint(
                name: "artist_relation_id",
                table: "artist_relations",
                column: "artist_relation_id");

            migrationBuilder.AddForeignKey(
                name: "FK_rating_criterion_targets_rating_criteria_rating_criterion_id",
                table: "rating_criterion_targets",
                column: "rating_criterion_id",
                principalTable: "rating_criteria",
                principalColumn: "rating_criterion_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_release_genres_releases_release_id",
                table: "release_genres",
                column: "release_id",
                principalTable: "releases",
                principalColumn: "release_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_release_tags_releases_release_id",
                table: "release_tags",
                column: "release_id",
                principalTable: "releases",
                principalColumn: "release_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_track_genres_tracks_track_id",
                table: "track_genres",
                column: "track_id",
                principalTable: "tracks",
                principalColumn: "track_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_track_tags_tracks_track_id",
                table: "track_tags",
                column: "track_id",
                principalTable: "tracks",
                principalColumn: "track_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
