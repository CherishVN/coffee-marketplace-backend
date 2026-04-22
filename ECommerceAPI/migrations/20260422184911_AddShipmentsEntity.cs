using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceAPI.migrations
{
    /// <inheritdoc />
    public partial class AddShipmentsEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bảng public.shipments (và các thay đổi model liên quan) đã khớp CSDL thực tế
            // hoặc được quản lý tách; không phát sinh DDL tại đây để tránh tạo trùng / xóa bảng.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
