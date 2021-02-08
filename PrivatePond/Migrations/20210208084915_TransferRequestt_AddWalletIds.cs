using Microsoft.EntityFrameworkCore.Migrations;

namespace PrivatePond.Migrations
{
    public partial class TransferRequestt_AddWalletIds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FromWalletId",
                table: "TransferRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToWalletId",
                table: "TransferRequests",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromWalletId",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "ToWalletId",
                table: "TransferRequests");
        }
    }
}
