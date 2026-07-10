using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biogarden.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderStockCommitTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "StockCommitted",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Older paid, non-cancelled orders already reduced stock during payment confirmation.
            migrationBuilder.Sql(
                "UPDATE [Orders] SET [StockCommitted] = 1 " +
                "WHERE [Status] <> 4 AND [PaymentStatus] IN (2, 4, 5, 6);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StockCommitted",
                table: "Orders");
        }
    }
}
