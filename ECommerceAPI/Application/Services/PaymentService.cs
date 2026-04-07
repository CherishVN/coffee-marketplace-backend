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
    private static readonly TimeSpan PaymentCreationRetryWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PendingPaymentTimeout = TimeSpan.FromMinutes(100);
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
        _httpClient = httpClientFactory.CreateClient("MoMoGateway");
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

        var lockedProvider = await GetLockedProviderForOrderAsync(orderId);
        if (!string.IsNullOrWhiteSpace(lockedProvider)
            && !string.Equals(lockedProvider, "VNPAY", StringComparison.OrdinalIgnoreCase))
        {
            return new CreatePaymentResponseDto
            {
                Success = false,
                Message = $"Đơn hàng đã chọn cổng {lockedProvider}. Vui lòng thanh toán lại đúng phương thức đã chọn ở checkout."
            };
        }

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
                OrderCode = order.OrderCode,
                PaymentId = payment.Id,
                Amount = amount
            };
        }
        else
        {
            // ── THANH TOÁN CHƯA HOÀN TẤT ────────────────────────────────────
            payment.Status = (short)PaymentStatus.Failed;
            payment.PaidAt = DateTime.UtcNow;
            payment.ProviderRef = transactionNo;

            // Giữ đơn ở trạng thái chờ thanh toán để khách có thể thử lại.
            order.Status = (short)OrderStatus.PendingPayment;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var oidFail = order.OrderCode;
            await _notifications.PublishAsync(
                order.CustomerId,
                nameof(NotificationType.Payment),
                "Thanh toán chưa hoàn tất",
                $"Đơn #{oidFail} chưa thanh toán thành công (mã: {responseCode}). Bạn có thể thử lại trong vòng {PendingPaymentTimeout.TotalMinutes:0} phút.",
                "Order",
                order.Id,
                queueEmail: true);

            _logger.LogWarning("[VNPay Return] Payment NOT completed for OrderId: {OrderId}, Code: {Code}", order.Id, responseCode);

            return new VNPayReturnDto
            {
                Success = false,
                Message = $"Thanh toán chưa hoàn tất. Mã: {responseCode}",
                ResponseCode = responseCode,
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                PaymentId = payment.Id,
                Amount = amount
            };
        }
    }

    public async Task<CreatePaymentResponseDto> CreateMoMoPaymentAsync(Guid orderId, Guid customerId)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId);

        if (order == null)
            return new CreatePaymentResponseDto { Success = false, Message = "Đơn hàng không tồn tại" };

        if ((OrderStatus)order.Status != OrderStatus.PendingPayment)
            return new CreatePaymentResponseDto { Success = false, Message = "Đơn hàng không ở trạng thái chờ thanh toán" };

        var lockedProvider = await GetLockedProviderForOrderAsync(orderId);
        if (!string.IsNullOrWhiteSpace(lockedProvider)
            && !string.Equals(lockedProvider, "MOMO", StringComparison.OrdinalIgnoreCase))
        {
            return new CreatePaymentResponseDto
            {
                Success = false,
                Message = $"Đơn hàng đã chọn cổng {lockedProvider}. Vui lòng thanh toán lại đúng phương thức đã chọn ở checkout."
            };
        }

        var existingPaid = await _context.Payments
            .AnyAsync(p => p.OrderId == orderId && p.Status == (short)PaymentStatus.Paid);

        if (existingPaid)
            return new CreatePaymentResponseDto { Success = false, Message = "Đơn hàng đã được thanh toán" };

        var threshold = DateTime.UtcNow.Subtract(PaymentCreationRetryWindow);
        var latestPending = await _context.Payments
            .Where(p => p.OrderId == orderId
                        && p.Provider == "MOMO"
                        && p.Status == (short)PaymentStatus.Pending
                        && p.CreatedAt >= threshold)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        if (latestPending != null)
        {
            return new CreatePaymentResponseDto
            {
                Success = false,
                Message = "Yêu cầu thanh toán đang được xử lý, vui lòng thử lại sau vài giây."
            };
        }

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

        // Phase 2: gọi MoMo bên ngoài DB transaction.
        string responseBody;
        try
        {
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(_moMoSettings.ApiUrl, content);
            responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var httpError = $"MoMo trả về HTTP {(int)response.StatusCode}";
                await MarkPaymentCreationFailedAsync(payment.Id, httpError);
                return new CreatePaymentResponseDto { Success = false, Message = httpError };
            }
        }
        catch (TaskCanceledException)
        {
            var timeoutMessage = "Kết nối MoMo bị timeout, vui lòng thử lại.";
            await MarkPaymentCreationFailedAsync(payment.Id, timeoutMessage);
            return new CreatePaymentResponseDto { Success = false, Message = timeoutMessage };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MoMo] Create payment call failed for OrderId: {OrderId}", orderId);
            await MarkPaymentCreationFailedAsync(payment.Id, "Không thể kết nối MoMo");
            return new CreatePaymentResponseDto { Success = false, Message = "Không thể kết nối MoMo, vui lòng thử lại." };
        }

        _logger.LogInformation("[MoMo] Response: {Body}", responseBody);

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            await MarkPaymentCreationFailedAsync(payment.Id, "Phản hồi MoMo không hợp lệ");
            return new CreatePaymentResponseDto { Success = false, Message = "Phản hồi từ MoMo không hợp lệ." };
        }

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
        await MarkPaymentCreationFailedAsync(payment.Id, errorMsg ?? "Lỗi tạo thanh toán MoMo");
        return new CreatePaymentResponseDto { Success = false, Message = errorMsg };
    }

    private async Task MarkPaymentCreationFailedAsync(Guid paymentId, string reason)
    {
        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.Id == paymentId);
        if (payment == null || payment.Status != (short)PaymentStatus.Pending)
            return;

        payment.Status = (short)PaymentStatus.Failed;
        payment.PaidAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogWarning("[MoMo] Marked payment {PaymentId} as failed. Reason: {Reason}", paymentId, reason);
    }

    // ── 4. Xử lý MoMo IPN Callback ───────────────────────────────────────────
    public async Task<MoMoReturnDto> ProcessMoMoIpnAsync(MoMoIpnRequest request)
    {
        _logger.LogInformation("[MoMo IPN] Received: OrderId={OrderId}, ResultCode={Code}", request.OrderId, request.ResultCode);

        if (!IsMoMoSignatureValid(request))
        {
            _logger.LogWarning("[MoMo IPN] Invalid signature!");
            return new MoMoReturnDto { Success = false, Message = "Chữ ký không hợp lệ", ResultCode = -1 };
        }

        if (!TryParseMoMoPaymentId(request.OrderId, out var paymentId))
            return new MoMoReturnDto { Success = false, Message = "OrderId không hợp lệ", ResultCode = -1 };

        return await HandleMoMoPaymentStateAsync(
            paymentId,
            request.ResultCode,
            request.Message,
            request.TransId > 0 ? request.TransId.ToString() : null);
    }

    public async Task<MoMoReturnDto> ProcessMoMoReturnAsync(IQueryCollection queryParams)
    {
        var orderId = queryParams["orderId"].ToString();
        var message = queryParams["message"].ToString();
        var resultCodeRaw = queryParams["resultCode"].ToString();

        var resultCode = int.TryParse(resultCodeRaw, out var parsedResultCode) ? parsedResultCode : -1;

        _logger.LogInformation("[MoMo Return] Received: OrderId={OrderId}, ResultCode={Code}", orderId, resultCode);

        if (!TryParseMoMoPaymentId(orderId, out var paymentId))
            return new MoMoReturnDto { Success = false, Message = "OrderId không hợp lệ", ResultCode = -1 };

        var returnRequest = BuildMoMoRequestFromQuery(queryParams);

        if (returnRequest is not null && !string.IsNullOrWhiteSpace(returnRequest.Signature))
        {
            if (!IsMoMoSignatureValid(returnRequest))
            {
                _logger.LogWarning("[MoMo Return] Invalid signature for OrderId={OrderId}", orderId);
                return new MoMoReturnDto { Success = false, Message = "Chữ ký không hợp lệ", ResultCode = -1 };
            }

            return await HandleMoMoPaymentStateAsync(
                paymentId,
                returnRequest.ResultCode,
                returnRequest.Message,
                returnRequest.TransId > 0 ? returnRequest.TransId.ToString() : null);
        }

        // Fallback cho môi trường local khi NotifyUrl/IPN không gọi được từ MoMo.
        _logger.LogWarning("[MoMo Return] Missing signature payload, fallback process by return params for PaymentId={PaymentId}", paymentId);

        return await HandleMoMoPaymentStateAsync(
            paymentId,
            resultCode,
            message,
            queryParams["transId"].ToString());
    }

    private async Task<MoMoReturnDto> HandleMoMoPaymentStateAsync(Guid paymentId, int resultCode, string? message, string? providerRef)
    {
        var payment = await _context.Payments
            .Include(p => p.Order)
                .ThenInclude(o => o.OrderItems)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null)
            return new MoMoReturnDto { Success = false, Message = "Không tìm thấy thanh toán", ResultCode = -1 };

        if (payment.Status == (short)PaymentStatus.Paid)
        {
            return new MoMoReturnDto
            {
                Success = true,
                Message = "Đã xử lý trước đó",
                ResultCode = 0,
                OrderId = payment.OrderId,
                OrderCode = payment.Order.OrderCode,
                PaymentId = payment.Id,
                Amount = payment.Amount
            };
        }

        if (payment.Status != (short)PaymentStatus.Pending)
        {
            return new MoMoReturnDto
            {
                Success = false,
                Message = "Giao dịch đã được xử lý ở trạng thái khác",
                ResultCode = resultCode,
                OrderId = payment.OrderId,
                OrderCode = payment.Order.OrderCode,
                PaymentId = payment.Id,
                Amount = payment.Amount
            };
        }

        var order = payment.Order;

        if (resultCode == 0)
        {
            payment.Status = (short)PaymentStatus.Paid;
            payment.PaidAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(providerRef))
            {
                payment.ProviderRef = providerRef;
            }

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

            _logger.LogInformation("[MoMo] Payment SUCCESS for OrderId: {OrderId}", order.Id);

            return new MoMoReturnDto
            {
                Success = true,
                Message = "Thanh toán thành công",
                ResultCode = 0,
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                PaymentId = payment.Id,
                Amount = payment.Amount
            };
        }

        payment.Status = (short)PaymentStatus.Failed;
        payment.PaidAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(providerRef))
        {
            payment.ProviderRef = providerRef;
        }

        // Giữ đơn ở trạng thái chờ thanh toán để khách có thể thử lại.
        order.Status = (short)OrderStatus.PendingPayment;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var momoFail = order.OrderCode;
        await _notifications.PublishAsync(
            order.CustomerId,
            nameof(NotificationType.Payment),
            "Thanh toán MoMo chưa hoàn tất",
            $"Đơn #{momoFail} chưa thanh toán thành công (mã: {resultCode}). Bạn có thể thử lại trong vòng {PendingPaymentTimeout.TotalMinutes:0} phút.",
            "Order",
            order.Id,
            queueEmail: true);

        _logger.LogWarning("[MoMo] Payment NOT completed for OrderId: {OrderId}, Code: {Code}", order.Id, resultCode);

        return new MoMoReturnDto
        {
            Success = false,
            Message = string.IsNullOrWhiteSpace(message) ? "Thanh toán chưa hoàn tất" : message,
            ResultCode = resultCode,
            OrderId = order.Id,
            OrderCode = order.OrderCode,
            PaymentId = payment.Id,
            Amount = payment.Amount
        };
    }

    public async Task<int> ExpireStalePendingPaymentsAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.Subtract(PendingPaymentTimeout);

        var staleOrderIds = await _context.Orders
            .Where(o => o.Status == (short)OrderStatus.PendingPayment)
            .Where(o => o.Payments.Any(p => p.Status == (short)PaymentStatus.Pending && p.CreatedAt <= cutoff))
            .Where(o => !o.Payments.Any(p => p.Status == (short)PaymentStatus.Pending && p.CreatedAt > cutoff))
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        if (staleOrderIds.Count == 0)
            return 0;

        var expiredCount = 0;

        foreach (var orderId in staleOrderIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order == null || order.Status != (short)OrderStatus.PendingPayment)
                continue;

            var hasAnyPaidPayment = order.Payments.Any(p => p.Status == (short)PaymentStatus.Paid);
            if (hasAnyPaidPayment)
                continue;

            var pendingPayments = order.Payments
                .Where(p => p.Status == (short)PaymentStatus.Pending)
                .ToList();

            if (pendingPayments.Count == 0)
                continue;

            var hasActivePending = pendingPayments.Any(p => p.CreatedAt > cutoff);
            if (hasActivePending)
                continue;

            var now = DateTime.UtcNow;

            foreach (var payment in pendingPayments)
            {
                payment.Status = (short)PaymentStatus.Cancelled;
                payment.PaidAt = now;
            }

            order.Status = (short)OrderStatus.Cancelled;
            order.CancelReason = $"Hết hạn thanh toán sau {PendingPaymentTimeout.TotalMinutes:0} phút";
            order.UpdatedAt = now;

            foreach (var item in order.OrderItems)
            {
                var inv = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.VariantId == item.VariantId, cancellationToken);

                if (inv != null)
                {
                    inv.ReservedQuantity -= item.Quantity;
                    if (inv.ReservedQuantity < 0) inv.ReservedQuantity = 0;
                    inv.UpdatedAt = now;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            var orderCode = order.OrderCode;
            await _notifications.PublishAsync(
                order.CustomerId,
                nameof(NotificationType.Payment),
                "Đơn hàng đã hết hạn thanh toán",
                $"Đơn #{orderCode} đã tự động hủy do quá thời gian thanh toán {PendingPaymentTimeout.TotalMinutes:0} phút.",
                "Order",
                order.Id,
                queueEmail: true);

            expiredCount++;
        }

        if (expiredCount > 0)
        {
            _logger.LogInformation("[Payment Timeout] Expired {Count} stale pending order(s)", expiredCount);
        }

        return expiredCount;
    }

    private bool IsMoMoSignatureValid(MoMoIpnRequest request)
    {
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
        return string.Equals(expectedSignature, request.Signature, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryParseMoMoPaymentId(string? orderId, out Guid paymentId)
    {
        return Guid.TryParse(orderId, out paymentId);
    }

    private MoMoIpnRequest? BuildMoMoRequestFromQuery(IQueryCollection queryParams)
    {
        var orderId = queryParams["orderId"].ToString();
        if (string.IsNullOrWhiteSpace(orderId))
            return null;

        var resultCode = int.TryParse(queryParams["resultCode"], out var parsedResultCode)
            ? parsedResultCode
            : -1;

        var amount = long.TryParse(queryParams["amount"], out var parsedAmount)
            ? parsedAmount
            : 0;

        var transId = long.TryParse(queryParams["transId"], out var parsedTransId)
            ? parsedTransId
            : 0;

        var responseTime = long.TryParse(queryParams["responseTime"], out var parsedResponseTime)
            ? parsedResponseTime
            : 0;

        return new MoMoIpnRequest
        {
            PartnerCode = string.IsNullOrWhiteSpace(queryParams["partnerCode"])
                ? _moMoSettings.PartnerCode
                : queryParams["partnerCode"].ToString(),
            OrderId = orderId,
            RequestId = queryParams["requestId"].ToString(),
            Amount = amount,
            OrderInfo = queryParams["orderInfo"].ToString(),
            OrderType = queryParams["orderType"].ToString(),
            TransId = transId,
            ResultCode = resultCode,
            Message = queryParams["message"].ToString(),
            PayType = queryParams["payType"].ToString(),
            ResponseTime = responseTime,
            ExtraData = queryParams["extraData"].ToString(),
            Signature = queryParams["signature"].ToString()
        };
    }

    private static string HmacSHA256(string key, string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }

    private async Task<string?> GetLockedProviderForOrderAsync(Guid orderId)
    {
        return await _context.Payments
            .Where(p => p.OrderId == orderId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => p.Provider)
            .FirstOrDefaultAsync();
    }

}
