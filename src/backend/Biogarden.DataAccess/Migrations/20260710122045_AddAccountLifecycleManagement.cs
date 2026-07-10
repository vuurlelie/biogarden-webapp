using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biogarden.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountLifecycleManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AnonymizedAtUtc",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAtUtc",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountLifecycleRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    HandledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandledByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdminNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountLifecycleRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountLifecycleRequests_AspNetUsers_HandledByAdminId",
                        column: x => x.HandledByAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountLifecycleRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountLifecycleRequests_HandledByAdminId",
                table: "AccountLifecycleRequests",
                column: "HandledByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLifecycleRequests_RequestedAtUtc",
                table: "AccountLifecycleRequests",
                column: "RequestedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLifecycleRequests_UserId_Status",
                table: "AccountLifecycleRequests",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountLifecycleRequests");

            migrationBuilder.DropColumn(
                name: "AnonymizedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DeactivatedAtUtc",
                table: "AspNetUsers");
        }
    }
}
