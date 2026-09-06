using System;
using System.Collections.Generic;
using System.Text.Json;
using ECommerceAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ECommerceAPI.Infrastructure.Data;

public partial class ApplicationDbContext : DbContext
{
    private static readonly JsonSerializerOptions ReviewImageUrlsJsonOptions = new();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Address> Addresses { get; set; }

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Conversation> Conversations { get; set; }

    public virtual DbSet<ConversationUserPreference> ConversationUserPreferences { get; set; }

    public virtual DbSet<Dispute> Disputes { get; set; }

    public virtual DbSet<DisputeMessage> DisputeMessages { get; set; }

    public virtual DbSet<DisputeOrderItem> DisputeOrderItems { get; set; }

    public virtual DbSet<FavoriteProduct> FavoriteProducts { get; set; }

    public virtual DbSet<Inventory> Inventories { get; set; }

    public virtual DbSet<Material> Materials { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }

    public virtual DbSet<Shipment> Shipments { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<ECommerceAPI.Domain.Entities.Payment> Payments { get; set; }

    public virtual DbSet<PlatformFeeRecord> PlatformFeeRecords { get; set; }

    public virtual DbSet<PlatformFeeConfig> PlatformFeeConfigs { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<ProductReview> ProductReviews { get; set; }

    public virtual DbSet<ProductVariant> ProductVariants { get; set; }

    public virtual DbSet<ProductTag> ProductTags { get; set; }

    public virtual DbSet<ProductMaterial> ProductMaterials { get; set; }

    public virtual DbSet<CustomerWallet> CustomerWallets { get; set; }

    public virtual DbSet<CustomerWalletLedger> CustomerWalletLedgers { get; set; }

    public virtual DbSet<CustomerWithdrawalRequest> CustomerWithdrawalRequests { get; set; }

    public virtual DbSet<SellerWallet> SellerWallets { get; set; }

    public virtual DbSet<SellerWalletLedger> SellerWalletLedgers { get; set; }

    public virtual DbSet<SellerWithdrawalRequest> SellerWithdrawalRequests { get; set; }

    public virtual DbSet<Shop> Shops { get; set; }

    public virtual DbSet<ShopDocument> ShopDocuments { get; set; }

    public virtual DbSet<ShopFollow> ShopFollows { get; set; }

    public virtual DbSet<LocalSpecialtyProfile> LocalSpecialtyProfiles { get; set; }

    public virtual DbSet<ProductLocalMeta> ProductLocalMetas { get; set; }


    public virtual DbSet<Tag> Tags { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Banner> Banners { get; set; }

    public virtual DbSet<Collection> Collections { get; set; }

    public virtual DbSet<CollectionProduct> CollectionProducts { get; set; }

    public virtual DbSet<UserAuditLog> UserAuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Banner>(entity =>
        {
            entity.ToTable("banners");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.IsActive, e.SortOrder }, "idx_banners_active_sorted")
                  .HasFilter("is_active = TRUE");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.Subtitle).HasColumnName("subtitle");
            entity.Property(e => e.CtaText).HasColumnName("cta_text");
            entity.Property(e => e.CtaUrl).HasColumnName("cta_url");
            entity.Property(e => e.ImageDesktop).HasColumnName("image_desktop");
            entity.Property(e => e.ImageMobile).HasColumnName("image_mobile");
            entity.Property(e => e.ImageAlt).HasColumnName("image_alt");
            
            entity.Property(e => e.TextPosition)
                .HasDefaultValue("left")
                .HasColumnName("text_position");
            
            entity.Property(e => e.TextColor)
                .HasDefaultValue("light")
                .HasColumnName("text_color");
            
            entity.Property(e => e.OverlayOpacity)
                .HasPrecision(3, 2)
                .HasDefaultValue(0.30m)
                .HasColumnName("overlay_opacity");

            entity.Property(e => e.LinkType)
                .HasDefaultValue("collection")
                .HasColumnName("link_type");
                
            entity.Property(e => e.LinkValue).HasColumnName("link_value");
            
            entity.Property(e => e.StartsAt).HasColumnName("starts_at");
            entity.Property(e => e.EndsAt).HasColumnName("ends_at");

            entity.Property(e => e.SortOrder)
                .HasDefaultValue(0)
                .HasColumnName("sort_order");
                
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
                
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
                
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<Collection>(entity =>
        {
            entity.ToTable("collections");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.Slug).IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Slug).HasColumnName("slug");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ShortDesc).HasColumnName("short_desc");
            entity.Property(e => e.Image).HasColumnName("image");
            entity.Property(e => e.ImageAlt).HasColumnName("image_alt");

            entity.Property(e => e.Type)
                .HasDefaultValue("manual")
                .HasColumnName("type");

            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.TagId).HasColumnName("tag_id");

            entity.Property(e => e.ShowOnHome)
                .HasDefaultValue(false)
                .HasColumnName("show_on_home");
                
            entity.Property(e => e.HomeSortOrder)
                .HasDefaultValue(0)
                .HasColumnName("home_sort_order");

            entity.Property(e => e.StartsAt).HasColumnName("starts_at");
            entity.Property(e => e.EndsAt).HasColumnName("ends_at");

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
                
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
                
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Category)
                .WithMany()
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("collections_category_id_fkey");

            entity.HasOne(d => d.Tag)
                .WithMany()
                .HasForeignKey(d => d.TagId)
                .HasConstraintName("collections_tag_id_fkey");
        });

        modelBuilder.Entity<CollectionProduct>(entity =>
        {
            entity.ToTable("collection_products");

            entity.HasKey(e => new { e.CollectionId, e.ProductId });
            
            entity.HasIndex(e => new { e.CollectionId, e.SortOrder }, "idx_collection_products_sort");

            entity.Property(e => e.CollectionId).HasColumnName("collection_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");

            entity.Property(e => e.SortOrder)
                .HasDefaultValue(0)
                .HasColumnName("sort_order");
                
            entity.Property(e => e.AddedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("added_at");

            entity.HasOne(d => d.Collection)
                .WithMany(p => p.CollectionProducts)
                .HasForeignKey(d => d.CollectionId)
                .HasConstraintName("collection_products_collection_id_fkey")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Product)
                .WithMany()
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("collection_products_product_id_fkey")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder
            .HasPostgresEnum("auth", "aal_level", new[] { "aal1", "aal2", "aal3" })
            .HasPostgresEnum("auth", "code_challenge_method", new[] { "s256", "plain" })
            .HasPostgresEnum("auth", "factor_status", new[] { "unverified", "verified" })
            .HasPostgresEnum("auth", "factor_type", new[] { "totp", "webauthn", "phone" })
            .HasPostgresEnum("auth", "oauth_authorization_status", new[] { "pending", "approved", "denied", "expired" })
            .HasPostgresEnum("auth", "oauth_client_type", new[] { "public", "confidential" })
            .HasPostgresEnum("auth", "oauth_registration_type", new[] { "dynamic", "manual" })
            .HasPostgresEnum("auth", "oauth_response_type", new[] { "code" })
            .HasPostgresEnum("auth", "one_time_token_type", new[] { "confirmation_token", "reauthentication_token", "recovery_token", "email_change_token_new", "email_change_token_current", "phone_change_token" })
            .HasPostgresEnum("realtime", "action", new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE", "ERROR" })
            .HasPostgresEnum("realtime", "equality_op", new[] { "eq", "neq", "lt", "lte", "gt", "gte", "in" })
            .HasPostgresEnum("storage", "buckettype", new[] { "STANDARD", "ANALYTICS", "VECTOR" })
            .HasPostgresExtension("extensions", "pg_stat_statements")
            .HasPostgresExtension("extensions", "pgcrypto")
            .HasPostgresExtension("extensions", "uuid-ossp")
            .HasPostgresExtension("graphql", "pg_graphql")
            .HasPostgresExtension("unaccent")
            .HasPostgresExtension("vault", "supabase_vault");

        modelBuilder.Entity<Address>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("addresses_pkey");

            entity.ToTable("addresses");

            entity.HasIndex(e => new { e.UserId, e.IsDefault }, "idx_addresses_default");

            entity.HasIndex(e => e.UserId, "idx_addresses_user");

            entity.HasIndex(e => e.UserId, "ux_addresses_one_default_per_user")
                .IsUnique()
                .HasFilter("(is_default = true)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.AddressLine1).HasColumnName("address_line1");
            entity.Property(e => e.AddressLine2).HasColumnName("address_line2");
            entity.Property(e => e.City).HasColumnName("city");
            entity.Property(e => e.Country)
                .HasDefaultValueSql("'VN'::text")
                .HasColumnName("country");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.District).HasColumnName("district");
            entity.Property(e => e.FullName).HasColumnName("full_name");
            entity.Property(e => e.IsDefault)
                .HasDefaultValue(false)
                .HasColumnName("is_default");
            entity.Property(e => e.Label).HasColumnName("label");
            entity.Property(e => e.Phone).HasColumnName("phone");
            entity.Property(e => e.PostalCode).HasColumnName("postal_code");
            entity.Property(e => e.Province).HasColumnName("province");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Ward).HasColumnName("ward");

            entity.HasOne(d => d.User).WithMany(p => p.Addresses)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("addresses_user_id_fkey");
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("carts_pkey");

            entity.ToTable("carts");

            entity.HasIndex(e => new { e.CustomerId, e.Status }, "carts_customer_id_status_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.Status)
                .HasDefaultValue((short)0)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Customer).WithMany(p => p.Carts)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("carts_customer_id_fkey");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("cart_items_pkey");

            entity.ToTable("cart_items");

            entity.HasIndex(e => new { e.CartId, e.VariantId }, "cart_items_cart_id_variant_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CartId).HasColumnName("cart_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.ProductName).HasColumnName("product_name");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.UnitPrice)
                .HasPrecision(12, 2)
                .HasColumnName("unit_price");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.VariantId).HasColumnName("variant_id");

            entity.HasOne(d => d.Cart).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.CartId)
                .HasConstraintName("cart_items_cart_id_fkey");

            entity.HasOne(d => d.Product).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("cart_items_product_id_fkey");

            entity.HasOne(d => d.Variant).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.VariantId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("cart_items_variant_id_fkey");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("categories_pkey");

            entity.ToTable("categories");

            entity.HasIndex(e => e.Slug, "categories_slug_key").IsUnique();

            entity.HasIndex(e => e.IsActive, "idx_categories_active");

            entity.HasIndex(e => e.ParentId, "idx_categories_parent");

            entity.HasIndex(e => new { e.Code, e.Level }, "idx_categories_code_level").IsUnique();

            entity.HasIndex(e => new { e.Name, e.Level }, "idx_categories_name_level").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Level)
                .HasDefaultValue((short)1)
                .HasColumnName("level");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.ParentId).HasColumnName("parent_id");
            entity.Property(e => e.Slug).HasColumnName("slug");
            entity.Property(e => e.Image).HasColumnName("image");
            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent)
                .HasForeignKey(d => d.ParentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("categories_parent_id_fkey");
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("conversations_pkey");

            entity.ToTable("conversations");

            entity.HasIndex(e => new { e.BuyerId, e.SellerId, e.ShopId }, "conversations_buyer_id_seller_id_shop_id_key").IsUnique();

            entity.HasIndex(e => e.BuyerId, "idx_conversations_buyer");

            entity.HasIndex(e => e.SellerId, "idx_conversations_seller");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.BuyerId).HasColumnName("buyer_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.SellerId).HasColumnName("seller_id");
            entity.Property(e => e.ShopId).HasColumnName("shop_id");

            entity.HasIndex(e => e.ProductId, "idx_conversations_product");

            entity.HasOne(d => d.Buyer).WithMany(p => p.ConversationBuyers)
                .HasForeignKey(d => d.BuyerId)
                .HasConstraintName("conversations_buyer_id_fkey");

            entity.HasOne(d => d.Order).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("conversations_order_id_fkey");

            entity.HasOne(d => d.Product).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("conversations_product_id_fkey");

            entity.HasOne(d => d.Seller).WithMany(p => p.ConversationSellers)
                .HasForeignKey(d => d.SellerId)
                .HasConstraintName("conversations_seller_id_fkey");

            entity.HasOne(d => d.Shop).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.ShopId)
                .HasConstraintName("conversations_shop_id_fkey");
        });

        modelBuilder.Entity<ConversationUserPreference>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("conversation_user_preferences_pkey");

            entity.ToTable("conversation_user_preferences");

            entity.HasIndex(e => new { e.ConversationId, e.UserId }, "conversation_user_preferences_conversation_id_user_id_key").IsUnique();

            entity.HasIndex(e => e.UserId, "idx_conversation_user_prefs_user");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.IsMuted)
                .HasDefaultValue(false)
                .HasColumnName("is_muted");
            entity.Property(e => e.HiddenAt).HasColumnName("hidden_at");

            entity.HasOne(d => d.Conversation).WithMany(p => p.UserPreferences)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("conversation_user_preferences_conversation_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.ConversationUserPreferences)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("conversation_user_preferences_user_id_fkey");
        });

        modelBuilder.Entity<Dispute>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("disputes_pkey");

            entity.ToTable("disputes");

            entity.HasIndex(e => e.OrderId, "idx_disputes_order");
            entity.HasIndex(e => e.CustomerId, "idx_disputes_customer");
            entity.HasIndex(e => e.ShopId, "idx_disputes_shop");
            entity.HasIndex(e => e.Status, "idx_disputes_status");
            entity.HasIndex(e => e.Type, "idx_disputes_type");
            entity.HasIndex(e => e.CreatedAt, "idx_disputes_created").IsDescending();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.ShopId).HasColumnName("shop_id");
            entity.Property(e => e.Type)
                .HasDefaultValue((short)0)
                .HasColumnName("type");
            entity.Property(e => e.Status)
                .HasDefaultValue((short)0)
                .HasColumnName("status");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.EvidenceUrls)
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("evidence_urls");
            entity.Property(e => e.RequestedAmount)
                .HasPrecision(12, 2)
                .HasDefaultValue(0m)
                .HasColumnName("requested_amount");
            entity.Property(e => e.ApprovedAmount)
                .HasPrecision(12, 2)
                .HasColumnName("approved_amount");
            entity.Property(e => e.SellerResponse).HasColumnName("seller_response");
            entity.Property(e => e.SellerEvidenceUrls)
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("seller_evidence_urls");
            entity.Property(e => e.SellerRespondedAt).HasColumnName("seller_responded_at");
            entity.Property(e => e.Resolution).HasColumnName("resolution");
            entity.Property(e => e.CustomerNote).HasColumnName("customer_note");
            entity.Property(e => e.AdminNote).HasColumnName("admin_note");
            entity.Property(e => e.ResolvedBy).HasColumnName("resolved_by");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Order).WithOne(p => p.Dispute)
                .HasForeignKey<Dispute>(d => d.OrderId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("disputes_order_id_fkey");

            entity.HasOne(d => d.Customer).WithMany(p => p.DisputesAsCustomer)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("disputes_customer_id_fkey");

            entity.HasOne(d => d.Shop).WithMany(p => p.Disputes)
                .HasForeignKey(d => d.ShopId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("disputes_shop_id_fkey");

            entity.HasOne(d => d.ResolvedByNavigation).WithMany(p => p.DisputesResolvedByNavigation)
                .HasForeignKey(d => d.ResolvedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("disputes_resolved_by_fkey");
        });

        modelBuilder.Entity<DisputeMessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("dispute_messages_pkey");

            entity.ToTable("dispute_messages");

            entity.HasIndex(e => e.DisputeId, "idx_dispute_messages_dispute");
            entity.HasIndex(e => e.SenderId, "idx_dispute_messages_sender");
            entity.HasIndex(e => new { e.DisputeId, e.IsRead }, "idx_dispute_messages_unread")
                .HasFilter("(is_read = false)");
            entity.HasIndex(e => e.CreatedAt, "idx_dispute_messages_created").IsDescending();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.DisputeId).HasColumnName("dispute_id");
            entity.Property(e => e.SenderId).HasColumnName("sender_id");
            entity.Property(e => e.SenderRole)
                .HasDefaultValue((short)0)
                .HasColumnName("sender_role");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.Attachments)
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("attachments");
            entity.Property(e => e.IsRead)
                .HasDefaultValue(false)
                .HasColumnName("is_read");
            entity.Property(e => e.ReadAt).HasColumnName("read_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Dispute).WithMany(p => p.DisputeMessages)
                .HasForeignKey(d => d.DisputeId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("dispute_messages_dispute_id_fkey");

            entity.HasOne(d => d.Sender).WithMany(p => p.DisputeMessages)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("dispute_messages_sender_id_fkey");
        });

        modelBuilder.Entity<DisputeOrderItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("dispute_order_items_pkey");

            entity.ToTable("dispute_order_items");

            entity.HasIndex(e => e.DisputeId, "idx_dispute_order_items_dispute");
            entity.HasIndex(e => e.OrderItemId, "idx_dispute_order_items_order_item");
            entity.HasIndex(e => new { e.DisputeId, e.OrderItemId }, "uq_dispute_order_items_dispute_line")
                .IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.DisputeId).HasColumnName("dispute_id");
            entity.Property(e => e.OrderItemId).HasColumnName("order_item_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.UnitPriceSnapshot)
                .HasPrecision(12, 2)
                .HasColumnName("unit_price_snapshot");
            entity.Property(e => e.LineSnapshotTotal)
                .HasPrecision(12, 2)
                .HasColumnName("line_snapshot_total");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Dispute).WithMany(p => p.DisputeOrderItems)
                .HasForeignKey(d => d.DisputeId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("dispute_order_items_dispute_id_fkey");

            entity.HasOne(d => d.OrderItem).WithMany(p => p.DisputeOrderItems)
                .HasForeignKey(d => d.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("dispute_order_items_order_item_id_fkey");
        });

        modelBuilder.Entity<FavoriteProduct>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.ProductId }).HasName("favorite_products_pkey");

            entity.ToTable("favorite_products");

            entity.HasIndex(e => e.ProductId, "idx_fav_products_product");

            entity.HasIndex(e => e.UserId, "idx_fav_products_user");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Product).WithMany(p => p.FavoriteProducts)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("favorite_products_product_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.FavoriteProducts)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("favorite_products_user_id_fkey");
        });

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("inventories_pkey");

            entity.ToTable("inventories");

            entity.HasIndex(e => new { e.ProductId, e.VariantId }, "inventories_product_id_variant_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity)
                .HasDefaultValue(0)
                .HasColumnName("quantity");
            entity.Property(e => e.ReservedQuantity)
                .HasDefaultValue(0)
                .HasColumnName("reserved_quantity");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.VariantId).HasColumnName("variant_id");

            entity.HasOne(d => d.Product).WithMany(p => p.Inventories)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("inventories_product_id_fkey");

            entity.HasOne(d => d.Variant).WithMany(p => p.Inventories)
                .HasForeignKey(d => d.VariantId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("inventories_variant_id_fkey");
        });

        modelBuilder.Entity<Material>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("materials_pkey");

            entity.ToTable("materials");

            entity.HasIndex(e => e.IsActive, "idx_materials_active");

            entity.HasIndex(e => e.Slug, "materials_slug_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Slug).HasColumnName("slug");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messages_pkey");

            entity.ToTable("messages");

            entity.HasIndex(e => e.ConversationId, "idx_messages_conversation");

            entity.HasIndex(e => new { e.ConversationId, e.IsRead }, "idx_messages_unread");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.IsRead)
                .HasDefaultValue(false)
                .HasColumnName("is_read");
            entity.Property(e => e.MessageType)
                .HasDefaultValueSql("'text'::text")
                .HasColumnName("message_type");
            entity.Property(e => e.SenderId).HasColumnName("sender_id");

            entity.HasOne(d => d.Conversation).WithMany(p => p.Messages)
                .HasForeignKey(d => d.ConversationId)
                .HasConstraintName("messages_conversation_id_fkey");

            entity.HasOne(d => d.Sender).WithMany(p => p.Messages)
                .HasForeignKey(d => d.SenderId)
                .HasConstraintName("messages_sender_id_fkey");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notifications_pkey");

            entity.ToTable("notifications");

            entity.HasIndex(e => new { e.UserId, e.IsRead }, "idx_notifications_unread");

            entity.HasIndex(e => e.UserId, "idx_notifications_user");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.IsRead)
                .HasDefaultValue(false)
                .HasColumnName("is_read");
            entity.Property(e => e.ReferenceId).HasColumnName("reference_id");
            entity.Property(e => e.ReferenceType).HasColumnName("reference_type");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("notifications_user_id_fkey");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("orders_pkey");

            entity.ToTable("orders");

            entity.HasIndex(e => e.CustomerId, "idx_orders_customer");

            entity.HasIndex(e => e.ShopId, "idx_orders_shop");

            entity.HasIndex(e => e.Status, "idx_orders_status");

            entity.HasIndex(e => e.TransactionId, "idx_orders_transaction");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.OrderCode).HasColumnName("order_code");
            entity.Property(e => e.CancelReason).HasColumnName("cancel_reason");
            entity.Property(e => e.CancelRequestedAt)
                .HasColumnType("timestamp with time zone")
                .HasColumnName("cancel_requested_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.ShipAddress).HasColumnName("ship_address");
            entity.Property(e => e.ShipFullName).HasColumnName("ship_full_name");
            entity.Property(e => e.ShipPhone).HasColumnName("ship_phone");
            entity.Property(e => e.ShippingAddressId).HasColumnName("shipping_address_id");
            entity.Property(e => e.ShippingFee)
                .HasPrecision(12, 2)
                .HasColumnName("shipping_fee");
            entity.Property(e => e.ShopId).HasColumnName("shop_id");
            entity.Property(e => e.Status)
                .HasDefaultValue((short)0)
                .HasColumnName("status");
            entity.Property(e => e.Subtotal)
                .HasPrecision(12, 2)
                .HasColumnName("subtotal");
            entity.Property(e => e.Total)
                .HasPrecision(12, 2)
                .HasColumnName("total");
            entity.Property(e => e.TransactionId).HasColumnName("transaction_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("orders_customer_id_fkey");

            entity.HasOne(d => d.ShippingAddress).WithMany(p => p.Orders)
                .HasForeignKey(d => d.ShippingAddressId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("orders_shipping_address_id_fkey");

            entity.HasOne(d => d.Shop).WithMany(p => p.Orders)
                .HasForeignKey(d => d.ShopId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("orders_shop_id_fkey");

            entity.HasOne(d => d.Transaction).WithMany(p => p.Orders)
                .HasForeignKey(d => d.TransactionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("orders_transaction_id_fkey");
        });

        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("order_status_histories_pkey");

            entity.ToTable("order_status_histories");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.PreviousStatus).HasColumnName("previous_status");
            entity.Property(e => e.NewStatus).HasColumnName("new_status");
            entity.Property(e => e.ChangedBy).HasColumnName("changed_by");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Order)
                .WithMany(p => p.OrderStatusHistories)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("order_status_histories_order_id_fkey");

            entity.HasOne(d => d.ChangedByUser)
                .WithMany()
                .HasForeignKey(d => d.ChangedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("order_status_histories_changed_by_fkey");
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shipments_pkey");

            entity.ToTable("shipments");

            entity.HasIndex(e => e.TrackingCode, "shipments_tracking_code_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ShopId).HasColumnName("shop_id");
            entity.Property(e => e.ShippingProvider)
                .HasDefaultValueSql("'GHN'::text")
                .HasColumnName("shipping_provider");
            entity.Property(e => e.ShippingServiceId).HasColumnName("shipping_service_id");
            entity.Property(e => e.TrackingCode).HasColumnName("tracking_code");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.ProviderShippingFee)
                .HasPrecision(12, 2)
                .HasDefaultValue(0m)
                .HasColumnName("provider_shipping_fee");
            entity.Property(e => e.CodAmount)
                .HasPrecision(12, 2)
                .HasDefaultValue(0m)
                .HasColumnName("cod_amount");
            entity.Property(e => e.EstimatedDeliveryDate).HasColumnName("estimated_delivery_date");
            entity.Property(e => e.ActualDeliveryDate).HasColumnName("actual_delivery_date");
            entity.Property(e => e.DeliveryProofUrls)
                .HasColumnType("text")
                .HasColumnName("delivery_proof_urls");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Order)
                .WithMany(p => p.Shipments)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("shipments_order_id_fkey");

            entity.HasOne(d => d.Shop)
                .WithMany(p => p.Shipments)
                .HasForeignKey(d => d.ShopId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("shipments_shop_id_fkey");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("order_items_pkey");

            entity.ToTable("order_items");

            entity.HasIndex(e => e.OrderId, "idx_order_items_order");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.LineTotal)
                .HasPrecision(12, 2)
                .HasColumnName("line_total");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.ProductName).HasColumnName("product_name");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.UnitPrice)
                .HasPrecision(12, 2)
                .HasColumnName("unit_price");
            entity.Property(e => e.VariantId).HasColumnName("variant_id");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("order_items_order_id_fkey");

            entity.HasOne(d => d.Product).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("order_items_product_id_fkey");

            entity.HasOne(d => d.Variant).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.VariantId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("order_items_variant_id_fkey");
        });

        modelBuilder.Entity<ECommerceAPI.Domain.Entities.Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("payments_pkey");

            entity.ToTable("payments");

            entity.HasIndex(e => e.OrderId, "idx_payments_order");

            entity.HasIndex(e => e.Status, "idx_payments_status");

            entity.HasIndex(e => e.TransactionId, "idx_payments_transaction");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasPrecision(12, 2)
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasDefaultValueSql("'VND'::text")
                .HasColumnName("currency");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.PaidAt).HasColumnName("paid_at");
            entity.Property(e => e.Provider).HasColumnName("provider");
            entity.Property(e => e.ProviderRef).HasColumnName("provider_ref");
            entity.Property(e => e.Status)
                .HasDefaultValue((short)0)
                .HasColumnName("status");
            entity.Property(e => e.TransactionId).HasColumnName("transaction_id");

            entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("payments_order_id_fkey");

            entity.HasOne(d => d.Transaction).WithMany(p => p.Payments)
                .HasForeignKey(d => d.TransactionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("payments_transaction_id_fkey");
        });

        modelBuilder.Entity<PlatformFeeRecord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("platform_fee_records_pkey");

            entity.ToTable("platform_fee_records");

            entity.HasIndex(e => e.CreatedAt, "idx_platform_fee_records_created_at");
            entity.HasIndex(e => e.ShopId, "idx_platform_fee_records_shop_id");
            entity.HasIndex(e => e.SellerId, "idx_platform_fee_records_seller_id");

            entity.HasIndex(e => e.OrderId, "platform_fee_records_order_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.PaymentId).HasColumnName("payment_id");
            entity.Property(e => e.ShopId).HasColumnName("shop_id");
            entity.Property(e => e.SellerId).HasColumnName("seller_id");
            entity.Property(e => e.GrossSubtotal)
                .HasPrecision(12, 2)
                .HasColumnName("gross_subtotal");
            entity.Property(e => e.CommissionPercent)
                .HasPrecision(6, 2)
                .HasColumnName("commission_percent");
            entity.Property(e => e.FeeAmount)
                .HasPrecision(12, 2)
                .HasColumnName("fee_amount");
            entity.Property(e => e.NetToSeller)
                .HasPrecision(12, 2)
                .HasColumnName("net_to_seller");
            entity.Property(e => e.Currency)
                .HasDefaultValueSql("'VND'::text")
                .HasColumnName("currency");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ReversedAt).HasColumnName("reversed_at");

            entity.HasOne(d => d.Order)
                .WithMany()
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("platform_fee_records_order_id_fkey");

            entity.HasOne(d => d.Payment)
                .WithMany()
                .HasForeignKey(d => d.PaymentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("platform_fee_records_payment_id_fkey");

            entity.HasOne(d => d.Shop)
                .WithMany()
                .HasForeignKey(d => d.ShopId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("platform_fee_records_shop_id_fkey");

            entity.HasOne(d => d.Seller)
                .WithMany()
                .HasForeignKey(d => d.SellerId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("platform_fee_records_seller_id_fkey");
        });

        modelBuilder.Entity<PlatformFeeConfig>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("platform_fee_configs_pkey");

            entity.ToTable("platform_fee_configs");

            entity.HasIndex(e => e.CreatedAt, "idx_platform_fee_configs_created_at");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CommissionPercent)
                .HasPrecision(5, 2)
                .HasColumnName("commission_percent");
            entity.Property(e => e.ChangedBy).HasColumnName("changed_by");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Admin)
                .WithMany()
                .HasForeignKey(d => d.ChangedBy)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("platform_fee_configs_changed_by_fkey");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("products_pkey");

            entity.ToTable("products");

            entity.HasIndex(e => e.CategoryId, "idx_products_category");

            entity.HasIndex(e => e.ShopId, "idx_products_shop");

            entity.HasIndex(e => e.Status, "idx_products_status");

            entity.HasIndex(e => e.Slug, "products_slug_key").IsUnique();

            entity.HasIndex(e => e.SearchVector, "products_search_idx").HasMethod("gin");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ProductCode).HasColumnName("product_code");
            entity.Property(e => e.BasePrice)
                .HasPrecision(12, 2)
                .HasColumnName("base_price");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasDefaultValueSql("'VND'::text")
                .HasColumnName("currency");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Slug).HasColumnName("slug");
            entity.Property(e => e.SearchVector)
                .HasComputedColumnSql("(setweight(to_tsvector('simple'::regconfig, f_immutable_unaccent(COALESCE(name, ''::text))), 'A'::\"char\") || setweight(to_tsvector('simple'::regconfig, f_immutable_unaccent(COALESCE(description, ''::text))), 'B'::\"char\"))", true)
                .HasColumnName("search_vector");
            entity.Property(e => e.ShopId).HasColumnName("shop_id");
            entity.Property(e => e.SoldCount)
                .HasDefaultValue(0)
                .HasColumnName("sold_count");
            entity.Property(e => e.Status)
                .HasDefaultValue((short)1)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.LastApprovedSnapshotJson)
                .HasColumnName("last_approved_snapshot_json");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("products_category_id_fkey");

            entity.HasOne(d => d.Shop).WithMany(p => p.Products)
                .HasForeignKey(d => d.ShopId)
                .HasConstraintName("products_shop_id_fkey");
        });

        modelBuilder.Entity<ProductTag>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.TagId }).HasName("product_tags_pkey");

            entity.ToTable("product_tags");

            entity.HasIndex(e => e.TagId, "idx_product_tags_tag");

            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.TagId).HasColumnName("tag_id");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductTags)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("product_tags_product_id_fkey");

            entity.HasOne(d => d.Tag).WithMany(p => p.ProductTags)
                .HasForeignKey(d => d.TagId)
                .HasConstraintName("product_tags_tag_id_fkey");
        });

        modelBuilder.Entity<ProductMaterial>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.MaterialId }).HasName("product_materials_pkey");

            entity.ToTable("product_materials");

            entity.HasIndex(e => e.MaterialId, "idx_product_materials_material");
            entity.HasIndex(e => e.ProductId, "idx_product_materials_product");

            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.MaterialId).HasColumnName("material_id");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductMaterials)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("product_materials_product_id_fkey");

            entity.HasOne(d => d.Material).WithMany(p => p.ProductMaterials)
                .HasForeignKey(d => d.MaterialId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("product_materials_material_id_fkey");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("product_images_pkey");

            entity.ToTable("product_images");

            entity.HasIndex(e => e.ProductId, "idx_product_images_product");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ImageUrl).HasColumnName("image_url");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.SortOrder)
                .HasDefaultValue(0)
                .HasColumnName("sort_order");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("product_images_product_id_fkey");
        });

        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("product_reviews_pkey");

            entity.ToTable("product_reviews");

            entity.HasIndex(e => e.ProductId, "idx_product_reviews_product");

            entity.HasIndex(e => e.Status, "idx_product_reviews_status");

            entity.HasIndex(e => e.UserId, "idx_product_reviews_user");

            entity.HasIndex(e => new { e.ProductId, e.UserId }, "product_reviews_product_id_user_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.Status)
                .HasDefaultValue((short)1)
                .HasColumnName("status");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            var reviewImagesConverter = new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v ?? new List<string>(), ReviewImageUrlsJsonOptions),
                v => string.IsNullOrEmpty(v)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(v, ReviewImageUrlsJsonOptions) ?? new List<string>());
            entity.Property(e => e.ImageUrls)
                .HasColumnName("image_urls")
                .HasColumnType("text")
                .HasConversion(reviewImagesConverter);
            entity.Property(e => e.SellerReply).HasColumnName("seller_reply");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductReviews)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("product_reviews_product_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.ProductReviews)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("product_reviews_user_id_fkey");
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("product_variants_pkey");

            entity.ToTable("product_variants");

            entity.HasIndex(e => e.IsActive, "idx_variants_active");

            entity.HasIndex(e => e.ProductId, "idx_variants_product");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Attributes)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("attributes");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Price)
                .HasPrecision(12, 2)
                .HasColumnName("price");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Sku).HasColumnName("sku");
            entity.Property(e => e.VariantName).HasColumnName("variant_name");
            entity.Property(e => e.Weight).HasColumnName("weight");
            entity.Property(e => e.Length).HasColumnName("length");
            entity.Property(e => e.Width).HasColumnName("width");
            entity.Property(e => e.Height).HasColumnName("height");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductVariants)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("product_variants_product_id_fkey");
        });

        modelBuilder.Entity<CustomerWallet>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("customer_wallets_pkey");

            entity.ToTable("customer_wallets");

            entity.HasIndex(e => e.CustomerId, "customer_wallets_customer_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.AvailableBalance)
                .HasPrecision(12, 2)
                .HasDefaultValue(0m)
                .HasColumnName("available_balance");
            entity.Property(e => e.Currency)
                .HasDefaultValueSql("'VND'::text")
                .HasColumnName("currency");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Customer).WithOne(p => p.CustomerWallet)
                .HasForeignKey<CustomerWallet>(d => d.CustomerId)
                .HasConstraintName("customer_wallets_customer_id_fkey");
        });

        modelBuilder.Entity<CustomerWalletLedger>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("customer_wallet_ledger_pkey");

            entity.ToTable("customer_wallet_ledger");

            entity.HasIndex(e => e.WalletId, "idx_customer_wallet_ledger_wallet_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.WalletId).HasColumnName("wallet_id");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.Amount)
                .HasPrecision(12, 2)
                .HasColumnName("amount");
            entity.Property(e => e.Currency)
                .HasDefaultValueSql("'VND'::text")
                .HasColumnName("currency");
            entity.Property(e => e.ReferenceType).HasColumnName("reference_type");
            entity.Property(e => e.ReferenceId).HasColumnName("reference_id");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Wallet).WithMany(p => p.CustomerWalletLedgers)
                .HasForeignKey(d => d.WalletId)
                .HasConstraintName("customer_wallet_ledger_wallet_fkey");
        });

        modelBuilder.Entity<CustomerWithdrawalRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("customer_withdrawal_requests_pkey");

            entity.ToTable("customer_withdrawal_requests");

            entity.HasIndex(e => e.CustomerId, "idx_customer_withdrawal_requests_customer_id");
            entity.HasIndex(e => e.Status, "idx_customer_withdrawal_requests_status")
                .HasFilter("(status = 0)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.WalletId).HasColumnName("wallet_id");
            entity.Property(e => e.Amount)
                .HasPrecision(12, 2)
                .HasColumnName("amount");
            entity.Property(e => e.Currency)
                .HasDefaultValueSql("'VND'::text")
                .HasColumnName("currency");
            entity.Property(e => e.BankName).HasColumnName("bank_name");
            entity.Property(e => e.BankAccountNumber).HasColumnName("bank_account_number");
            entity.Property(e => e.BankAccountName).HasColumnName("bank_account_name");
            entity.Property(e => e.Status)
                .HasDefaultValue((short)0)
                .HasColumnName("status");
            entity.Property(e => e.RejectionReason).HasColumnName("rejection_reason");
            entity.Property(e => e.AdminNote).HasColumnName("admin_note");
            entity.Property(e => e.RequestedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("requested_at");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.PaidAt).HasColumnName("paid_at");

            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerWithdrawalRequestCustomers)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("customer_withdrawal_requests_customer_id_fkey");

            entity.HasOne(d => d.Wallet).WithMany(p => p.CustomerWithdrawalRequests)
                .HasForeignKey(d => d.WalletId)
                .HasConstraintName("customer_withdrawal_requests_wallet_id_fkey");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.CustomerWithdrawalRequestReviewedByNavigations)
                .HasForeignKey(d => d.ReviewedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("customer_withdrawal_requests_reviewed_by_fkey");
        });

        modelBuilder.Entity<SellerWallet>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("seller_wallets_pkey");

            entity.ToTable("seller_wallets");

            entity.HasIndex(e => e.SellerId, "idx_seller_wallets_seller");

            entity.HasIndex(e => e.SellerId, "seller_wallets_seller_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.AvailableBalance)
                .HasPrecision(12, 2)
                .HasColumnName("available_balance");
            entity.Property(e => e.HeldBalance)
                .HasPrecision(12, 2)
                .HasDefaultValue(0m)
                .HasColumnName("held_balance");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasDefaultValueSql("'VND'::text")
                .HasColumnName("currency");
            entity.Property(e => e.PendingBalance)
                .HasPrecision(12, 2)
                .HasColumnName("pending_balance");
            entity.Property(e => e.SellerId).HasColumnName("seller_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Seller).WithOne(p => p.SellerWallet)
                .HasForeignKey<SellerWallet>(d => d.SellerId)
                .HasConstraintName("seller_wallets_seller_id_fkey");
        });

        modelBuilder.Entity<SellerWalletLedger>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("seller_wallet_ledger_pkey");

            entity.ToTable("seller_wallet_ledger");

            entity.HasIndex(e => new { e.ReferenceType, e.ReferenceId }, "idx_wallet_ledger_ref");

            entity.HasIndex(e => e.WalletId, "idx_wallet_ledger_wallet");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasPrecision(12, 2)
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasDefaultValueSql("'VND'::text")
                .HasColumnName("currency");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.ReferenceId).HasColumnName("reference_id");
            entity.Property(e => e.ReferenceType).HasColumnName("reference_type");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.WalletId).HasColumnName("wallet_id");

            entity.HasOne(d => d.Wallet).WithMany(p => p.SellerWalletLedgers)
                .HasForeignKey(d => d.WalletId)
                .HasConstraintName("seller_wallet_ledger_wallet_id_fkey");
        });

        modelBuilder.Entity<SellerWithdrawalRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("seller_withdrawal_requests_pkey");

            entity.ToTable("seller_withdrawal_requests");

            entity.HasIndex(e => e.SellerId, "idx_withdrawals_seller");

            entity.HasIndex(e => e.Status, "idx_withdrawals_status");

            entity.HasIndex(e => e.WalletId, "idx_withdrawals_wallet");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.AdminNote).HasColumnName("admin_note");
            entity.Property(e => e.Amount)
                .HasPrecision(12, 2)
                .HasColumnName("amount");
            entity.Property(e => e.BankAccountName).HasColumnName("bank_account_name");
            entity.Property(e => e.BankAccountNumber).HasColumnName("bank_account_number");
            entity.Property(e => e.BankName).HasColumnName("bank_name");
            entity.Property(e => e.Currency)
                .HasDefaultValueSql("'VND'::text")
                .HasColumnName("currency");
            entity.Property(e => e.PaidAt).HasColumnName("paid_at");
            entity.Property(e => e.RejectionReason).HasColumnName("rejection_reason");
            entity.Property(e => e.RequestedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("requested_at");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.SellerId).HasColumnName("seller_id");
            entity.Property(e => e.Status)
                .HasDefaultValue((short)0)
                .HasColumnName("status");
            entity.Property(e => e.WalletId).HasColumnName("wallet_id");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.SellerWithdrawalRequestReviewedByNavigations)
                .HasForeignKey(d => d.ReviewedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("seller_withdrawal_requests_reviewed_by_fkey");

            entity.HasOne(d => d.Seller).WithMany(p => p.SellerWithdrawalRequestSellers)
                .HasForeignKey(d => d.SellerId)
                .HasConstraintName("seller_withdrawal_requests_seller_id_fkey");

            entity.HasOne(d => d.Wallet).WithMany(p => p.SellerWithdrawalRequests)
                .HasForeignKey(d => d.WalletId)
                .HasConstraintName("seller_withdrawal_requests_wallet_id_fkey");
        });

        modelBuilder.Entity<Shop>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shops_pkey");

            entity.ToTable("shops");

            entity.HasIndex(e => e.OwnerId, "idx_shops_owner");

            entity.HasIndex(e => e.Status, "idx_shops_status");

            entity.HasIndex(e => e.Slug, "shops_slug_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ShopCode).HasColumnName("shop_code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.LogoUrl).HasColumnName("logo_url");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.OwnerId).HasColumnName("owner_id");
            entity.Property(e => e.RejectionReason).HasColumnName("rejection_reason");
            entity.Property(e => e.SuspensionReason).HasColumnName("suspension_reason");
            entity.Property(e => e.SuspendedAt).HasColumnName("suspended_at");
            entity.Property(e => e.SuspendedBy).HasColumnName("suspended_by");
            entity.Property(e => e.Phone).HasColumnName("phone");
            entity.Property(e => e.AddressLine).HasColumnName("address_line");
            entity.Property(e => e.WardCode).HasColumnName("ward_code");
            entity.Property(e => e.DistrictId).HasColumnName("district_id");
            entity.Property(e => e.ProvinceId).HasColumnName("province_id");
            entity.Property(e => e.City).HasColumnName("city");
            entity.Property(e => e.GhnShopId).HasColumnName("ghn_shop_id");
            entity.Property(e => e.BusinessType).HasColumnName("business_type");
            entity.Property(e => e.BusinessLicenseNumber).HasColumnName("business_license_number");
            entity.Property(e => e.TaxCode).HasColumnName("tax_code");
            entity.Property(e => e.BankName).HasColumnName("bank_name");
            entity.Property(e => e.BankAccountNumber).HasColumnName("bank_account_number");
            entity.Property(e => e.BankAccountName).HasColumnName("bank_account_name");
            entity.Property(e => e.IdentitySnapshotJson).HasColumnName("identity_snapshot_json");
            entity.Property(e => e.Slug).HasColumnName("slug");
            entity.Property(e => e.Status)
                .HasDefaultValue((short)1)
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.VerificationStatus)
                .HasDefaultValue((short)0)
                .HasColumnName("verification_status");
            entity.Property(e => e.VerifiedAt).HasColumnName("verified_at");
            entity.Property(e => e.VerifiedBy).HasColumnName("verified_by");
            entity.Property(e => e.CoverUrl).HasColumnName("cover_url");

            entity.HasOne(d => d.Owner).WithMany(p => p.ShopOwners)
                .HasForeignKey(d => d.OwnerId)
                .HasConstraintName("shops_owner_id_fkey");

            entity.HasOne(d => d.VerifiedByNavigation).WithMany(p => p.ShopVerifiedByNavigations)
                .HasForeignKey(d => d.VerifiedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("shops_verified_by_fkey");
        });

        modelBuilder.Entity<ShopFollow>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.ShopId }).HasName("shop_follows_pkey");

            entity.ToTable("shop_follows");

            entity.HasIndex(e => e.ShopId, "idx_shop_follows_shop");
            entity.HasIndex(e => e.UserId, "idx_shop_follows_user");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ShopId).HasColumnName("shop_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasOne(d => d.User).WithMany(p => p.ShopFollows)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("shop_follows_user_id_fkey");

            entity.HasOne(d => d.Shop).WithMany(p => p.ShopFollows)
                .HasForeignKey(d => d.ShopId)
                .HasConstraintName("shop_follows_shop_id_fkey");
        });

        modelBuilder.Entity<ShopDocument>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shop_documents_pkey");

            entity.ToTable("shop_documents");

            entity.HasIndex(e => e.ShopId, "idx_shop_documents_shop");

            entity.HasIndex(e => e.Status, "idx_shop_documents_status");

            entity.HasIndex(e => new { e.ShopId, e.DocType }, "idx_shop_documents_type");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.DocType).HasColumnName("doc_type");
            entity.Property(e => e.FileUrl).HasColumnName("file_url");
            entity.Property(e => e.RejectionReason).HasColumnName("rejection_reason");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.ShopId).HasColumnName("shop_id");
            entity.Property(e => e.Status)
                .HasDefaultValue((short)1)
                .HasColumnName("status");
            entity.Property(e => e.SubmittedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("submitted_at");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.ShopDocuments)
                .HasForeignKey(d => d.ReviewedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("shop_documents_reviewed_by_fkey");

            entity.HasOne(d => d.Shop).WithMany(p => p.ShopDocuments)
                .HasForeignKey(d => d.ShopId)
                .HasConstraintName("shop_documents_shop_id_fkey");
        });


        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tags_pkey");

            entity.ToTable("tags");

            entity.HasIndex(e => e.Name, "tags_name_key").IsUnique();

            entity.HasIndex(e => e.Slug, "tags_slug_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Slug).HasColumnName("slug");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("transactions_pkey");

            entity.ToTable("transactions");

            entity.HasIndex(e => e.CustomerId, "idx_transactions_customer");

            entity.HasIndex(e => e.Status, "idx_transactions_status");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasPrecision(12, 2)
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasDefaultValueSql("'VND'::text")
                .HasColumnName("currency");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.PaidAt).HasColumnName("paid_at");
            entity.Property(e => e.Provider).HasColumnName("provider");
            entity.Property(e => e.ProviderRef).HasColumnName("provider_ref");
            entity.Property(e => e.Status)
                .HasDefaultValue((short)0)
                .HasColumnName("status");

            entity.HasOne(d => d.Customer).WithMany(p => p.Transactions)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("transactions_customer_id_fkey");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pkey");

            entity.ToTable("roles");

            entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("roles_code_key");

            entity.Property(e => e.Id)
                .UseIdentityByDefaultColumn()
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Status, "idx_users_status");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.UserCode).HasColumnName("user_code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.FullName).HasColumnName("full_name");
            entity.Property(e => e.Phone).HasColumnName("phone");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.HasOne(d => d.Role)
                .WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("users_role_id_fkey");
            entity.Property(e => e.Status)
                .HasDefaultValue((short)1)
                .HasColumnName("status");
            entity.Property(e => e.SuspensionReason).HasColumnName("suspension_reason");
            entity.Property(e => e.SuspendedAt).HasColumnName("suspended_at");
            entity.Property(e => e.SuspendedBy).HasColumnName("suspended_by");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<UserAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_audit_logs_pkey");

            entity.ToTable("user_audit_logs");

            entity.HasIndex(e => e.UserId, "idx_user_audit_logs_user");
            entity.HasIndex(e => e.EditorId, "idx_user_audit_logs_editor");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.EditorId).HasColumnName("editor_id");
            entity.Property(e => e.Action).HasColumnName("action");
            entity.Property(e => e.FieldName).HasColumnName("field_name");
            entity.Property(e => e.OldValue).HasColumnName("old_value");
            entity.Property(e => e.NewValue).HasColumnName("new_value");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("user_audit_logs_user_id_fkey");

            entity.HasOne(d => d.Editor).WithMany()
                .HasForeignKey(d => d.EditorId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("user_audit_logs_editor_id_fkey");
        });

        modelBuilder.Entity<LocalSpecialtyProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("local_specialty_profiles_pkey");
            entity.ToTable("local_specialty_profiles");
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
            entity.Property(e => e.CategoryCode).HasColumnName("category_code").HasMaxLength(100);
            entity.Property(e => e.ProvinceName).HasColumnName("province_name").HasMaxLength(200);
            entity.Property(e => e.ArchetypeName).HasColumnName("archetype_name").HasMaxLength(200);
            entity.Property(e => e.ExpectedTraitsPipe).HasColumnName("expected_traits_pipe");
            entity.Property(e => e.KeywordsPipe).HasColumnName("keywords_pipe");
            entity.Property(e => e.DisplayNote).HasColumnName("display_note");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);

            entity.HasData(
                new LocalSpecialtyProfile
                {
                    Id = 1,
                    CategoryCode = "ca_phe",
                    ProvinceName = "Đắk Lắk",
                    ArchetypeName = "Robusta Buôn Ma Thuột",
                    ExpectedTraitsPipe = "Đắng đậm|Ít chua|Caffeine cao|Mùi chocolate",
                    KeywordsPipe = "robusta|buon ma thuot|buôn ma thuột|đắk lắk|dak lak|bmth",
                    DisplayNote = "Thủ phủ Robusta toàn cầu — hạt chắc, vị đắng đậm, ít chua, thoảng mùi chocolate.",
                    IsActive = true
                },
                new LocalSpecialtyProfile
                {
                    Id = 2,
                    CategoryCode = "ca_phe",
                    ProvinceName = "Lâm Đồng",
                    ArchetypeName = "Arabica Cầu Đất",
                    ExpectedTraitsPipe = "Chua thanh|Hương trái cây|Hậu ngọt|Body nhẹ",
                    KeywordsPipe = "arabica|cau dat|cầu đất|lâm đồng|lam dong|da lat|đà lạt",
                    DisplayNote = "Vùng cao Cầu Đất — Arabica chua thanh, hương trái cây tự nhiên, hậu vị ngọt dịu.",
                    IsActive = true
                },
                new LocalSpecialtyProfile
                {
                    Id = 3,
                    CategoryCode = "ca_phe",
                    ProvinceName = "Sơn La",
                    ArchetypeName = "Arabica Sơn La",
                    ExpectedTraitsPipe = "Thơm nhẹ|Chua dịu|Hậu vị sạch|Body vừa",
                    KeywordsPipe = "arabica|son la|sơn la|mộc châu|moc chau",
                    DisplayNote = "Vùng núi Tây Bắc — Arabica thơm nhẹ, chua dịu, hậu vị sạch.",
                    IsActive = true
                },
                new LocalSpecialtyProfile
                {
                    Id = 4,
                    CategoryCode = "ca_phe",
                    ProvinceName = "Gia Lai",
                    ArchetypeName = "Robusta Pleiku",
                    ExpectedTraitsPipe = "Đắng vừa|Mùi đất|Thể chất đậm|Ít chua",
                    KeywordsPipe = "robusta|pleiku|plei ku|gia lai|ia grai|chư sê",
                    DisplayNote = "Cao nguyên Gia Lai — Robusta đắng vừa, mùi đất đặc trưng Tây Nguyên.",
                    IsActive = true
                },
                new LocalSpecialtyProfile
                {
                    Id = 5,
                    CategoryCode = "ca_phe",
                    ProvinceName = "Quảng Trị",
                    ArchetypeName = "Arabica Khe Sanh",
                    ExpectedTraitsPipe = "Hương dịu|Chua nhẹ|Hậu vị thanh|Đất đỏ bazan",
                    KeywordsPipe = "arabica|khe sanh|quảng trị|quang tri|hướng hóa|huong hoa",
                    DisplayNote = "Vùng cao Khe Sanh — Arabica trồng trên đất đỏ bazan, hương dịu, vị chua nhẹ, hậu vị thanh đặc trưng.",
                    IsActive = true
                }
            );
        });

        modelBuilder.Entity<ProductLocalMeta>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("product_local_meta_pkey");
            entity.ToTable("product_local_meta");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.LocalSpecialtyProfileId).HasColumnName("local_specialty_profile_id");
            entity.Property(e => e.SelectedTraitsPipe).HasColumnName("selected_traits_pipe");
            entity.Property(e => e.MismatchWarning).HasColumnName("mismatch_warning");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Product)
                .WithOne()
                .HasForeignKey<ProductLocalMeta>(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("product_local_meta_product_id_fkey");

            entity.HasOne(d => d.LocalSpecialtyProfile)
                .WithMany(p => p.ProductLocalMetas)
                .HasForeignKey(d => d.LocalSpecialtyProfileId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("product_local_meta_profile_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
