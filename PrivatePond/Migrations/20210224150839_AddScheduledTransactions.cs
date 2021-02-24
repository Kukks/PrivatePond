using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PrivatePond.Migrations
{
    public partial class AddScheduledTransactions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferRequests_SigningRequestId",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "TransferRequestId",
                table: "WalletTransactions");

            migrationBuilder.CreateTable(
                name: "ScheduledTransactions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Transaction = table.Column<string>(type: "text", nullable: true),
                    BroadcastAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReplacesSigningRequestId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledTransactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_SigningRequestId",
                table: "TransferRequests",
                column: "SigningRequestId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledTransactions");

            migrationBuilder.DropIndex(
                name: "IX_TransferRequests_SigningRequestId",
                table: "TransferRequests");

            migrationBuilder.AddColumn<string>(
                name: "TransferRequestId",
                table: "WalletTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_SigningRequestId",
                table: "TransferRequests",
                column: "SigningRequestId");
        }
    }
}
