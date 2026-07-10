using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biogarden.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderDeletionVisibilityAndItemUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Unit",
                table: "OrderItems",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.Sql("""
                UPDATE [Orders]
                SET [DeletedAtUtc] = [CancelledAtUtc]
                WHERE [Status] = 4 AND [CancelledAtUtc] IS NOT NULL AND [DeletedAtUtc] IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE [OrderItems]
                SET [Unit] = [Products].[Unit]
                FROM [OrderItems]
                INNER JOIN [Products] ON [Products].[Id] = [OrderItems].[ProductId];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "OrderItems");
        }
    }
}
