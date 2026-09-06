using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceAI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleIdToAppUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "role_id",
                table: "users",
                type: "smallint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "role_id",
                table: "users");
        }
    }
}
