using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biogarden.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RenameStripePaymentReferencesToSumUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_StripePaymentIntentId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_StripeCheckoutSessionId",
                table: "Orders");

            migrationBuilder.RenameColumn(
                name: "StripePaymentIntentId",
                table: "Orders",
                newName: "SumUpTransactionId");

            migrationBuilder.RenameColumn(
                name: "StripeCheckoutSessionId",
                table: "Orders",
                newName: "SumUpCheckoutId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SumUpTransactionId",
                table: "Orders",
                column: "SumUpTransactionId",
                unique: true,
                filter: "[SumUpTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SumUpCheckoutId",
                table: "Orders",
                column: "SumUpCheckoutId",
                unique: true,
                filter: "[SumUpCheckoutId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_SumUpTransactionId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_SumUpCheckoutId",
                table: "Orders");

            migrationBuilder.RenameColumn(
                name: "SumUpTransactionId",
                table: "Orders",
                newName: "StripePaymentIntentId");

            migrationBuilder.RenameColumn(
                name: "SumUpCheckoutId",
                table: "Orders",
                newName: "StripeCheckoutSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StripePaymentIntentId",
                table: "Orders",
                column: "StripePaymentIntentId",
                unique: true,
                filter: "[StripePaymentIntentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StripeCheckoutSessionId",
                table: "Orders",
                column: "StripeCheckoutSessionId",
                unique: true,
                filter: "[StripeCheckoutSessionId] IS NOT NULL");
        }
    }
}
