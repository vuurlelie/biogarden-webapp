using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biogarden.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddGlsDeliveryPointSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GlsDeliveryPointAddress",
                table: "Orders",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GlsDeliveryPointCountryCode",
                table: "Orders",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GlsDeliveryPointId",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GlsDeliveryPointName",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GlsDeliveryPointAddress",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GlsDeliveryPointCountryCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GlsDeliveryPointId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GlsDeliveryPointName",
                table: "Orders");
        }
    }
}
