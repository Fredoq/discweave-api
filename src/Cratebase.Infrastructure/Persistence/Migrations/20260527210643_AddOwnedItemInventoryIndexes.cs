using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cratebase.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnedItemInventoryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_owned_items_collection_condition",
                table: "owned_items",
                columns: new[] { "collection_id", "condition" });

            migrationBuilder.CreateIndex(
                name: "ix_owned_items_collection_storage_location",
                table: "owned_items",
                columns: new[] { "collection_id", "storage_location" });

            migrationBuilder.CreateIndex(
                name: "ix_owned_items_inventory_release_medium",
                table: "owned_items",
                columns: new[] { "collection_id", "target_type", "target_release_id", "medium_type" });

            migrationBuilder.CreateIndex(
                name: "ix_owned_items_inventory_release_status",
                table: "owned_items",
                columns: new[] { "collection_id", "target_type", "target_release_id", "ownership_status" });

            migrationBuilder.CreateIndex(
                name: "ix_owned_items_inventory_track_medium",
                table: "owned_items",
                columns: new[] { "collection_id", "target_type", "target_track_id", "medium_type" });

            migrationBuilder.CreateIndex(
                name: "ix_owned_items_inventory_track_status",
                table: "owned_items",
                columns: new[] { "collection_id", "target_type", "target_track_id", "ownership_status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_owned_items_collection_condition",
                table: "owned_items");

            migrationBuilder.DropIndex(
                name: "ix_owned_items_collection_storage_location",
                table: "owned_items");

            migrationBuilder.DropIndex(
                name: "ix_owned_items_inventory_release_medium",
                table: "owned_items");

            migrationBuilder.DropIndex(
                name: "ix_owned_items_inventory_release_status",
                table: "owned_items");

            migrationBuilder.DropIndex(
                name: "ix_owned_items_inventory_track_medium",
                table: "owned_items");

            migrationBuilder.DropIndex(
                name: "ix_owned_items_inventory_track_status",
                table: "owned_items");
        }
    }
}
