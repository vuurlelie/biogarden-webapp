using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biogarden.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Products_ListFilters_Name",
                table: "Products",
                columns: new[] { "RecordStatus", "Category", "Available", "DeliveryAvailable", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_ListFilters_Price",
                table: "Products",
                columns: new[] { "RecordStatus", "Category", "Available", "DeliveryAvailable", "Price", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_ListFilters_PublishedAt",
                table: "Posts",
                columns: new[] { "RecordStatus", "Draft", "Category", "PublishedAt", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_ListFilters_Title",
                table: "Posts",
                columns: new[] { "RecordStatus", "Draft", "Category", "Title", "PublishedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_ListFilters_Name",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_ListFilters_Price",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Posts_ListFilters_PublishedAt",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_ListFilters_Title",
                table: "Posts");
        }
    }
}
