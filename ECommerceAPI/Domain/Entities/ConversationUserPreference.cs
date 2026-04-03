using System;

namespace ECommerceAPI.Domain.Entities;

/// <summary>Tùy chọn chat theo từng người (tắt thông báo / ẩn khỏi danh sách).</summary>
public partial class ConversationUserPreference
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public Guid UserId { get; set; }

    public bool IsMuted { get; set; }

    /// <summary>Khác null = người dùng đã ẩn cuộc trò chuyện khỏi danh sách.</summary>
    public DateTime? HiddenAt { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
