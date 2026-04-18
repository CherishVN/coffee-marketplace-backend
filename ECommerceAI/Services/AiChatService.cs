using System.Text.Json;
using ECommerceAI.Data;
using ECommerceAI.Data.Entities;
using ECommerceAI.Data.Entities.ReadOnly;
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

    // ── Tạo session mới (luôn tạo mới, archive session cũ nếu có) ────────────
    public async Task<SessionResponseDto> CreateNewSessionAsync(Guid userId)
    {
        // Đánh dấu tất cả session active cũ là "archived"
        var oldSessions = await _context.AiChatSessions
            .Where(s => s.UserId == userId && s.Status == "active")
            .ToListAsync();

        foreach (var old in oldSessions)
        {
            old.Status = "archived";
            old.UpdatedAt = DateTime.UtcNow;
        }

        var newSession = new AiChatSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.AiChatSessions.Add(newSession);
        await _context.SaveChangesAsync();

        return MapToSessionDto(newSession);
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
        var productsContext = await BuildProductContextAsync(message, session.Messages);
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

        // 6. Nếu AI muốn tìm sản phẩm → search và trả về danh sách (kèm filter giá nếu có)
        List<ProductSuggestionDto> products = new();
        if (!string.IsNullOrEmpty(parsed.SearchQuery))
        {
            var maxPrice = ExtractMaxPrice(message);
            products = await SearchProductsAsync(parsed.SearchQuery, maxPrice);
        }

        // Khi LLM trả search_query kèm mức giá, token hóa có thể sai — thử lại từ khóa sạch từ câu user.
        if (!products.Any() && !string.IsNullOrWhiteSpace(parsed.SearchQuery) && IsLikelyProductRequest(message))
        {
            var cleaned = ExtractProductKeyword(message);
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                products = await SearchProductsAsync(cleaned, ExtractMaxPrice(message));
                if (products.Any()) parsed.SearchQuery = cleaned;
            }
        }

        // Fallback cho trường hợp LLM không trả search_query nhưng user đang hỏi sản phẩm.
        if (!products.Any() && string.IsNullOrWhiteSpace(parsed.SearchQuery) && IsLikelyProductRequest(message))
        {
            var fallbackKeyword = ExtractProductKeyword(message);
            if (!string.IsNullOrWhiteSpace(fallbackKeyword))
            {
                products = await SearchProductsAsync(fallbackKeyword, ExtractMaxPrice(message));
                if (products.Any()) parsed.SearchQuery = fallbackKeyword;
            }
        }

        // Nếu user xin "ảnh mẫu" nhưng LLM không trả search_query ổn định,
        // fallback theo ngữ cảnh đoạn chat trước để FE vẫn có ảnh sản phẩm.
        if (!products.Any() && IsImageRequest(message))
        {
            var fallbackQueries = BuildFallbackQueries(message, session.Messages);
            foreach (var query in fallbackQueries)
            {
                products = await SearchProductsAsync(query, ExtractMaxPrice(message));
                if (products.Any())
                {
                    parsed.SearchQuery = query;
                    break;
                }
            }
        }

        if (products.Any() && IsImageRequest(message))
        {
            parsed.Reply = "Mình đã lấy các mẫu có ảnh bên dưới, bạn tick sản phẩm muốn mua rồi bấm OK giúp mình nhé.";
        }

        if (!products.Any() && IsLikelyProductRequest(message))
        {
            parsed.Reply =
                "Mình chưa tìm thấy sản phẩm phù hợp trong kho local brand hiện tại. Bạn thử mô tả rõ hơn (tên sản phẩm, ngành hàng, mức giá, chất liệu/thuộc tính, khu vực hoặc thương hiệu) để mình lọc chính xác hơn nhé.";
        }

        if (string.IsNullOrWhiteSpace(parsed.Reply))
        {
            parsed.Reply = products.Any()
                ? "Mình đã tìm thấy vài sản phẩm phù hợp cho bạn ở bên dưới."
                : "Mình đã nhận yêu cầu của bạn. Bạn mô tả thêm một chút để mình hỗ trợ chuẩn hơn nhé.";
        }

        // 7. Lưu user message + assistant reply cuối cùng vào DB
        var now = DateTime.UtcNow;
        _context.AiChatMessages.AddRange(
            new AiChatMessage { SessionId = sessionId, Role = "user", Content = message, CreatedAt = now },
            new AiChatMessage { SessionId = sessionId, Role = "assistant", Content = parsed.Reply, CreatedAt = now.AddMilliseconds(1) }
        );

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
    public async Task<ConfirmOrderResponseDto> ConfirmOrderAsync(Guid sessionId, Guid userId, Guid cartId, Guid shippingAddressId, string? accessToken)
    {
        // Gọi Main API để tạo đơn hàng
        try
        {
            var httpClient = _httpClientFactory.CreateClient("MainApi");
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            var payload = JsonSerializer.Serialize(new
            {
                cartId = cartId,
                shippingAddressId = shippingAddressId
            });

            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("/api/cart/checkout", content);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(body);
                Guid? orderId = null;

                if (result.TryGetProperty("data", out var dataNode)
                    && dataNode.TryGetProperty("orderIds", out var orderIdsNode)
                    && orderIdsNode.ValueKind == JsonValueKind.Array
                    && orderIdsNode.GetArrayLength() > 0)
                {
                    orderId = orderIdsNode[0].GetGuid();
                }

                // Lưu AI message thông báo đơn hàng đã tạo
                _context.AiChatMessages.Add(new AiChatMessage
                {
                    SessionId = sessionId,
                    Role = "assistant",
                    Content = orderId.HasValue
                        ? $" Đơn hàng đã được tạo thành công! Mã đơn hàng: {orderId.Value}"
                        : " Đơn hàng đã được tạo thành công!",
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
                    Message = orderId.HasValue
                        ? $"✅ Đơn hàng #{orderId.Value} đã được tạo thành công!"
                        : "✅ Đơn hàng đã được tạo thành công!"
                };
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "Create order failed. Status: {StatusCode}. Body: {Body}",
                response.StatusCode,
                errorBody);

            var fallbackMessage = "Không thể tạo đơn hàng. Vui lòng kiểm tra lại giỏ hàng hoặc địa chỉ giao hàng.";
            string resolvedMessage = fallbackMessage;
            try
            {
                var errorJson = JsonSerializer.Deserialize<JsonElement>(errorBody);
                if (errorJson.TryGetProperty("message", out var messageNode) &&
                    messageNode.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(messageNode.GetString()))
                {
                    resolvedMessage = messageNode.GetString()!;
                }
            }
            catch
            {
                // keep fallback message
            }

            return new ConfirmOrderResponseDto
            {
                Success = false,
                Message = resolvedMessage
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

    /// <summary>Name, category, or tag only — no description (aligns with storefront; avoids SEO keyword spam).</summary>
    private static IQueryable<Product> WhereProductMatchesToken(IQueryable<Product> q, string tokenLower) =>
        q.Where(p =>
            p.Name.ToLower().Contains(tokenLower)
            || (p.Category != null &&
                (p.Category.Name.ToLower().Contains(tokenLower) ||
                 p.Category.Slug.ToLower().Contains(tokenLower)))
            || p.ProductTags.Any(pt =>
                pt.Tag.Name.ToLower().Contains(tokenLower) ||
                pt.Tag.Slug.ToLower().Contains(tokenLower)));

    private async Task<string> BuildProductContextAsync(string userMessage, IEnumerable<AiChatMessage>? history = null)
    {
        var keyword = ExtractProductKeyword(userMessage);
        if (IsImageRequest(userMessage))
        {
            var fallbackKeyword = ExtractLastProductKeywordFromHistory(history);
            if (!string.IsNullOrWhiteSpace(fallbackKeyword))
            {
                keyword = fallbackKeyword;
            }
        }
        if (string.IsNullOrEmpty(keyword)) return string.Empty;

        var maxPrice = ExtractMaxPrice(userMessage);

        var kw = keyword.ToLower();
        var query = WhereProductMatchesToken(
                _context.Products
                    .Include(p => p.Variants)
                    .Include(p => p.Images)
                    .Include(p => p.Category),
                kw)
            .Where(p => p.Status == 1);

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

    private static bool IsImageRequest(string message)
    {
        var lower = message.ToLower();
        return lower.Contains("ảnh") || lower.Contains("hình") || lower.Contains("mẫu");
    }

    private static bool IsLikelyProductRequest(string message)
    {
        var lower = message.ToLower();
        if (string.IsNullOrWhiteSpace(lower)) return false;

        var shoppingSignals = new[]
        {
            "mua", "tìm", "gợi ý", "đề xuất", "cho tôi", "xem", "mẫu", "ảnh",
            "giá", "bao nhiêu", "thương hiệu", "shop", "sản phẩm", "local brand"
        };
        if (shoppingSignals.Any(s => lower.Contains(s))) return true;

        // Fallback đa ngành hàng: nếu câu có ít nhất 2 token "ý nghĩa" thì coi là nhu cầu tìm sản phẩm.
        var genericStopWords = new HashSet<string>
        {
            "tôi", "mình", "anh", "chị", "em", "là", "và", "hoặc", "có", "không", "giúp",
            "với", "nhé", "ạ", "ơi", "nha", "thì", "đi", "được", "được không"
        };
        var tokens = lower
            .Split(new[] { ' ', ',', '.', '?', '!', ':', ';', '/', '\\', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !genericStopWords.Contains(t) && !long.TryParse(t, out _))
            .ToList();

        return tokens.Count >= 2;
    }

    private static string ExtractLastProductKeywordFromHistory(IEnumerable<AiChatMessage>? history)
    {
        if (history == null) return string.Empty;
        var genericWords = new HashSet<string>
        {
            "ảnh", "hình", "mẫu", "xem", "cho", "vài", "các", "sản phẩm", "đó",
            "anh", "em", "nào", "đi", "giúp", "mình", "tôi", "sp"
        };

        foreach (var msg in history
                     .Where(m => m.Role == "user")
                     .OrderByDescending(m => m.CreatedAt))
        {
            var keyword = ExtractProductKeyword(msg.Content ?? string.Empty);
            if (string.IsNullOrWhiteSpace(keyword)) continue;
            if (!genericWords.Contains(keyword.Trim().ToLower()))
            {
                return keyword;
            }
        }

        return string.Empty;
    }

    private static List<string> BuildFallbackQueries(string message, IEnumerable<AiChatMessage>? history)
    {
        var queries = new List<string>();

        var currentKeyword = ExtractProductKeyword(message);
        if (!string.IsNullOrWhiteSpace(currentKeyword))
            queries.Add(currentKeyword);

        if (history != null)
        {
            foreach (var msg in history
                         .Where(m => m.Role == "user")
                         .OrderByDescending(m => m.CreatedAt)
                         .Take(8))
            {
                var keyword = ExtractProductKeyword(msg.Content ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(keyword))
                    queries.Add(keyword);
            }
        }

        return queries
            .Select(q => q.Trim())
            .Where(q => q.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Giá hiển thị trong chat: thấp nhất giữa base và variant đang bán.</summary>
    private static decimal GetMinCustomerFacingPrice(Product p)
    {
        var active = p.Variants?.Where(v => v.IsActive).ToList();
        if (active == null || active.Count == 0)
            return p.BasePrice;
        var minV = active.Min(v => v.Price ?? p.BasePrice);
        return Math.Min(p.BasePrice, minV);
    }

    /// <summary>Token dạng 300k, 1.5tr — không dùng để match tên sản phẩm.</summary>
    private static bool IsPriceLikeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        var t = token.Trim().ToLowerInvariant();
        if (t.Length < 2) return false;
        if (long.TryParse(t, System.Globalization.NumberStyles.Integer, null, out _)) return true;

        string[] suffixes = { "kđ", "nghìn", "ngàn", "triệu", "tr", "k", "đ" };
        foreach (var suf in suffixes)
        {
            if (t.Length <= suf.Length || !t.EndsWith(suf, StringComparison.Ordinal)) continue;
            var head = t[..^suf.Length].Replace(",", ".").Trim();
            if (string.IsNullOrEmpty(head)) continue;
            if (decimal.TryParse(head, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                return true;
        }

        return false;
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
            "anh", "em", "các", "cái", "mẫu", "ảnh", "hình", "xem", "đó", "nào",
            "giúp", "với", "đi", "sản", "phẩm", "sp",
            // Tính từ mô tả không phải tên sản phẩm
            "đẹp", "xấu", "tốt", "rẻ", "mắc", "hot", "mới", "cũ", "ngon", "chất",
            "đỉnh", "xịn", "sang", "trẻ", "hợp", "thời", "thượng", "lưu"
        };

        var words = message.ToLower()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w =>
                !stopWords.Contains(w) &&
                w.Length > 1 &&
                !IsPriceLikeToken(w) &&
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
        var normalizedTokens = NormalizeSearchTokens(query);

        var dbQuery = _context.Products
            .Include(p => p.Variants.Where(v => v.IsActive))
            .Include(p => p.Images)
            .Include(p => p.Category)
            .Where(p => p.Status == 1);

        if (normalizedTokens.Count > 0)
        {
            foreach (var token in normalizedTokens)
                dbQuery = WhereProductMatchesToken(dbQuery, token);
        }
        else
        {
            var lowerQuery = query.ToLower().Trim();
            dbQuery = WhereProductMatchesToken(dbQuery, lowerQuery);
        }

        if (maxPrice.HasValue)
            dbQuery = dbQuery.Where(p =>
                p.BasePrice <= maxPrice.Value ||
                p.Variants.Any(v => v.IsActive && (v.Price ?? p.BasePrice) <= maxPrice.Value));

        var products = await dbQuery.Take(5).ToListAsync();

        return products.Select(p => new ProductSuggestionDto
        {
            Id = p.Id,
            Name = p.Name,
            BasePrice = GetMinCustomerFacingPrice(p),
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

    private static List<string> NormalizeSearchTokens(string query)
    {
        var stopWords = new HashSet<string>
        {
            "đẹp", "xinh", "hot", "trend", "trendy", "new", "mới", "cao", "cấp",
            "xịn", "siêu", "best", "top", "chất", "đỉnh", "sịn", "phiên", "bản",
            "mẫu", "này", "kia", "đó", "vài", "các", "cho", "tôi", "mình", "anh",
            "chị", "em", "muốn", "cần", "mua", "tìm",
            "dưới", "trên", "tầm", "khoảng", "quanh", "tối", "đa", "đến", "lên",
            "max", "min", "under", "below", "above", "around",
            "nghìn", "ngàn", "triệu", "trăm", "đồng", "vnđ", "vnd"
        };

        return query
            .ToLower()
            .Split(new[] { ' ', ',', '.', '?', '!', ':', ';', '/', '\\', '-', '_', '(', ')', '[', ']', '"' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 2)
            .Where(token => !stopWords.Contains(token))
            .Where(token => !IsPriceLikeToken(token))
            .Where(token => !long.TryParse(token, out _)) // loại năm/số như 2026
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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
