using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ECommerceAPI.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAiTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_chat_messages");

            migrationBuilder.DropTable(
                name: "ai_chat_session_preferences");

            migrationBuilder.DropTable(
                name: "ai_material_suggestions");

            migrationBuilder.DropTable(
                name: "ai_product_recommendations");

            migrationBuilder.DropTable(
                name: "ai_recommendation_items");

            migrationBuilder.DropTable(
                name: "ai_tag_suggestions");

            migrationBuilder.DropTable(
                name: "ai_generated_carts");

            migrationBuilder.DropTable(
                name: "ai_chat_sessions");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {


            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "order_status_histories",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.CreateTable(
                name: "ai_chat_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "now()"),
                    status = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'active'::text"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ai_chat_sessions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "ai_chat_sessions_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_material_suggestions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seller_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'accepted'::text"),
                    chosen_material_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    suggested_materials = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ai_material_suggestions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "ai_material_suggestions_product_id_fkey",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ai_material_suggestions_seller_id_fkey",
                        column: x => x.seller_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_tag_suggestions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    chosen_category_id = table.Column<long>(type: "bigint", nullable: true),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seller_id = table.Column<Guid>(type: "uuid", nullable: false),
                    suggested_category_id = table.Column<long>(type: "bigint", nullable: true),
                    action = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'accepted'::text"),
                    chosen_tags = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    input_description = table.Column<string>(type: "text", nullable: true),
                    input_title = table.Column<string>(type: "text", nullable: true),
                    suggested_tags = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ai_tag_suggestions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "ai_tag_suggestions_chosen_category_id_fkey",
                        column: x => x.chosen_category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "ai_tag_suggestions_product_id_fkey",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ai_tag_suggestions_seller_id_fkey",
                        column: x => x.seller_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "ai_tag_suggestions_suggested_category_id_fkey",
                        column: x => x.suggested_category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ai_chat_messages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "now()"),
                    role = table.Column<string>(type: "text", nullable: false),
                    suggested_products_json = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ai_chat_messages_pkey", x => x.id);
                    table.ForeignKey(
                        name: "ai_chat_messages_session_id_fkey",
                        column: x => x.session_id,
                        principalTable: "ai_chat_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_chat_session_preferences",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_muted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    last_read_message_id = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ai_chat_session_preferences_pkey", x => x.session_id);
                    table.ForeignKey(
                        name: "ai_chat_session_preferences_session_id_fkey",
                        column: x => x.session_id,
                        principalTable: "ai_chat_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_generated_carts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    cart_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ai_generated_carts_pkey", x => x.id);
                    table.ForeignKey(
                        name: "ai_generated_carts_cart_id_fkey",
                        column: x => x.cart_id,
                        principalTable: "carts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "ai_generated_carts_session_id_fkey",
                        column: x => x.session_id,
                        principalTable: "ai_chat_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_product_recommendations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "now()"),
                    reason = table.Column<string>(type: "text", nullable: true),
                    score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ai_product_recommendations_pkey", x => x.id);
                    table.ForeignKey(
                        name: "ai_product_recommendations_product_id_fkey",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ai_product_recommendations_session_id_fkey",
                        column: x => x.session_id,
                        principalTable: "ai_chat_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_recommendation_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ai_cart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: true, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ai_recommendation_items_pkey", x => x.id);
                    table.ForeignKey(
                        name: "ai_recommendation_items_ai_cart_id_fkey",
                        column: x => x.ai_cart_id,
                        principalTable: "ai_generated_carts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ai_recommendation_items_product_id_fkey",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ai_recommendation_items_variant_id_fkey",
                        column: x => x.variant_id,
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_ai_chat_messages_session",
                table: "ai_chat_messages",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "idx_ai_chat_session_preferences_deleted",
                table: "ai_chat_session_preferences",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "idx_ai_chat_session_preferences_muted",
                table: "ai_chat_session_preferences",
                column: "is_muted");

            migrationBuilder.CreateIndex(
                name: "idx_ai_chat_sessions_status",
                table: "ai_chat_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_ai_chat_sessions_user",
                table: "ai_chat_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_ai_generated_carts_cart",
                table: "ai_generated_carts",
                column: "cart_id");

            migrationBuilder.CreateIndex(
                name: "idx_ai_generated_carts_session",
                table: "ai_generated_carts",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_material_suggestions_product_id",
                table: "ai_material_suggestions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_material_suggestions_seller_id",
                table: "ai_material_suggestions",
                column: "seller_id");

            migrationBuilder.CreateIndex(
                name: "idx_ai_product_recommendations_product",
                table: "ai_product_recommendations",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_ai_product_recommendations_session",
                table: "ai_product_recommendations",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "idx_ai_recommendation_items_cart",
                table: "ai_recommendation_items",
                column: "ai_cart_id");

            migrationBuilder.CreateIndex(
                name: "idx_ai_recommendation_items_product",
                table: "ai_recommendation_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_recommendation_items_variant_id",
                table: "ai_recommendation_items",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "idx_ai_suggest_product",
                table: "ai_tag_suggestions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_ai_suggest_seller",
                table: "ai_tag_suggestions",
                column: "seller_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_tag_suggestions_chosen_category_id",
                table: "ai_tag_suggestions",
                column: "chosen_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_tag_suggestions_suggested_category_id",
                table: "ai_tag_suggestions",
                column: "suggested_category_id");
        }
    }
}
