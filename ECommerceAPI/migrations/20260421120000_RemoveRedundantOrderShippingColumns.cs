using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceAPI.migrations
{
    /// <inheritdoc />
    public partial class RemoveRedundantOrderShippingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chạy thủ công trên PostgreSQL/Supabase nếu cần:
            // 1) Bản ghi trên orders chưa có shipments → chèn 1 dòng.
            // 2) DROP 5 cột trùng trên orders.

            migrationBuilder.Sql(
                """
                INSERT INTO shipments (id, order_id, shop_id, shipping_provider, shipping_service_id, tracking_code, status, provider_shipping_fee, cod_amount, estimated_delivery_date, actual_delivery_date, created_at, updated_at)
                SELECT gen_random_uuid(), o.id, o.shop_id,
                    COALESCE(NULLIF(TRIM(o.shipping_provider), ''), 'GHN'),
                    o.shipping_service_id,
                    CASE
                        WHEN o.tracking_code IS NOT NULL AND BTRIM(o.tracking_code) <> '' THEN o.tracking_code
                        ELSE 'MIGRATED-' || REPLACE(o.id::text, '-', '')
                    END,
                    'migrated',
                    COALESCE(o.provider_shipping_fee, 0),
                    0,
                    o.estimated_delivery_date,
                    o.actual_delivery_date,
                    o.created_at, o.updated_at
                FROM orders o
                WHERE NOT EXISTS (SELECT 1 FROM shipments s WHERE s.order_id = o.id);
                """
            );

            migrationBuilder.Sql(
                """
                ALTER TABLE orders DROP COLUMN IF EXISTS shipping_provider;
                ALTER TABLE orders DROP COLUMN IF EXISTS shipping_service_id;
                ALTER TABLE orders DROP COLUMN IF EXISTS tracking_code;
                ALTER TABLE orders DROP COLUMN IF EXISTS estimated_delivery_date;
                ALTER TABLE orders DROP COLUMN IF EXISTS actual_delivery_date;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE orders ADD COLUMN IF NOT EXISTS shipping_provider text;
                ALTER TABLE orders ADD COLUMN IF NOT EXISTS shipping_service_id text;
                ALTER TABLE orders ADD COLUMN IF NOT EXISTS tracking_code text;
                ALTER TABLE orders ADD COLUMN IF NOT EXISTS estimated_delivery_date timestamp with time zone;
                ALTER TABLE orders ADD COLUMN IF NOT EXISTS actual_delivery_date timestamp with time zone;
                """
            );

            migrationBuilder.Sql(
                """
                UPDATE orders o
                SET
                    shipping_provider = s.shipping_provider,
                    shipping_service_id = s.shipping_service_id,
                    tracking_code = s.tracking_code,
                    estimated_delivery_date = s.estimated_delivery_date,
                    actual_delivery_date = s.actual_delivery_date
                FROM (
                    SELECT DISTINCT ON (order_id) *
                    FROM shipments
                    ORDER BY order_id, created_at
                ) s
                WHERE s.order_id = o.id;
                """
            );
        }
    }
}
