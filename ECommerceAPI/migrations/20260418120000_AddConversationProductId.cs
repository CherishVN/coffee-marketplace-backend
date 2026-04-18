using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationProductId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "product_id",
                table: "conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_conversations_product",
                table: "conversations",
                column: "product_id");

            migrationBuilder.AddForeignKey(
                name: "conversations_product_id_fkey",
                table: "conversations",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "conversations_product_id_fkey",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "idx_conversations_product",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "product_id",
                table: "conversations");
        }
    }
}
