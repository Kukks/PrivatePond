﻿using Microsoft.EntityFrameworkCore.Migrations;

namespace PrivatePond.Migrations
{
    public partial class TransferRequesttId_Generate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromWalletId",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "ToUserId",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "ToWalletId",
                table: "TransferRequests");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FromWalletId",
                table: "TransferRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToUserId",
                table: "TransferRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToWalletId",
                table: "TransferRequests",
                type: "text",
                nullable: true);
        }
    }
}
