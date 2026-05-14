using ECommerceAI.Data.Entities;
using ECommerceAI.Data.Entities.ReadOnly;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAI.Data;

public class AiDbContext : DbContext
{
    public AiDbContext(DbContextOptions<AiDbContext> options) : base(options) { }

    // ── AI-owned tables (read/write) ──────────────────────────────────────
    public DbSet<AiChatSession> AiChatSessions { get; set; }
    public DbSet<AiChatSessionPreference> AiChatSessionPreferences { get; set; }
    public DbSet<AiChatMessage> AiChatMessages { get; set; }
    public DbSet<AiGeneratedCart> AiGeneratedCarts { get; set; }
    public DbSet<AiProductRecommendation> AiProductRecommendations { get; set; }
    public DbSet<AiRecommendationItem> AiRecommendationItems { get; set; }
    public DbSet<AiTagSuggestion> AiTagSuggestions { get; set; }
    public DbSet<AiMaterialSuggestion> AiMaterialSuggestions { get; set; }

    // ── Shared tables (read-only) ─────────────────────────────────────────
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductTag> ProductTags { get; set; }
    public DbSet<ProductVariant> ProductVariants { get; set; }
    public DbSet<ProductImage> ProductImages { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<Material> Materials { get; set; }
    public DbSet<Shop> Shops { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<AppUser> Users { get; set; }
    public DbSet<Dispute> Disputes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ai_schema");

        // ── AI Chat Session ───────────────────────────────────────────────
        modelBuilder.Entity<AiChatSession>(e =>
        {
            e.ToTable("ai_chat_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Status).HasColumnName("status").HasDefaultValueSql("'active'::text");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

            e.HasMany(x => x.Messages).WithOne(m => m.Session)
                .HasForeignKey(m => m.SessionId).HasConstraintName("ai_chat_messages_session_id_fkey");
            e.HasMany(x => x.GeneratedCarts).WithOne(c => c.Session)
                .HasForeignKey(c => c.SessionId).HasConstraintName("ai_generated_carts_session_id_fkey");
            e.HasMany(x => x.Recommendations).WithOne(r => r.Session)
                .HasForeignKey(r => r.SessionId).HasConstraintName("ai_product_recommendations_session_id_fkey");
            
            e.HasOne(x => x.Preference).WithOne(p => p.Session)
                .HasForeignKey<AiChatSessionPreference>(p => p.SessionId).HasConstraintName("ai_chat_session_preferences_session_id_fkey");
        });

        // ── AI Chat Session Preference ────────────────────────────────────
        modelBuilder.Entity<AiChatSessionPreference>(e =>
        {
            e.ToTable("ai_chat_session_preferences");
            e.HasKey(x => x.SessionId);
            e.Property(x => x.SessionId).HasColumnName("session_id");
            e.Property(x => x.IsMuted).HasColumnName("is_muted").HasDefaultValue(false);
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            e.Property(x => x.LastReadMessageId).HasColumnName("last_read_message_id");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        });

        // ── AI Chat Message ───────────────────────────────────────────────
        modelBuilder.Entity<AiChatMessage>(e =>
        {
            e.ToTable("ai_chat_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.SessionId).HasColumnName("session_id");
            e.Property(x => x.Role).HasColumnName("role");
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.SuggestedProductsJson).HasColumnName("suggested_products_json");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        });

        // ── AI Generated Cart ─────────────────────────────────────────────
        modelBuilder.Entity<AiGeneratedCart>(e =>
        {
            e.ToTable("ai_generated_carts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.SessionId).HasColumnName("session_id");
            e.Property(x => x.CartId).HasColumnName("cart_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasMany(x => x.Items).WithOne(i => i.AiCart)
                .HasForeignKey(i => i.AiCartId).HasConstraintName("ai_recommendation_items_ai_cart_id_fkey");
        });

        // ── AI Product Recommendation ─────────────────────────────────────
        modelBuilder.Entity<AiProductRecommendation>(e =>
        {
            e.ToTable("ai_product_recommendations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.SessionId).HasColumnName("session_id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.Score).HasColumnName("score").HasPrecision(5, 2);
            e.Property(x => x.Reason).HasColumnName("reason");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        });

        // ── AI Recommendation Item ────────────────────────────────────────
        modelBuilder.Entity<AiRecommendationItem>(e =>
        {
            e.ToTable("ai_recommendation_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.AiCartId).HasColumnName("ai_cart_id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.VariantId).HasColumnName("variant_id");
            e.Property(x => x.Quantity).HasColumnName("quantity").HasDefaultValue(1);
        });

        // ── AI Tag Suggestion ─────────────────────────────────────────────
        modelBuilder.Entity<AiTagSuggestion>(e =>
        {
            e.ToTable("ai_tag_suggestions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.SellerId).HasColumnName("seller_id");
            e.Property(x => x.InputTitle).HasColumnName("input_title");
            e.Property(x => x.InputDescription).HasColumnName("input_description");
            e.Property(x => x.SuggestedCategoryId).HasColumnName("suggested_category_id");
            e.Property(x => x.SuggestedTags).HasColumnName("suggested_tags").HasColumnType("jsonb");
            e.Property(x => x.ChosenCategoryId).HasColumnName("chosen_category_id");
            e.Property(x => x.ChosenTags).HasColumnName("chosen_tags").HasColumnType("jsonb");
            e.Property(x => x.Action).HasColumnName("action").HasDefaultValueSql("'accepted'::text");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        });

        // ── AI Material Suggestion ────────────────────────────────────────
        modelBuilder.Entity<AiMaterialSuggestion>(e =>
        {
            e.ToTable("ai_material_suggestions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.SellerId).HasColumnName("seller_id");
            e.Property(x => x.SuggestedMaterials).HasColumnName("suggested_materials").HasColumnType("jsonb");
            e.Property(x => x.ChosenMaterialIds).HasColumnName("chosen_material_ids");
            e.Property(x => x.Action).HasColumnName("action").HasDefaultValueSql("'accepted'::text");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        });

        // ── Read-only: Products ───────────────────────────────────────────
        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ShopId).HasColumnName("shop_id");
            e.Property(x => x.CategoryId).HasColumnName("category_id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.BasePrice).HasColumnName("base_price").HasPrecision(12, 2);
            e.Property(x => x.Currency).HasColumnName("currency");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasMany(x => x.Variants).WithOne()
                .HasForeignKey(v => v.ProductId);
            e.HasMany(x => x.Images).WithOne()
                .HasForeignKey(i => i.ProductId);
            e.HasMany(x => x.ProductTags).WithOne(pt => pt.Product)
                .HasForeignKey(pt => pt.ProductId);
            e.HasOne(x => x.Category).WithMany()
                .HasForeignKey(x => x.CategoryId);
        });

        modelBuilder.Entity<ProductTag>(e =>
        {
            e.ToTable("product_tags");
            e.HasKey(x => new { x.ProductId, x.TagId });
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.TagId).HasColumnName("tag_id");
            e.HasOne(x => x.Tag).WithMany()
                .HasForeignKey(x => x.TagId);
        });

        // ── Read-only: ProductVariant ─────────────────────────────────────
        modelBuilder.Entity<ProductVariant>(e =>
        {
            e.ToTable("product_variants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.Sku).HasColumnName("sku");
            e.Property(x => x.VariantName).HasColumnName("variant_name");
            e.Property(x => x.Price).HasColumnName("price").HasPrecision(12, 2);
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.Attributes).HasColumnName("attributes").HasColumnType("jsonb");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── Read-only: ProductImage ───────────────────────────────────────
        modelBuilder.Entity<ProductImage>(e =>
        {
            e.ToTable("product_images");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.ImageUrl).HasColumnName("image_url");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
        });

        // ── Read-only: Category ───────────────────────────────────────────
        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ParentId).HasColumnName("parent_id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Slug).HasColumnName("slug");
            e.Property(x => x.Level).HasColumnName("level");
            e.Property(x => x.IsActive).HasColumnName("is_active");
        });

        // ── Read-only: Tag ────────────────────────────────────────────────
        modelBuilder.Entity<Tag>(e =>
        {
            e.ToTable("tags");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Slug).HasColumnName("slug");
        });

        // ── Read-only: Material ───────────────────────────────────────────
        modelBuilder.Entity<Material>(e =>
        {
            e.ToTable("materials");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Slug).HasColumnName("slug");
            e.Property(x => x.IsActive).HasColumnName("is_active");
        });

        // ── Read-only: Shop ───────────────────────────────────────────────
        modelBuilder.Entity<Shop>(e =>
        {
            e.ToTable("shops");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OwnerId).HasColumnName("owner_id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.VerificationStatus).HasColumnName("verification_status");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── Read-only: Order ──────────────────────────────────────────────
        modelBuilder.Entity<Order>(e =>
        {
            e.ToTable("orders");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.ShopId).HasColumnName("shop_id");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
            e.Property(x => x.ShippingFee).HasColumnName("shipping_fee").HasPrecision(12, 2);
            e.Property(x => x.Total).HasColumnName("total").HasPrecision(12, 2);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasMany(x => x.OrderItems).WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId);
        });

        // ── Read-only: OrderItem ──────────────────────────────────────────
        modelBuilder.Entity<OrderItem>(e =>
        {
            e.ToTable("order_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.ProductName).HasColumnName("product_name");
            e.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(12, 2);
            e.Property(x => x.Quantity).HasColumnName("quantity");
            e.Property(x => x.LineTotal).HasColumnName("line_total").HasPrecision(12, 2);
        });

        // ── Read-only: AppUser ────────────────────────────────────────────
        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ── Read-only: Dispute ────────────────────────────────────────────
        modelBuilder.Entity<Dispute>(e =>
        {
            e.ToTable("disputes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.ShopId).HasColumnName("shop_id");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.RequestedAmount).HasColumnName("requested_amount").HasPrecision(12, 2);
            e.Property(x => x.ApprovedAmount).HasColumnName("approved_amount").HasPrecision(12, 2);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

    }
}
