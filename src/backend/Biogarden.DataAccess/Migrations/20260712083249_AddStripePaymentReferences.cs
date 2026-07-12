using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biogarden.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddStripePaymentReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripeCheckoutSessionId",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripePaymentIntentId",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StripeCheckoutSessionId",
                table: "Orders",
                column: "StripeCheckoutSessionId",
                unique: true,
                filter: "[StripeCheckoutSessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StripePaymentIntentId",
                table: "Orders",
                column: "StripePaymentIntentId",
                unique: true,
                filter: "[StripePaymentIntentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_StripeCheckoutSessionId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_StripePaymentIntentId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "StripeCheckoutSessionId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "StripePaymentIntentId",
                table: "Orders");
        }
    }
}
