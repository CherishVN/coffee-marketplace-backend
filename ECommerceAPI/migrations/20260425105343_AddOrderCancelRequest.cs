using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceAPI.migrations
{
    /// <inheritdoc />
    public partial class AddOrderCancelRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "cancel_requested_at",
                table: "orders",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "order_status_histories",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.CreateIndex(
                name: "IX_order_status_histories_changed_by",
                table: "order_status_histories",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "IX_order_status_histories_order_id",
                table: "order_status_histories",
                column: "order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_order_status_histories_changed_by",
                table: "order_status_histories");

            migrationBuilder.DropIndex(
                name: "IX_order_status_histories_order_id",
                table: "order_status_histories");

            migrationBuilder.DropColumn(
                name: "cancel_requested_at",
                table: "orders");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "order_status_histories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "now()");
        }
    }
}
