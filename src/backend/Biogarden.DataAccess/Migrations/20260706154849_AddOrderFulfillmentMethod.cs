using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biogarden.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderFulfillmentMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "FulfillmentMethod",
                table: "Orders",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FulfillmentMethod",
                table: "Orders");
        }
    }
}
