using Microsoft.EntityFrameworkCore.Migrations;

namespace PrivatePond.Migrations
{
    public partial class TransferRequesttId_Generate2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactions_TransferRequests_TransferRequestId",
                table: "WalletTransactions");

            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_TransferRequestId",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "SignedPSBT",
                table: "SigningRequests");

            migrationBuilder.DropColumn(
                name: "SignerId",
                table: "SigningRequests");

            migrationBuilder.RenameColumn(
                name: "SigningRequestGroup",
                table: "SigningRequests",
                newName: "FinalPSBT");

            migrationBuilder.AddColumn<string>(
                name: "SigningRequestId",
                table: "TransferRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredSignatures",
                table: "SigningRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SigningRequestItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SigningRequestId = table.Column<string>(type: "text", nullable: true),
                    SignedPSBT = table.Column<string>(type: "text", nullable: true),
                    SignerId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SigningRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SigningRequestItems_SigningRequests_SigningRequestId",
                        column: x => x.SigningRequestId,
                        principalTable: "SigningRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_SigningRequestId",
                table: "TransferRequests",
                column: "SigningRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SigningRequestItems_SigningRequestId",
                table: "SigningRequestItems",
                column: "SigningRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_TransferRequests_SigningRequests_SigningRequestId",
                table: "TransferRequests",
                column: "SigningRequestId",
                principalTable: "SigningRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransferRequests_SigningRequests_SigningRequestId",
                table: "TransferRequests");

            migrationBuilder.DropTable(
                name: "SigningRequestItems");

            migrationBuilder.DropIndex(
                name: "IX_TransferRequests_SigningRequestId",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "SigningRequestId",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "RequiredSignatures",
                table: "SigningRequests");

            migrationBuilder.RenameColumn(
                name: "FinalPSBT",
                table: "SigningRequests",
                newName: "SigningRequestGroup");

            migrationBuilder.AddColumn<string>(
                name: "SignedPSBT",
                table: "SigningRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignerId",
                table: "SigningRequests",
                type: "text",
                nullable: true);

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
    }
}
