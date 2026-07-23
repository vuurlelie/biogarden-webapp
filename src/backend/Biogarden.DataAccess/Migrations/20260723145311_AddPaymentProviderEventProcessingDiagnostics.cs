using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biogarden.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentProviderEventProcessingDiagnostics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastProcessingAttemptAtUtc",
                table: "PaymentProviderEvents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingError",
                table: "PaymentProviderEvents",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingFailedAtUtc",
                table: "PaymentProviderEvents",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastProcessingAttemptAtUtc",
                table: "PaymentProviderEvents");

            migrationBuilder.DropColumn(
                name: "ProcessingError",
                table: "PaymentProviderEvents");

            migrationBuilder.DropColumn(
                name: "ProcessingFailedAtUtc",
                table: "PaymentProviderEvents");
        }
    }
}
