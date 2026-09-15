using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biogarden.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderEmailEventIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderEmails_OrderId_Kind",
                table: "OrderEmails");

            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                table: "OrderEmails",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_OrderEmails_OrderId_Kind_EventId",
                table: "OrderEmails",
                columns: new[] { "OrderId", "Kind", "EventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderEmails_OrderId_Kind_EventId",
                table: "OrderEmails");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "OrderEmails");

            migrationBuilder.CreateIndex(
                name: "IX_OrderEmails_OrderId_Kind",
                table: "OrderEmails",
                columns: new[] { "OrderId", "Kind" },
                unique: true);
        }
    }
}
