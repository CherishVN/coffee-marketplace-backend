using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Data;

namespace ECommerceAPI.Application.Services;

public class OrderStatusHistoryService : IOrderStatusHistoryService
{
    private const int MaxNoteLength = 4000;
    private readonly ApplicationDbContext _context;

    public OrderStatusHistoryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public void AddEntry(
        Guid orderId,
        short? previousStatus,
        short newStatus,
        Guid? changedBy,
        string? note)
    {
        if (previousStatus is { } p && p == newStatus)
            return;

        var normalized = string.IsNullOrWhiteSpace(note)
            ? null
            : (note.Length > MaxNoteLength ? note.AsSpan(0, MaxNoteLength).ToString() : note.Trim());

        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ChangedBy = changedBy,
            Note = normalized,
            CreatedAt = DateTime.UtcNow
        });
    }
}
