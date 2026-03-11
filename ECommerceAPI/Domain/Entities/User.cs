using System;
using System.Collections.Generic;

namespace ECommerceAPI.Domain.Entities;

public partial class User
{
    public Guid Id { get; set; }

    public string? FullName { get; set; }

    public string? Phone { get; set; }

    public short? RoleId { get; set; }

    public short Status { get; set; }

    public string? SuspensionReason { get; set; }

    public DateTime? SuspendedAt { get; set; }

    public Guid? SuspendedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Role? Role { get; set; }

    public virtual ICollection<Address> Addresses { get; set; } = new List<Address>();

        public virtual ICollection<AiChatSession> AiChatSessions { get; set; } = new List<AiChatSession>();

    public virtual ICollection<AiMaterialSuggestion> AiMaterialSuggestions { get; set; } = new List<AiMaterialSuggestion>();

    public virtual ICollection<AiTagSuggestion> AiTagSuggestions { get; set; } = new List<AiTagSuggestion>();

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<Conversation> ConversationBuyers { get; set; } = new List<Conversation>();

    public virtual ICollection<Conversation> ConversationSellers { get; set; } = new List<Conversation>();

    public virtual ICollection<FavoriteProduct> FavoriteProducts { get; set; } = new List<FavoriteProduct>();

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();

    public virtual SellerWallet? SellerWallet { get; set; }

    public virtual ICollection<SellerWithdrawalRequest> SellerWithdrawalRequestReviewedByNavigations { get; set; } = new List<SellerWithdrawalRequest>();

    public virtual ICollection<SellerWithdrawalRequest> SellerWithdrawalRequestSellers { get; set; } = new List<SellerWithdrawalRequest>();

    public virtual ICollection<ShopDocument> ShopDocuments { get; set; } = new List<ShopDocument>();

    public virtual ICollection<Shop> ShopOwners { get; set; } = new List<Shop>();

    public virtual ICollection<ShopReview> ShopReviews { get; set; } = new List<ShopReview>();

    public virtual ICollection<Shop> ShopVerifiedByNavigations { get; set; } = new List<Shop>();

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    // Disputes
    public virtual ICollection<Dispute> DisputesAsCustomer { get; set; } = new List<Dispute>();
    public virtual ICollection<Dispute> DisputesResolvedByNavigation { get; set; } = new List<Dispute>();
    public virtual ICollection<DisputeMessage> DisputeMessages { get; set; } = new List<DisputeMessage>();
}
