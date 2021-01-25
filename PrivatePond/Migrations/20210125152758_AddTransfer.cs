using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PrivatePond.Migrations
{
    public partial class AddTransfer : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WalletBlobJson",
                table: "Wallets");

            migrationBuilder.AddColumn<string>(
                name: "TransferRequestId",
                table: "WalletTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TransferRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ToUserId = table.Column<string>(type: "text", nullable: true),
                    ToWalletId = table.Column<string>(type: "text", nullable: true),
                    FromWalletId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Destination = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TransferType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_TransferRequestId",
                table: "WalletTransactions",
                column: "TransferRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransactions_TransferRequests_TransferRequestId",
                table: "WalletTransactions",
                column: "TransferRequestId",
                principalTable: "TransferRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactions_TransferRequests_TransferRequestId",
                table: "WalletTransactions");

            migrationBuilder.DropTable(
                name: "TransferRequests");

            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_TransferRequestId",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "TransferRequestId",
                table: "WalletTransactions");

            migrationBuilder.AddColumn<string>(
                name: "WalletBlobJson",
                table: "Wallets",
                type: "text",
                nullable: true);
        }
    }
}
