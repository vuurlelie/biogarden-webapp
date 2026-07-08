using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biogarden.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSeparateDeliveryAddressSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryBuilding",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryCity",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryDoor",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryDoorbell",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryFloor",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryHouseNumber",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPostalCode",
                table: "Orders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStreet",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryBuilding",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryCity",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryDoor",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryDoorbell",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryFloor",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryHouseNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryPostalCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryStreet",
                table: "Orders");
        }
    }
}
