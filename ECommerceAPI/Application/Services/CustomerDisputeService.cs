using System.Text.Json;
using ECommerceAPI.Application;
using ECommerceAPI.Application.DTOs.Disputes;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class CustomerDisputeService : ICustomerDisputeService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notifications;

    /// <summary>Cửa sổ khiếu nại sau khi nhận hàng (mốc từ lịch sử Đã giao / Hoàn thành).</summary>
    private const int DisputeWindowDaysAfterReceipt = 7;

    private const int NotReceivedMinDaysInShipping = 5;
    private const int NotReceivedMinDaysInProcessing = 7;
    private const int NotReceivedDaysPastEta = 3;

    // Terminal statuses where evidence can no longer be updated
    private static readonly DisputeStatus[] FinalStatuses =
    [
        DisputeStatus.Resolved,
        DisputeStatus.Rejected,
        DisputeStatus.Refunded,
        DisputeStatus.Cancelled
    ];

    // Statuses where customer is allowed to cancel:
    //   Pending       — chưa ai xử lý, huỷ thoải mái
    //   WaitingSeller — admin đang chờ seller, customer đã tự dàn xếp được
    //   WaitingCustomer — admin chờ phản hồi thêm từ customer, customer chủ động đóng
    // KHÔNG cho huỷ khi UnderReview vì admin đang bỏ công xử lý
    private static readonly DisputeStatus[] CustomerCancellableStatuses =
    [
        DisputeStatus.Pending,
        DisputeStatus.WaitingSeller,
        DisputeStatus.WaitingCustomer
    ];

    public CustomerDisputeService(ApplicationDbContext context, INotificationService notifications)
    {
        _context = context;
        _notifications = notifications;
    }

    public async Task<CustomerDisputeResponseDto> CreateDisputeAsync(Guid customerId, CreateDisputeDto dto)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.Shop)
            .Include(o => o.OrderStatusHistories)
            .Include(o => o.Shipments)
            .FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.CustomerId == customerId);

        if (order == null)
        {
            return Fail("Không tìm thấy đơn hàng");
        }

        var now = DateTime.UtcNow;
        var typeErr = ValidateCreateRulesForDisputeType(order, dto.Type, now);
        if (typeErr != null)
            return Fail(typeErr);

        // BR: Một đơn — một khiếu nại (đang mở)
        var existingDispute = await _context.Disputes
            .AnyAsync(d => d.OrderId == dto.OrderId);

        if (existingDispute)
        {
            return Fail("Đơn hàng này đã có khiếu nại");
        }

        if (dto.Items == null || dto.Items.Count == 0)
        {
            return Fail("Vui lòng chọn ít nhất một sản phẩm và số lượng bị khiếu nại.");
        }

        var itemById = order.OrderItems.ToDictionary(i => i.Id);
        var seen = new HashSet<Guid>();
        decimal sumAffectedGoods = 0;

        foreach (var line in dto.Items)
        {
            if (!seen.Add(line.OrderItemId))
                return Fail("Không được chọn trùng một dòng sản phẩm. Gộp số lượng vào một dòng.");

            if (!itemById.TryGetValue(line.OrderItemId, out var oi))
                return Fail("Có sản phẩm không thuộc đơn hàng này.");

            if (line.Quantity < 1 || line.Quantity > oi.Quantity)
            {
                return Fail(
                    $"Số lượng khiếu nại không hợp lệ cho «{oi.ProductName}» (tối đa {oi.Quantity} theo đơn).");
            }

            sumAffectedGoods += oi.UnitPrice * line.Quantity;
        }

        // BR: Số tiền yêu cầu không vượt tổng đơn; với phần hàng đã chọn — không vượt giá trị phần đó
        if (dto.RequestedAmount > order.Total)
        {
            return Fail($"Số tiền yêu cầu không được vượt quá tổng giá trị đơn hàng ({order.Total:N0} VND).");
        }

        if (dto.RequestedAmount > sumAffectedGoods)
        {
            return Fail(
                $"Số tiền yêu cầu không được vượt quá giá trị các sản phẩm đã chọn ({sumAffectedGoods:N0} VND).");
        }

        if (dto.Type == (short)DisputeType.Refund && dto.RequestedAmount <= 0)
        {
            return Fail("Với loại «Hoàn tiền», vui lòng nhập số tiền hoàn lớn hơn 0.");
        }

        var evidenceJson = dto.EvidenceUrls != null && dto.EvidenceUrls.Count > 0
            ? JsonSerializer.Serialize(dto.EvidenceUrls)
            : "[]";

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            OrderId = dto.OrderId,
            CustomerId = customerId,
            ShopId = order.ShopId,
            Type = dto.Type,
            Status = (short)DisputeStatus.Pending,
            Title = dto.Title,
            Reason = dto.Reason,
            EvidenceUrls = evidenceJson,
            RequestedAmount = dto.RequestedAmount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Disputes.Add(dispute);

        foreach (var line in dto.Items)
        {
            var oi = itemById[line.OrderItemId];
            var lineSnap = oi.UnitPrice * line.Quantity;
            _context.DisputeOrderItems.Add(new DisputeOrderItem
            {
                Id = Guid.NewGuid(),
                DisputeId = dispute.Id,
                OrderItemId = oi.Id,
                Quantity = line.Quantity,
                UnitPriceSnapshot = oi.UnitPrice,
                LineSnapshotTotal = lineSnap,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        var orderRef = NotificationFormatting.ShortEntityId(order.Id);
        await _notifications.PublishAsync(
            order.Shop.OwnerId,
            nameof(NotificationType.Dispute),
            "Khiếu nại mới",
            $"Khách hàng đã tạo khiếu nại cho đơn #{orderRef}: {dto.Title}",
            "Dispute",
            dispute.Id,
            queueEmail: true);

        await _notifications.PublishToUsersWithRoleAsync(
            "admin",
            nameof(NotificationType.Dispute),
            "Khiếu nại mới cần xử lý",
            $"Cửa hàng «{order.Shop.Name}»: khách tạo khiếu nại cho đơn #{orderRef} — {dto.Title}.",
            "Dispute",
            dispute.Id,
            queueEmail: false);

        var created = await _context.Disputes
            .Include(d => d.DisputeOrderItems)
            .ThenInclude(x => x.OrderItem)
            .Include(d => d.Shop)
            .FirstAsync(d => d.Id == dispute.Id);

        return new CustomerDisputeResponseDto
        {
            Success = true,
            Message = "Tạo khiếu nại thành công.",
            Dispute = MapToDto(created, created.Shop.Name)
        };
    }

    public async Task<CustomerDisputeResponseDto> UpdateEvidenceAsync(Guid customerId, Guid disputeId, UpdateEvidenceDto dto)
    {
        var dispute = await _context.Disputes
            .Include(d => d.Shop)
            .Include(d => d.DisputeOrderItems)
            .ThenInclude(x => x.OrderItem)
            .FirstOrDefaultAsync(d => d.Id == disputeId && d.CustomerId == customerId);

        if (dispute == null)
        {
            return Fail("Không tìm thấy khiếu nại");
        }

        // BR: Evidence can be updated only before admin final decision
        if (FinalStatuses.Contains((DisputeStatus)dispute.Status))
        {
            return Fail("Không thể cập nhật bằng chứng sau khi admin đã ra quyết định cuối");
        }

        dispute.EvidenceUrls = JsonSerializer.Serialize(dto.EvidenceUrls);
        if (dto.CustomerNote != null)
            dispute.CustomerNote = dto.CustomerNote.Trim();

        // Nếu admin đang chờ customer phản hồi → tự động chuyển về UnderReview sau khi customer submit
        var wasWaitingCustomer = (DisputeStatus)dispute.Status == DisputeStatus.WaitingCustomer;
        if (wasWaitingCustomer)
            dispute.Status = (short)DisputeStatus.UnderReview;

        dispute.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var ordRef = NotificationFormatting.ShortEntityId(dispute.OrderId);
        if (wasWaitingCustomer)
        {
            await _notifications.PublishToUsersWithRoleAsync(
                "admin",
                nameof(NotificationType.Dispute),
                "Khách đã bổ sung thông tin khiếu nại",
                $"Đơn #{ordRef} — «{dispute.Title}»: khách đã gửi phản hồi hoặc bằng chứng bổ sung.",
                "Dispute",
                dispute.Id,
                queueEmail: false);
        }

        return new CustomerDisputeResponseDto
        {
            Success = true,
            Message = wasWaitingCustomer
                ? "Đã gửi phản hồi. Admin sẽ xem xét và liên hệ lại với bạn."
                : "Cập nhật bằng chứng thành công",
            Dispute = MapToDto(dispute, dispute.Shop.Name)
        };
    }

    public async Task<CustomerDisputeListResponseDto> GetMyDisputesAsync(Guid customerId, int page, int pageSize, short? status = null)
    {
        var query = _context.Disputes
            .Include(d => d.Shop)
            .Where(d => d.CustomerId == customerId);

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        var totalCount = await query.CountAsync();

        var disputes = await query
            .Include(d => d.DisputeOrderItems)
            .ThenInclude(x => x.OrderItem)
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new CustomerDisputeListResponseDto
        {
            Success = true,
            Disputes = disputes.Select(d => MapToDto(d, d.Shop.Name)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CustomerDisputeResponseDto> GetDisputeByIdAsync(Guid customerId, Guid disputeId)
    {
        var dispute = await _context.Disputes
            .Include(d => d.Shop)
            .Include(d => d.DisputeOrderItems)
            .ThenInclude(x => x.OrderItem)
            .FirstOrDefaultAsync(d => d.Id == disputeId && d.CustomerId == customerId);

        if (dispute == null)
        {
            return Fail("Không tìm thấy khiếu nại");
        }

        return new CustomerDisputeResponseDto
        {
            Success = true,
            Dispute = MapToDto(dispute, dispute.Shop.Name)
        };
    }

    public async Task<CustomerDisputeResponseDto> CancelDisputeAsync(Guid customerId, Guid disputeId)
    {
        var dispute = await _context.Disputes
            .Include(d => d.Shop)
            .Include(d => d.DisputeOrderItems)
            .ThenInclude(x => x.OrderItem)
            .FirstOrDefaultAsync(d => d.Id == disputeId && d.CustomerId == customerId);

        if (dispute == null)
        {
            return Fail("Không tìm thấy khiếu nại");
        }

        if (!CustomerCancellableStatuses.Contains((DisputeStatus)dispute.Status))
        {
            return Fail("Không thể hủy khiếu nại ở trạng thái này. " +
                "Chỉ được hủy khi đang Chờ xử lý, Chờ seller phản hồi hoặc Chờ phản hồi từ bạn.");
        }

        dispute.Status = (short)DisputeStatus.Cancelled;
        dispute.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var ordRef = NotificationFormatting.ShortEntityId(dispute.OrderId);
        await _notifications.PublishAsync(
            dispute.Shop.OwnerId,
            nameof(NotificationType.Dispute),
            "Khiếu nại đã hủy",
            $"Khách hàng đã hủy khiếu nại liên quan đơn #{ordRef}.",
            "Dispute",
            dispute.Id,
            queueEmail: true);

        await _notifications.PublishToUsersWithRoleAsync(
            "admin",
            nameof(NotificationType.Dispute),
            "Khách hủy khiếu nại",
            $"Đơn #{ordRef}: khách đã hủy khiếu nại «{dispute.Title}».",
            "Dispute",
            dispute.Id,
            queueEmail: false);

        return new CustomerDisputeResponseDto
        {
            Success = true,
            Message = "Đã hủy khiếu nại",
            Dispute = MapToDto(dispute, dispute.Shop.Name)
        };
    }

    private static CustomerDisputeDto MapToDto(Dispute dispute, string shopName)
    {
        var evidenceUrls = TryDeserializeUrls(dispute.EvidenceUrls);
        var sellerEvidenceUrls = TryDeserializeUrls(dispute.SellerEvidenceUrls);
        var isFinal = FinalStatuses.Contains((DisputeStatus)dispute.Status);

        return new CustomerDisputeDto
        {
            Id = dispute.Id,
            OrderId = dispute.OrderId,
            ShopId = dispute.ShopId,
            ShopName = shopName,
            Type = dispute.Type,
            Status = dispute.Status,
            Title = dispute.Title,
            Reason = dispute.Reason,
            RequestedAmount = dispute.RequestedAmount,
            ApprovedAmount = dispute.ApprovedAmount,
            Resolution = dispute.Resolution,
            EvidenceUrls = evidenceUrls,
            SellerEvidenceUrls = sellerEvidenceUrls,
            SellerResponse = dispute.SellerResponse,
            SellerRespondedAt = dispute.SellerRespondedAt,
            CreatedAt = dispute.CreatedAt,
            UpdatedAt = dispute.UpdatedAt,
            CanUpdateEvidence = !isFinal,
            CustomerNote = dispute.CustomerNote,
            AdminNote = string.IsNullOrWhiteSpace(dispute.AdminNote) ? null : dispute.AdminNote.Trim(),
            AffectedItems = MapAffectedItems(dispute)
        };
    }

    private static List<DisputeAffectedItemDto> MapAffectedItems(Dispute dispute)
    {
        if (dispute.DisputeOrderItems == null || dispute.DisputeOrderItems.Count == 0)
            return new List<DisputeAffectedItemDto>();

        return dispute.DisputeOrderItems
            .OrderBy(x => x.OrderItem?.ProductName)
            .Select(r => new DisputeAffectedItemDto
            {
                OrderItemId = r.OrderItemId,
                ProductName = r.OrderItem?.ProductName ?? "",
                Quantity = r.Quantity,
                UnitPrice = r.UnitPriceSnapshot,
                LineTotal = r.LineSnapshotTotal
            })
            .ToList();
    }

    private static List<string> TryDeserializeUrls(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static CustomerDisputeResponseDto Fail(string message) => new()
    {
        Success = false,
        Message = message
    };

    /// <summary>
    /// Các loại cần đã nhận hàng (hoặc đã xác nhận hoàn tất) mới khiếu nại được.
    /// <see cref="DisputeType.NotReceived"/> xử lý riêng.
    /// </summary>
    private string? ValidateCreateRulesForDisputeType(Order order, short disputeTypeRaw, DateTime utcNow)
    {
        if (!Enum.IsDefined(typeof(DisputeType), disputeTypeRaw))
            return "Loại khiếu nại không hợp lệ.";

        var disputeType = (DisputeType)disputeTypeRaw;
        if (disputeType == DisputeType.NotReceived)
            return ValidateNotReceivedCreateRules(order, utcNow);

        var allowedPostReceipt = new[]
        {
            (short)OrderStatus.Delivered,
            (short)OrderStatus.Completed
        };

        if (!allowedPostReceipt.Contains(order.Status))
        {
            return "Với loại khiếu nại này, chỉ có thể khiếu nại khi đơn đã giao (Delivered) hoặc đã hoàn thành (Completed).";
        }

        var receiptAnchor = GetReceiptAnchorUtc(order);
        if ((utcNow - receiptAnchor).TotalDays > DisputeWindowDaysAfterReceipt)
        {
            return $"Đã quá thời hạn khiếu nại ({DisputeWindowDaysAfterReceipt} ngày kể từ khi nhận hàng — theo thời điểm đơn Đã giao hoặc bạn xác nhận Hoàn thành).";
        }

        return null;
    }

    /// <summary>
    /// Không nhận được hàng: cho phép khi đang chuẩn bị / đang giao (sau ngưỡng ngày hoặc quá ETA),
    /// hoặc khi shop đã báo Delivered nhưng khách phản đối (trong 7 ngày kể từ Đã giao).
    /// Không cho khi đơn đã Completed (đã xác nhận nhận hàng), đã hủy / hoàn tiền, hoặc chưa thanh toán xác nhận.
    /// </summary>
    private string? ValidateNotReceivedCreateRules(Order order, DateTime utcNow)
    {
        var st = (OrderStatus)order.Status;

        if (st is OrderStatus.PendingPayment or OrderStatus.PendingConfirmation or OrderStatus.Cancelled
            or OrderStatus.Refunded)
        {
            return "Không thể khiếu nại «Không nhận được hàng» ở trạng thái đơn hiện tại.";
        }

        if (st == OrderStatus.Completed)
        {
            return "Đơn đã hoàn thành (bạn đã xác nhận nhận hàng). Không thể tạo khiếu nại «Không nhận được hàng».";
        }

        if (st == OrderStatus.Confirmed)
        {
            return "Đơn mới được xác nhận, chưa giao. Vui lòng đợi shop chuẩn bị / gửi hàng; nếu quá lâu không cập nhật, liên hệ hỗ trợ.";
        }

        if (st == OrderStatus.Delivered)
        {
            var deliveredAt = FirstEnteredStatusAtUtc(order, OrderStatus.Delivered) ?? order.UpdatedAt;
            if ((utcNow - deliveredAt).TotalDays > DisputeWindowDaysAfterReceipt)
            {
                return $"Đã quá thời hạn khiếu nại ({DisputeWindowDaysAfterReceipt} ngày kể từ khi đơn chuyển sang Đã giao / nhận hàng).";
            }

            return null;
        }

        if (st == OrderStatus.Processing)
        {
            if (NotReceivedDelayElapsedForStatus(order, OrderStatus.Processing, utcNow, NotReceivedMinDaysInProcessing))
                return null;

            return $"Khiếu nại «Không nhận được hàng» khi đơn đang chuẩn bị chỉ được sau ít nhất {NotReceivedMinDaysInProcessing} ngày kể từ khi đơn vào trạng thái này (tránh khiếu nại sớm).";
        }

        if (st == OrderStatus.Shipping)
        {
            if (NotReceivedShippingDelayOrEtaElapsed(order, utcNow))
                return null;

            return
                $"Khiếu nại «Không nhận được hàng» khi đơn đang giao chỉ được sau ít nhất {NotReceivedMinDaysInShipping} ngày kể từ khi đơn chuyển sang Đang giao, " +
                $"hoặc sau {NotReceivedDaysPastEta} ngày kể từ ngày dự kiến giao (nếu có).";
        }

        return "Không thể khiếu nại «Không nhận được hàng» ở trạng thái đơn hiện tại.";
    }

    private static DateTime? FirstEnteredStatusAtUtc(Order order, OrderStatus status)
    {
        var list = order.OrderStatusHistories;
        if (list == null || list.Count == 0)
            return null;

        var target = (short)status;
        return list
            .Where(h => h.NewStatus == target)
            .OrderBy(h => h.CreatedAt)
            .Select(h => (DateTime?)h.CreatedAt)
            .FirstOrDefault();
    }

    private static bool NotReceivedDelayElapsedForStatus(
        Order order,
        OrderStatus status,
        DateTime utcNow,
        int minWholeDays)
    {
        var entered = FirstEnteredStatusAtUtc(order, status);
        var anchor = entered ?? order.UpdatedAt;
        return (utcNow - anchor).TotalDays >= minWholeDays;
    }

    private static bool NotReceivedShippingDelayOrEtaElapsed(Order order, DateTime utcNow)
    {
        if (NotReceivedDelayElapsedForStatus(order, OrderStatus.Shipping, utcNow, NotReceivedMinDaysInShipping))
            return true;

        var ship = order.ShipmentForDisplay();
        if (ship?.EstimatedDeliveryDate is { } eta)
        {
            var deadline = eta.AddDays(NotReceivedDaysPastEta);
            // Cùng mốc thời gian với tham số (không dùng UtcNow lệch) để trùng với CreateDispute/validate
            if (new DateTimeOffset(utcNow, TimeSpan.Zero) >= deadline)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Mốc «nhận hàng» cho cửa sổ khiếu nại: sớm nhất trong (lần đầu Đã giao, lần đầu Hoàn thành).
    /// Không có lịch sử → <see cref="Order.UpdatedAt"/> (dữ liệu cũ).
    /// </summary>
    private static DateTime GetReceiptAnchorUtc(Order order)
    {
        var deliveredAt = FirstEnteredStatusAtUtc(order, OrderStatus.Delivered);
        var completedAt = FirstEnteredStatusAtUtc(order, OrderStatus.Completed);
        var candidates = new List<DateTime>();
        if (deliveredAt.HasValue) candidates.Add(deliveredAt.Value);
        if (completedAt.HasValue) candidates.Add(completedAt.Value);
        if (candidates.Count > 0)
            return candidates.Min();
        return order.UpdatedAt;
    }
}
