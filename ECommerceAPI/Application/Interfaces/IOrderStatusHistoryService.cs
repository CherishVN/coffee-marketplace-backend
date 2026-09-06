namespace ECommerceAPI.Application.Interfaces;

public interface IOrderStatusHistoryService
{
    /// <summary>
    /// Ghi một dòng lịch sử (chưa SaveChanges). Bỏ qua nếu trùng trạng thái (trừ khi previous null — luôn ghi tạo đơn với new).
    /// </summary>
    void AddEntry(
        Guid orderId,
        short? previousStatus,
        short newStatus,
        Guid? changedBy,
        string? note);
}
