using System.Text.Json;
using ECommerceAI.Data;
using ECommerceAI.Data.Entities;
using ECommerceAI.DTOs.Chat;
using ECommerceAI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAI.Services;

public class AiChatService : IAiChatService
{
    private readonly AiDbContext _context;
    private readonly GeminiClientService _gemini;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AiChatService> _logger;
    private readonly string _systemPrompt;

    public AiChatService(
        AiDbContext context,
        GeminiClientService gemini,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<AiChatService> logger)
    {
        _context = context;
        _gemini = gemini;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;

        var promptPath = Path.Combine(AppContext.BaseDirectory, "Prompts", "ChatSystemPrompt.txt");
        _systemPrompt = File.Exists(promptPath) ? File.ReadAllText(promptPath) : "Bạn là trợ lý mua sắm AI.";
    }

    // ── Tạo hoặc lấy session hiện có ────────────────────────────────────────
    public async Task<SessionResponseDto> GetOrCreateSessionAsync(Guid userId)
    {
        var session = await _context.AiChatSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == "active");

        if (session == null)
        {
            session = new AiChatSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Status = "active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.AiChatSessions.Add(session);
            await _context.SaveChangesAsync();
        }

        return MapToSessionDto(session);
    }

    // ── Gửi tin nhắn và nhận phản hồi AI ────────────────────────────────────
    public async Task<SendMessageResponseDto> SendMessageAsync(Guid sessionId, Guid userId, string message)
    {
        // 1. Load session + history
        var session = await _context.AiChatSessions
            .Include(s => s.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId)
            ?? throw new KeyNotFoundException("Không tìm thấy session chat");

        // 2. Build history cho Gemini
        var history = new List<(string Role, string Text)>();

        foreach (var msg in session.Messages)
        {
            if (msg.Role == "user")
                history.Add(("user", msg.Content));
            else if (msg.Role == "assistant")
                history.Add(("model", msg.Content));
        }

        // 3. Nếu là yêu cầu tìm sản phẩm, lấy danh sách products để inject vào context
        var productsContext = await BuildProductContextAsync(message);
        var userMessageWithContext = string.IsNullOrEmpty(productsContext)
            ? message
            : $"{message}\n\n[Danh sách sản phẩm có sẵn trong hệ thống:\n{productsContext}]";

        history.Add(("user", userMessageWithContext));

        // 4. Gọi LLM
        string rawResponse;
        try
        {
            rawResponse = await _gemini.ChatAsync(_systemPrompt, history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini call failed for session {SessionId}", sessionId);
            return new SendMessageResponseDto
            {
                Reply = "Xin lỗi, tôi đang gặp sự cố kỹ thuật. Vui lòng thử lại sau.",
                Intent = "error",
                SessionId = sessionId
            };
        }

        // 5. Parse JSON response từ LLM
        var parsed = ParseLlmResponse(rawResponse);

        // 6. Lưu user message + assistant reply vào DB
        var now = DateTime.UtcNow;
        _context.AiChatMessages.AddRange(
            new AiChatMessage { SessionId = sessionId, Role = "user", Content = message, CreatedAt = now },
            new AiChatMessage { SessionId = sessionId, Role = "assistant", Content = parsed.Reply, CreatedAt = now.AddMilliseconds(1) }
        );

        // 7. Nếu AI muốn tìm sản phẩm → search và trả về danh sách (kèm filter giá nếu có)
        List<ProductSuggestionDto> products = new();
        if (!string.IsNullOrEmpty(parsed.SearchQuery))
        {
            var maxPrice = ExtractMaxPrice(message);
            products = await SearchProductsAsync(parsed.SearchQuery, maxPrice);
        }

        // 8. Update session timestamp
        session.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new SendMessageResponseDto
        {
            Reply = parsed.Reply,
            Intent = parsed.Intent,
            Products = products,
            NeedsConfirmation = parsed.NeedsConfirmation,
            CartUpdated = false,
            SessionId = sessionId,
            // Trả về productToAdd để frontend tự gọi Main API add-to-cart
            // rồi lưu cartId, sau đó truyền vào confirm-order
            ProductToAdd = parsed.ProductToAdd
        };
    }

    // ── Xác nhận tạo đơn hàng ───────────────────────────────────────────────
    public async Task<ConfirmOrderResponseDto> ConfirmOrderAsync(Guid sessionId, Guid userId, Guid cartId, Guid shippingAddressId)
    {
        // Gọi Main API để tạo đơn hàng
        try
        {
            var httpClient = _httpClientFactory.CreateClient("MainApi");
            var payload = JsonSerializer.Serialize(new
            {
                cartId = cartId,
                shippingAddressId = shippingAddressId
            });

            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("/api/orders/checkout", content);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(body);
                var orderId = result.GetProperty("orderId").GetGuid();

                // Lưu AI message thông báo đơn hàng đã tạo
                _context.AiChatMessages.Add(new AiChatMessage
                {
                    SessionId = sessionId,
                    Role = "assistant",
                    Content = $"✅ Đơn hàng đã được tạo thành công! Mã đơn hàng: {orderId}",
                    CreatedAt = DateTime.UtcNow
                });

                // Đóng session
                var session = await _context.AiChatSessions.FindAsync(sessionId);
                if (session != null)
                {
                    session.Status = "completed";
                    session.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return new ConfirmOrderResponseDto
                {
                    Success = true,
                    OrderId = orderId,
                    Message = $"✅ Đơn hàng #{orderId} đã được tạo thành công!"
                };
            }

            return new ConfirmOrderResponseDto
            {
                Success = false,
                Message = "Không thể tạo đơn hàng. Vui lòng thử lại hoặc tạo đơn thủ công."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create order for session {SessionId}", sessionId);
            return new ConfirmOrderResponseDto
            {
                Success = false,
                Message = "Có lỗi xảy ra khi tạo đơn hàng. Vui lòng thử lại."
            };
        }
    }

    // ── Lấy lịch sử chat ────────────────────────────────────────────────────
    public async Task<SessionResponseDto> GetHistoryAsync(Guid sessionId, Guid userId)
    {
        var session = await _context.AiChatSessions
            .Include(s => s.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId)
            ?? throw new KeyNotFoundException("Không tìm thấy session chat");

        return MapToSessionDto(session);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<string> BuildProductContextAsync(string userMessage)
    {
        var keyword = ExtractProductKeyword(userMessage);
        if (string.IsNullOrEmpty(keyword)) return string.Empty;

        var maxPrice = ExtractMaxPrice(userMessage);

        var query = _context.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .Include(p => p.Category)
            .Where(p => p.Status == 1 &&
                        (p.Name.ToLower().Contains(keyword.ToLower()) ||
                         (p.Description != null && p.Description.ToLower().Contains(keyword.ToLower()))));

        if (maxPrice.HasValue)
            query = query.Where(p =>
                p.BasePrice <= maxPrice.Value ||
                p.Variants.Any(v => v.IsActive && (v.Price ?? p.BasePrice) <= maxPrice.Value));

        var products = await query.Take(10).ToListAsync();
        if (!products.Any()) return string.Empty;

        return string.Join("\n", products.Select(p =>
        {
            var price = p.Variants.Any() ? p.Variants.Min(v => v.Price ?? p.BasePrice) : p.BasePrice;
            var imageUrl = p.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.ImageUrl ?? "";
            var variants = string.Join(", ", p.Variants.Where(v => v.IsActive).Select(v => $"{v.VariantName}({v.Id})"));
            return $"ID:{p.Id} | Tên:{p.Name} | Giá:{price:N0}đ | Ảnh:{imageUrl} | Variants:[{variants}]";
        }));
    }

    /// <summary>
    /// Trích xuất từ khóa sản phẩm từ message, bỏ qua số và từ liên quan đến giá.
    /// Ví dụ: "tôi muốn mua 1 áo thun dưới 180 nghìn" → "áo thun"
    /// </summary>
    private static string ExtractProductKeyword(string message)
    {
        // Từ dừng chung + tính từ mô tả không phải tên sản phẩm + năm/số năm
        var stopWords = new HashSet<string>
        {
            "tôi", "cần", "muốn", "mua", "tìm", "cho", "và", "hoặc", "có", "không",
            "ạ", "nhé", "thôi", "dưới", "trên", "khoảng", "tầm", "giá", "nghìn",
            "ngàn", "trăm", "triệu", "đồng", "vnđ", "vnd", "đ",
            // Tính từ mô tả không phải tên sản phẩm
            "đẹp", "xấu", "tốt", "rẻ", "mắc", "hot", "mới", "cũ", "ngon", "chất",
            "đỉnh", "xịn", "sang", "trẻ", "hợp", "thời", "thượng", "lưu"
        };

        var words = message.ToLower()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w =>
                !stopWords.Contains(w) &&
                w.Length > 1 &&
                !long.TryParse(w, out _) &&
                !(w.Length == 4 && w.StartsWith("20"))) // lọc năm như 2024, 2025, 2026
            .ToArray();

        // Ghép tất cả từ còn lại thành cụm từ tìm kiếm (không giới hạn 2 từ)
        return string.Join(" ", words);
    }

    /// <summary>
    /// Trích xuất giá tối đa từ message.
    /// Ví dụ: "dưới 180 nghìn" → 180000, "dưới 1 triệu" → 1000000
    /// </summary>
    private static decimal? ExtractMaxPrice(string message)
    {
        var lower = message.ToLower();

        // Các pattern: "dưới X nghìn/ngàn", "dưới X triệu", "dưới Xk", "tầm X"
        var pricePattern = new System.Text.RegularExpressions.Regex(
            @"(?:dưới|tầm|khoảng|tối đa)\s+(\d+(?:[,\.]\d+)?)\s*(nghìn|ngàn|triệu|k\b|tr\b)?");

        var match = pricePattern.Match(lower);
        if (!match.Success) return null;

        if (!decimal.TryParse(match.Groups[1].Value.Replace(",", "").Replace(".", ""), out var amount))
            return null;

        var unit = match.Groups[2].Value.Trim();
        return unit switch
        {
            "triệu" or "tr" => amount * 1_000_000,
            "nghìn" or "ngàn" or "k" => amount * 1_000,
            _ => amount >= 1000 ? amount : amount * 1_000
        };
    }

    private async Task<List<ProductSuggestionDto>> SearchProductsAsync(string query, decimal? maxPrice = null)
    {
        var dbQuery = _context.Products
            .Include(p => p.Variants.Where(v => v.IsActive))
            .Include(p => p.Images)
            .Include(p => p.Category)
            .Where(p => p.Status == 1 &&
                        (p.Name.ToLower().Contains(query.ToLower()) ||
                         (p.Description != null && p.Description.ToLower().Contains(query.ToLower()))));

        if (maxPrice.HasValue)
            dbQuery = dbQuery.Where(p =>
                p.BasePrice <= maxPrice.Value ||
                p.Variants.Any(v => v.IsActive && (v.Price ?? p.BasePrice) <= maxPrice.Value));

        var products = await dbQuery.Take(5).ToListAsync();

        return products.Select(p => new ProductSuggestionDto
        {
            Id = p.Id,
            Name = p.Name,
            BasePrice = p.BasePrice,
            ImageUrl = p.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.ImageUrl,
            CategoryName = p.Category?.Name,
            Variants = p.Variants.Select(v => new VariantSuggestionDto
            {
                Id = v.Id,
                VariantName = v.VariantName,
                Price = v.Price
            }).ToList()
        }).ToList();
    }

    private static LlmParsedResponse ParseLlmResponse(string raw)
    {
        try
        {
            // Làm sạch response (xóa markdown code block nếu có)
            var json = raw.Trim();
            if (json.StartsWith("```json")) json = json[7..];
            if (json.StartsWith("```")) json = json[3..];
            if (json.EndsWith("```")) json = json[..^3];
            json = json.Trim();

            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Parse product_to_add nếu có
            ProductToAddDto? productToAdd = null;
            if (root.TryGetProperty("product_to_add", out var pta) && pta.ValueKind == JsonValueKind.Object)
            {
                productToAdd = new ProductToAddDto
                {
                    ProductId = pta.TryGetProperty("product_id", out var pid) && pid.ValueKind == JsonValueKind.String
                        ? (Guid.TryParse(pid.GetString(), out var g1) ? g1 : null) : null,
                    VariantId = pta.TryGetProperty("variant_id", out var vid) && vid.ValueKind == JsonValueKind.String
                        ? (Guid.TryParse(vid.GetString(), out var g2) ? g2 : null) : null,
                    Quantity = pta.TryGetProperty("quantity", out var qty) ? qty.GetInt32() : 1
                };
            }

            return new LlmParsedResponse
            {
                Reply = root.TryGetProperty("reply", out var reply) ? reply.GetString() ?? "" : raw,
                Intent = root.TryGetProperty("intent", out var intent) ? intent.GetString() ?? "general" : "general",
                SearchQuery = root.TryGetProperty("search_query", out var sq) ? sq.GetString() : null,
                NeedsConfirmation = root.TryGetProperty("needs_confirmation", out var nc) && nc.GetBoolean(),
                ProductToAdd = productToAdd
            };
        }
        catch
        {
            // Nếu LLM không trả về JSON hợp lệ, dùng raw text
            return new LlmParsedResponse { Reply = raw, Intent = "general" };
        }
    }

    private static SessionResponseDto MapToSessionDto(AiChatSession session)
    {
        return new SessionResponseDto
        {
            SessionId = session.Id,
            Status = session.Status,
            History = session.Messages.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt
            }).ToList()
        };
    }

    private class LlmParsedResponse
    {
        public string Reply { get; set; } = "";
        public string Intent { get; set; } = "general";
        public string? SearchQuery { get; set; }
        public bool NeedsConfirmation { get; set; }
        public ProductToAddDto? ProductToAdd { get; set; }
    }
}
