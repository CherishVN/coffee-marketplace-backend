using System;
using System.Collections.Generic;
using NpgsqlTypes;

namespace ECommerceAPI.Domain.Entities;

public partial class Product
{
    public Guid Id { get; set; }

    public string ProductCode { get; set; } = null!;

    public Guid ShopId { get; set; }

    public long? CategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal BasePrice { get; set; }

    public string Currency { get; set; } = null!;

    public short Status { get; set; }

    public int SoldCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public NpgsqlTsVector? SearchVector { get; set; }

    public virtual ICollection<AiMaterialSuggestion> AiMaterialSuggestions { get; set; } = new List<AiMaterialSuggestion>();

    public virtual ICollection<AiProductRecommendation> AiProductRecommendations { get; set; } = new List<AiProductRecommendation>();

    public virtual ICollection<AiRecommendationItem> AiRecommendationItems { get; set; } = new List<AiRecommendationItem>();

    public virtual ICollection<AiTagSuggestion> AiTagSuggestions { get; set; } = new List<AiTagSuggestion>();

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual Category? Category { get; set; }

    public virtual ICollection<FavoriteProduct> FavoriteProducts { get; set; } = new List<FavoriteProduct>();

    public virtual ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public virtual ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();

    public virtual ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();

    public virtual Shop Shop { get; set; } = null!;

    public virtual ICollection<ProductMaterial> ProductMaterials { get; set; } = new List<ProductMaterial>();

    public virtual ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
}
