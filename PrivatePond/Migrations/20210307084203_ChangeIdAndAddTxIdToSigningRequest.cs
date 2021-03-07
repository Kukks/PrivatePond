using Microsoft.EntityFrameworkCore.Migrations;

namespace PrivatePond.Migrations
{
    public partial class ChangeIdAndAddTxIdToSigningRequest : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferRequests_SigningRequestId",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "SignerId",
                table: "SigningRequestItems");

            migrationBuilder.AddColumn<string>(
                name: "TransactionId",
                table: "SigningRequests",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_SigningRequestId",
                table: "TransferRequests",
                column: "SigningRequestId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferRequests_SigningRequestId",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "SigningRequests");

            migrationBuilder.AddColumn<string>(
                name: "SignerId",
                table: "SigningRequestItems",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_SigningRequestId",
                table: "TransferRequests",
                column: "SigningRequestId",
                unique: true);
        }
    }
}
