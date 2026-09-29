using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biogarden.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CheckoutDraftExpiresAtUtc",
                table: "Carts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckoutDraftJson",
                table: "Carts",
                type: "nvarchar(max)",
                maxLength: 16000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckoutDraftSession",
                table: "Carts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CheckoutDraftVersion",
                table: "Carts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Carts_CheckoutDraftExpiresAtUtc",
                table: "Carts",
                column: "CheckoutDraftExpiresAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Carts_CheckoutDraftExpiresAtUtc",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "CheckoutDraftExpiresAtUtc",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "CheckoutDraftJson",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "CheckoutDraftSession",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "CheckoutDraftVersion",
                table: "Carts");
        }
    }
}
