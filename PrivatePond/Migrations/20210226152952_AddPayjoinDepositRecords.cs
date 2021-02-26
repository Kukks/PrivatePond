using Microsoft.EntityFrameworkCore.Migrations;

namespace PrivatePond.Migrations
{
    public partial class AddPayjoinDepositRecords : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.CreateTable(
                name: "PayjoinLocks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayjoinLocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayjoinRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OriginalTransactionId = table.Column<string>(type: "text", nullable: true),
                    DepositRequestId = table.Column<string>(type: "text", nullable: true),
                    DepositContributedAmount = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayjoinRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayjoinRecords_DepositRequests_DepositRequestId",
                        column: x => x.DepositRequestId,
                        principalTable: "DepositRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayjoinRecords_DepositRequestId",
                table: "PayjoinRecords",
                column: "DepositRequestId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayjoinLocks");

            migrationBuilder.DropTable(
                name: "PayjoinRecords");
        }
    }
}
