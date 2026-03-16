using ECommerceAPI.Application.DTOs.Payments;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Domain.Enums;
using ECommerceAPI.Infrastructure.Configuration;
using ECommerceAPI.Infrastructure.Data;
using ECommerceAPI.Infrastructure.Payment;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ECommerceAPI.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly VNPaySettings _vnPaySettings;
    private readonly ILogger<PaymentService> _logger;
    private readonly IEmailService _emailService;
    private readonly IMemoryCache _memoryCache;

    public PaymentService(
        ApplicationDbContext context,
        IOptions<VNPaySettings> vnPaySettings,
        ILogger<PaymentService> logger,
        IEmailService emailService,
        IMemoryCache memoryCache)
    {
        _context = context;
        _vnPaySettings = vnPaySettings.Value;
        _logger = logger;
        _emailService = emailService;
        _memoryCache = memoryCache;
    }

    // ── 1. Tạo VNPay Payment URL ─────────────────────────────────────────────
    public async Task<CreatePaymentResponseDto> CreateVNPayPaymentAsync(
        Guid orderId, Guid customerId, string ipAddress)
    {
        // Lấy order và kiểm tra ownership
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId);

        if (order == null)
            return new CreatePaymentResponseDto { Success = false, Message = "Đơn hàng không tồn tại" };

        if ((OrderStatus)order.Status != OrderStatus.PendingPayment)
            return new CreatePaymentResponseDto { Success = false, Message = "Đơn hàng không ở trạng thái chờ thanh toán" };

        // Kiểm tra payment chưa thanh toán
        var existingPaid = await _context.Payments
            .AnyAsync(p => p.OrderId == orderId && p.Status == (short)PaymentStatus.Paid);

        if (existingPaid)
            return new CreatePaymentResponseDto { Success = false, Message = "Đơn hàng đã được thanh toán" };

        // Tạo Payment record (Pending)
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Provider = "VNPAY",
            Amount = order.Total,
            Currency = "VND",
            Status = (short)PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        // Tạo TxnRef = DateTime.Now.Ticks (giống dự án FlowerShop)
        var txnRef = DateTime.Now.Ticks.ToString();

        // Cache: txnRef → paymentId (expire 15 phút)
        _memoryCache.Set(
            $"TxnRef_{txnRef}",
            payment.Id,
            new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15))
        );

        // Build VNPay request
        var vnpay = new VNPayLibrary();
        vnpay.AddRequestData("vnp_Version", _vnPaySettings.Version);
        vnpay.AddRequestData("vnp_Command", _vnPaySettings.Command);
        vnpay.AddRequestData("vnp_TmnCode", _vnPaySettings.TmnCode);
        vnpay.AddRequestData("vnp_Amount", ((long)(order.Total * 100)).ToString());
        vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
        vnpay.AddRequestData("vnp_ExpireDate", DateTime.Now.AddMinutes(15).ToString("yyyyMMddHHmmss"));
        vnpay.AddRequestData("vnp_CurrCode", _vnPaySettings.CurrCode);
        vnpay.AddRequestData("vnp_IpAddr", ipAddress);
        vnpay.AddRequestData("vnp_Locale", _vnPaySettings.Locale);
        vnpay.AddRequestData("vnp_OrderInfo", $"Thanh toan don hang {orderId}");
        vnpay.AddRequestData("vnp_OrderType", "other");
        vnpay.AddRequestData("vnp_ReturnUrl", _vnPaySettings.ReturnUrl);
        vnpay.AddRequestData("vnp_TxnRef", txnRef);

        string paymentUrl = vnpay.CreateRequestUrl(_vnPaySettings.Url, _vnPaySettings.HashSecret);

        _logger.LogInformation("[VNPay] Created payment URL for OrderId: {OrderId}, PaymentId: {PaymentId}", orderId, payment.Id);

        return new CreatePaymentResponseDto
        {
            Success = true,
            PaymentUrl = paymentUrl,
            PaymentId = payment.Id,
            Message = "Tạo URL thanh toán thành công"
        };
    }

    // ── 2. Xử lý VNPay Return URL ────────────────────────────────────────────
    public async Task<VNPayReturnDto> ProcessVNPayReturnAsync(IQueryCollection queryParams)
    {
        _logger.LogInformation("[VNPay Return] Received callback with {Count} params", queryParams.Count);

        var vnpay = new VNPayLibrary();
        foreach (var (key, value) in queryParams)
        {
            vnpay.AddResponseData(key, value.ToString());
        }

        string vnpSecureHash = queryParams["vnp_SecureHash"].ToString();
        string responseCode = vnpay.GetResponseData("vnp_ResponseCode");
        string txnRef = vnpay.GetResponseData("vnp_TxnRef");
        string transactionNo = vnpay.GetResponseData("vnp_TransactionNo");
        string vnpAmountStr = vnpay.GetResponseData("vnp_Amount");

        _logger.LogInformation("[VNPay Return] ResponseCode: {Code}, TxnRef: {TxnRef}", responseCode, txnRef);

        // Validate signature
        bool isValidSignature = vnpay.ValidateSignature(vnpSecureHash, _vnPaySettings.HashSecret);
        if (!isValidSignature)
        {
            _logger.LogWarning("[VNPay Return] Invalid signature!");
            return new VNPayReturnDto { Success = false, Message = "Chữ ký không hợp lệ", ResponseCode = "97" };
        }

        // Lấy PaymentId từ cache
        if (!_memoryCache.TryGetValue($"TxnRef_{txnRef}", out Guid paymentId))
        {
            _logger.LogError("[VNPay Return] No matching payment for TxnRef: {TxnRef}", txnRef);
            return new VNPayReturnDto { Success = false, Message = "Không tìm thấy giao dịch", ResponseCode = "01" };
        }

        // Lấy Payment và Order
        var payment = await _context.Payments
            .Include(p => p.Order)
                .ThenInclude(o => o.OrderItems)
            .Include(p => p.Order)
                .ThenInclude(o => o.Customer)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null)
        {
            _logger.LogError("[VNPay Return] Payment {PaymentId} not found", paymentId);
            return new VNPayReturnDto { Success = false, Message = "Không tìm thấy thanh toán", ResponseCode = "01" };
        }

        // Idempotent: nếu đã xử lý rồi thì return luôn
        if (payment.Status == (short)PaymentStatus.Paid)
        {
            return new VNPayReturnDto
            {
                Success = true,
                Message = "Thanh toán đã được xử lý trước đó",
                OrderId = payment.OrderId,
                PaymentId = payment.Id,
                Amount = payment.Amount
            };
        }

        var order = payment.Order;
        decimal amount = long.TryParse(vnpAmountStr, out long rawAmount) ? rawAmount / 100m : payment.Amount;

        if (responseCode == "00")
        {
            // ── THANH TOÁN THÀNH CÔNG ────────────────────────────────────────
            payment.Status = (short)PaymentStatus.Paid;
            payment.PaidAt = DateTime.UtcNow;
            payment.ProviderRef = transactionNo;

            order.Status = (short)OrderStatus.Confirmed;
            order.UpdatedAt = DateTime.UtcNow;

            // Giảm tồn kho (ReservedQuantity đã cộng lúc checkout, giờ trừ Quantity thật)
            foreach (var item in order.OrderItems)
            {
                var inv = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.VariantId == item.VariantId);

                if (inv != null)
                {
                    inv.Quantity -= item.Quantity;
                    inv.ReservedQuantity -= item.Quantity;
                    if (inv.ReservedQuantity < 0) inv.ReservedQuantity = 0;
                    inv.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            // Email: User entity không có Email field (quản lý bởi Supabase Auth)
            // Có thể mở rộng sau nếu cần

            _logger.LogInformation("[VNPay Return] Payment SUCCESS for OrderId: {OrderId}", order.Id);

            return new VNPayReturnDto
            {
                Success = true,
                Message = "Thanh toán thành công",
                ResponseCode = responseCode,
                OrderId = order.Id,
                PaymentId = payment.Id,
                Amount = amount
            };
        }
        else
        {
            // ── THANH TOÁN THẤT BẠI ─────────────────────────────────────────
            payment.Status = (short)PaymentStatus.Failed;
            payment.PaidAt = DateTime.UtcNow;

            order.Status = (short)OrderStatus.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;

            // Hoàn lại reserved quantity
            foreach (var item in order.OrderItems)
            {
                var inv = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.VariantId == item.VariantId);

                if (inv != null)
                {
                    inv.ReservedQuantity -= item.Quantity;
                    if (inv.ReservedQuantity < 0) inv.ReservedQuantity = 0;
                    inv.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogWarning("[VNPay Return] Payment FAILED for OrderId: {OrderId}, Code: {Code}", order.Id, responseCode);

            return new VNPayReturnDto
            {
                Success = false,
                Message = $"Thanh toán thất bại. Mã lỗi: {responseCode}",
                ResponseCode = responseCode,
                OrderId = order.Id,
                PaymentId = payment.Id,
                Amount = amount
            };
        }
    }

    // ── Email xác nhận ───────────────────────────────────────────────────────
    private async Task SendConfirmationEmailAsync(string email, string fullName, Order order, decimal totalAmount)
    {
        var itemsHtml = string.Join("", order.OrderItems.Select(item =>
            $"<tr><td>{item.ProductName}</td><td style='text-align:center'>{item.Quantity}</td><td style='text-align:right'>{item.LineTotal:N0} VNĐ</td></tr>"
        ));

        string body = $@"
        <html><body style='font-family:Arial,sans-serif;max-width:600px;margin:auto'>
            <h2 style='color:#2c7be5'>✅ Thanh toán thành công!</h2>
            <p>Xin chào <b>{fullName}</b>,</p>
            <p>Đơn hàng <b>#{order.Id.ToString()[..8].ToUpper()}</b> của bạn đã được xác nhận.</p>
            <table border='1' cellpadding='8' cellspacing='0' width='100%' style='border-collapse:collapse'>
                <thead style='background:#f8f9fa'>
                    <tr><th>Sản phẩm</th><th>Số lượng</th><th>Thành tiền</th></tr>
                </thead>
                <tbody>{itemsHtml}</tbody>
            </table>
            <p style='text-align:right;font-size:16px'>
                <b>Phí vận chuyển:</b> {order.ShippingFee:N0} VNĐ<br/>
                <b>Tổng cộng:</b> <span style='color:#e63757'>{totalAmount:N0} VNĐ</span>
            </p>
            <p>Cảm ơn bạn đã mua hàng tại E-Commerce Platform!</p>
        </body></html>";

        await _emailService.SendAsync(email, $"✅ Xác nhận đơn hàng #{order.Id.ToString()[..8].ToUpper()}", body);
        _logger.LogInformation("[VNPay] Confirmation email sent to {Email}", email);
    }
}
