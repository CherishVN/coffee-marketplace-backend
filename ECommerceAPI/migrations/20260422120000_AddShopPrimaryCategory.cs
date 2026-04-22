using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddShopPrimaryCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "primary_category_id",
                table: "shops",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_shops_primary_category",
                table: "shops",
                column: "primary_category_id");

            migrationBuilder.AddForeignKey(
                name: "shops_primary_category_id_fkey",
                table: "shops",
                column: "primary_category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "shops_primary_category_id_fkey",
                table: "shops");

            migrationBuilder.DropIndex(
                name: "idx_shops_primary_category",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "primary_category_id",
                table: "shops");
        }
    }
}
