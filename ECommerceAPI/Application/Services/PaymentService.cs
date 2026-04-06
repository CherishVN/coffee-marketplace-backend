using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECommerceAPI.Application;
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
    private readonly MoMoSettings _moMoSettings;
    private readonly ILogger<PaymentService> _logger;
    private readonly INotificationService _notifications;
    private readonly ISellerWalletSettlementService _sellerWalletSettlement;
    private readonly IMemoryCache _memoryCache;
    private readonly HttpClient _httpClient;

    public PaymentService(
        ApplicationDbContext context,
        IOptions<VNPaySettings> vnPaySettings,
        IOptions<MoMoSettings> moMoSettings,
        ILogger<PaymentService> logger,
        INotificationService notifications,
        ISellerWalletSettlementService sellerWalletSettlement,
        IMemoryCache memoryCache,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _vnPaySettings = vnPaySettings.Value;
        _moMoSettings = moMoSettings.Value;
        _logger = logger;
        _notifications = notifications;
        _sellerWalletSettlement = sellerWalletSettlement;
        _memoryCache = memoryCache;
        _httpClient = httpClientFactory.CreateClient();
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
        vnpay.AddRequestData("vnp_OrderInfo", $"Thanh toán đơn hàng {order.OrderCode}");
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

            var settlement = await _sellerWalletSettlement.CreditSellerForPaidOrderAsync(order, payment);

            await _context.SaveChangesAsync();

            var oid = order.OrderCode;
            await _notifications.PublishAsync(
                order.CustomerId,
                nameof(NotificationType.Payment),
                "Thanh toán thành công",
                $"Đơn #{oid} đã thanh toán thành công. Số tiền: {amount:N0} VND.",
                "Order",
                order.Id,
                queueEmail: true);

            if (settlement is { NetAmount: > 0 })
            {
                await _notifications.PublishAsync(
                    settlement.SellerId,
                    nameof(NotificationType.Payment),
                    "Nhận tiền từ đơn hàng",
                    $"Đơn #{oid}: +{settlement.NetAmount:N0} VND vào ví khả dụng (tiền hàng {settlement.GrossSubtotal:N0} VND, phí sàn {settlement.CommissionPercent}%: {settlement.PlatformFeeAmount:N0} VND).",
                    "Order",
                    order.Id,
                    queueEmail: true);
            }

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

            var oidFail = order.OrderCode;
            await _notifications.PublishAsync(
                order.CustomerId,
                nameof(NotificationType.Payment),
                "Thanh toán không thành công",
                $"Đơn #{oidFail} đã bị hủy do thanh toán thất bại (mã: {responseCode}).",
                "Order",
                order.Id,
                queueEmail: true);

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

    // ── 3. Tạo MoMo Payment URL ──────────────────────────────────────────────
    public async Task<CreatePaymentResponseDto> CreateMoMoPaymentAsync(Guid orderId, Guid customerId)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId);

        if (order == null)
            return new CreatePaymentResponseDto { Success = false, Message = "Đơn hàng không tồn tại" };

        if ((OrderStatus)order.Status != OrderStatus.PendingPayment)
            return new CreatePaymentResponseDto { Success = false, Message = "Đơn hàng không ở trạng thái chờ thanh toán" };

        var existingPaid = await _context.Payments
            .AnyAsync(p => p.OrderId == orderId && p.Status == (short)PaymentStatus.Paid);

        if (existingPaid)
            return new CreatePaymentResponseDto { Success = false, Message = "Đơn hàng đã được thanh toán" };

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Provider = "MOMO",
            Amount = order.Total,
            Currency = "VND",
            Status = (short)PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        var requestId = payment.Id.ToString();
        var momoOrderId = payment.Id.ToString();
        var orderInfo = $"Thanh toán đơn hàng {order.OrderCode}";
        var amount = ((long)order.Total).ToString();
        var extraData = string.Empty;

        var rawSignature = $"accessKey={_moMoSettings.AccessKey}" +
                           $"&amount={amount}" +
                           $"&extraData={extraData}" +
                           $"&ipnUrl={_moMoSettings.NotifyUrl}" +
                           $"&orderId={momoOrderId}" +
                           $"&orderInfo={orderInfo}" +
                           $"&partnerCode={_moMoSettings.PartnerCode}" +
                           $"&redirectUrl={_moMoSettings.ReturnUrl}" +
                           $"&requestId={requestId}" +
                           $"&requestType={_moMoSettings.RequestType}";

        var signature = HmacSHA256(_moMoSettings.SecretKey, rawSignature);

        var requestBody = new
        {
            partnerCode = _moMoSettings.PartnerCode,
            partnerName = "EComViet",
            storeId = "EComVietStore",
            requestId,
            amount,
            orderId = momoOrderId,
            orderInfo,
            redirectUrl = _moMoSettings.ReturnUrl,
            ipnUrl = _moMoSettings.NotifyUrl,
            lang = "vi",
            extraData,
            requestType = _moMoSettings.RequestType,
            signature
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_moMoSettings.ApiUrl, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("[MoMo] Response: {Body}", responseBody);

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (root.TryGetProperty("resultCode", out var resultCodeProp) && resultCodeProp.GetInt32() == 0)
        {
            var payUrl = root.GetProperty("payUrl").GetString();
            _logger.LogInformation("[MoMo] Created payUrl for OrderId: {OrderId}, PaymentId: {PaymentId}", orderId, payment.Id);
            return new CreatePaymentResponseDto
            {
                Success = true,
                PaymentUrl = payUrl,
                PaymentId = payment.Id,
                Message = "Tạo URL thanh toán MoMo thành công"
            };
        }

        var errorMsg = root.TryGetProperty("message", out var msg) ? msg.GetString() : "Lỗi tạo thanh toán MoMo";
        return new CreatePaymentResponseDto { Success = false, Message = errorMsg };
    }

    // ── 4. Xử lý MoMo IPN Callback ───────────────────────────────────────────
    public async Task<MoMoReturnDto> ProcessMoMoIpnAsync(MoMoIpnRequest request)
    {
        _logger.LogInformation("[MoMo IPN] Received: OrderId={OrderId}, ResultCode={Code}", request.OrderId, request.ResultCode);

        var rawSignature = $"accessKey={_moMoSettings.AccessKey}" +
                           $"&amount={request.Amount}" +
                           $"&extraData={request.ExtraData}" +
                           $"&message={request.Message}" +
                           $"&orderId={request.OrderId}" +
                           $"&orderInfo={request.OrderInfo}" +
                           $"&orderType={request.OrderType}" +
                           $"&partnerCode={request.PartnerCode}" +
                           $"&payType={request.PayType}" +
                           $"&requestId={request.RequestId}" +
                           $"&responseTime={request.ResponseTime}" +
                           $"&resultCode={request.ResultCode}" +
                           $"&transId={request.TransId}";

        var expectedSignature = HmacSHA256(_moMoSettings.SecretKey, rawSignature);
        if (expectedSignature != request.Signature)
        {
            _logger.LogWarning("[MoMo IPN] Invalid signature!");
            return new MoMoReturnDto { Success = false, Message = "Chữ ký không hợp lệ", ResultCode = -1 };
        }

        if (!Guid.TryParse(request.OrderId, out var paymentId))
            return new MoMoReturnDto { Success = false, Message = "OrderId không hợp lệ", ResultCode = -1 };

        var payment = await _context.Payments
            .Include(p => p.Order)
                .ThenInclude(o => o.OrderItems)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null)
            return new MoMoReturnDto { Success = false, Message = "Không tìm thấy thanh toán", ResultCode = -1 };

        if (payment.Status == (short)PaymentStatus.Paid)
            return new MoMoReturnDto { Success = true, Message = "Đã xử lý trước đó", OrderId = payment.OrderId, PaymentId = payment.Id, Amount = payment.Amount };

        var order = payment.Order;

        if (request.ResultCode == 0)
        {
            payment.Status = (short)PaymentStatus.Paid;
            payment.PaidAt = DateTime.UtcNow;
            payment.ProviderRef = request.TransId.ToString();

            order.Status = (short)OrderStatus.Confirmed;
            order.UpdatedAt = DateTime.UtcNow;

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

            var momoSettlement = await _sellerWalletSettlement.CreditSellerForPaidOrderAsync(order, payment);

            await _context.SaveChangesAsync();

            var momoOk = order.OrderCode;
            await _notifications.PublishAsync(
                order.CustomerId,
                nameof(NotificationType.Payment),
                "Thanh toán thành công",
                $"Đơn #{momoOk} đã thanh toán MoMo thành công. Số tiền: {payment.Amount:N0} VND.",
                "Order",
                order.Id,
                queueEmail: true);

            if (momoSettlement is { NetAmount: > 0 })
            {
                await _notifications.PublishAsync(
                    momoSettlement.SellerId,
                    nameof(NotificationType.Payment),
                    "Nhận tiền từ đơn hàng",
                    $"Đơn #{momoOk}: +{momoSettlement.NetAmount:N0} VND vào ví khả dụng (tiền hàng {momoSettlement.GrossSubtotal:N0} VND, phí sàn {momoSettlement.CommissionPercent}%: {momoSettlement.PlatformFeeAmount:N0} VND).",
                    "Order",
                    order.Id,
                    queueEmail: true);
            }

            _logger.LogInformation("[MoMo IPN] Payment SUCCESS for OrderId: {OrderId}", order.Id);

            return new MoMoReturnDto { Success = true, Message = "Thanh toán thành công", ResultCode = 0, OrderId = order.Id, PaymentId = payment.Id, Amount = payment.Amount };
        }
        else
        {
            payment.Status = (short)PaymentStatus.Failed;
            payment.PaidAt = DateTime.UtcNow;

            order.Status = (short)OrderStatus.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;

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

            var momoFail = order.OrderCode;
            await _notifications.PublishAsync(
                order.CustomerId,
                nameof(NotificationType.Payment),
                "Thanh toán MoMo không thành công",
                $"Đơn #{momoFail} đã bị hủy do thanh toán thất bại (mã: {request.ResultCode}).",
                "Order",
                order.Id,
                queueEmail: true);

            _logger.LogWarning("[MoMo IPN] Payment FAILED for OrderId: {OrderId}, Code: {Code}", order.Id, request.ResultCode);

            return new MoMoReturnDto { Success = false, Message = request.Message, ResultCode = request.ResultCode, OrderId = order.Id, PaymentId = payment.Id, Amount = payment.Amount };
        }
    }

    private static string HmacSHA256(string key, string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }

}
