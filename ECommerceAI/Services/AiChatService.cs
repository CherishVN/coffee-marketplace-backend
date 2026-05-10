using System.Text.Json;
using System.Text.Json.Serialization;
using ECommerceAI.Data;
using ECommerceAI.Data.Entities;
using ECommerceAI.Data.Entities.ReadOnly;
using ECommerceAI.DTOs.Chat;
using ECommerceAI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceAI.Services;

public class AiChatService : IAiChatService
{
    private static readonly JsonSerializerOptions ProductJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AiDbContext _context;
    private readonly GeminiClientService _gemini;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AiChatService> _logger;
    private readonly string _systemPrompt;

    public AiChatService(
        AiDbContext context,
        [FromKeyedServices("default")] GeminiClientService gemini,
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
        // Chỉ query DB khi message có khả năng liên quan sản phẩm — tránh query thừa với "xin chào", "cảm ơn" v.v.
        var productsContext = (IsLikelyProductRequest(message) || IsImageRequest(message))
            ? await BuildProductContextAsync(message, session.Messages)
            : string.Empty;
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

        // Khách trả lời tiếp sau khi đã có thẻ SP (chọn phân loại / làm rõ) — câu ngắn thường không khớp AND search; khớp lại từ JSON gợi ý vừa lưu.
        if (!products.Any())
        {
            var fromSession = TryMatchRecentSuggestedProducts(message, session.Messages);
            if (fromSession.Count > 0)
            {
                products = fromSession;
                if (string.IsNullOrWhiteSpace(parsed.SearchQuery))
                    parsed.SearchQuery = ExtractProductKeyword(message);
            }
        }

        if (products.Any() && IsImageRequest(message))
        {
            parsed.Reply = "Mình đã lấy các mẫu có ảnh bên dưới, bạn tick sản phẩm muốn mua rồi bấm OK giúp mình nhé.";
        }

        if (!products.Any() && IsLikelyProductRequest(message) && !IsCheckoutOrOrderOnlyMessage(message))
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

        ApplyMultiVariantSelectionGate(message, products, parsed);

        // 7. Persist user + assistant rows; assistant row stores suggested products JSON for history UI
        var now = DateTime.UtcNow;
        var assistantRow = new AiChatMessage
        {
            SessionId = sessionId,
            Role = "assistant",
            Content = parsed.Reply,
            CreatedAt = now.AddMilliseconds(1),
            SuggestedProductsJson = products.Count > 0
                ? JsonSerializer.Serialize(products, ProductJsonOptions)
                : null
        };
        _context.AiChatMessages.AddRange(
            new AiChatMessage { SessionId = sessionId, Role = "user", Content = message, CreatedAt = now },
            assistantRow
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
    public async Task<ConfirmOrderResponseDto> ConfirmOrderAsync(
        Guid sessionId,
        Guid userId,
        Guid cartId,
        Guid shippingAddressId,
        IReadOnlyList<AiShopShippingOptionDto>? shippingOptions,
        string? accessToken)
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

            // Main API yêu cầu `shippingOptions` (phí GHN từng shop) để tạo đơn — AI
            // service không tự gọi GHN nên client phải tính trước & truyền vào.
            var payload = JsonSerializer.Serialize(
                new
                {
                    cartId = cartId,
                    shippingAddressId = shippingAddressId,
                    shippingOptions = shippingOptions?
                        .Select(o => new
                        {
                            shopId = o.ShopId,
                            shippingProvider = o.ShippingProvider,
                            shippingServiceId = o.ShippingServiceId,
                            shippingFee = o.ShippingFee,
                            estimatedDeliveryDate = o.EstimatedDeliveryDate
                        })
                        .ToList()
                },
                ProductJsonOptions);

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

    /// <summary>Name, category, tag, hoặc tên biến thể (size/hương vị…) — không dùng mô tả dài.</summary>
    private static IQueryable<Product> WhereProductMatchesToken(IQueryable<Product> q, string tokenLower) =>
        q.Where(p =>
            p.Name.ToLower().Contains(tokenLower)
            || (p.Category != null &&
                (p.Category.Name.ToLower().Contains(tokenLower) ||
                 p.Category.Slug.ToLower().Contains(tokenLower)))
            || p.ProductTags.Any(pt =>
                pt.Tag.Name.ToLower().Contains(tokenLower) ||
                pt.Tag.Slug.ToLower().Contains(tokenLower))
            || p.Variants.Any(v => v.IsActive && v.VariantName.ToLower().Contains(tokenLower)));

    /// <summary>
    /// Token quá rộng: OR trên các token này khiến trà/túi… (danh mục chứa "đặc sản") lọt vào câu hỏi bánh kẹo.
    /// </summary>
    private static readonly HashSet<string> OverlyBroadSearchTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "đặc", "sản", "dac", "san"
    };

    private static List<string> ToMeaningfulSearchTokens(IReadOnlyList<string> tokens) =>
        tokens.Where(t => t.Length >= 2 && !OverlyBroadSearchTokens.Contains(t)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static IQueryable<Product> ApplySearchTokensToQuery(
        IQueryable<Product> dbQuery,
        IReadOnlyList<string> normalizedTokens,
        string rawQuery)
    {
        var meaningful = ToMeaningfulSearchTokens(normalizedTokens);

        // ≥2 token cụ thể: AND — SP phải khớp tất cả (tên/danh mục/tag/biến thể), tránh lẫn ngành hàng.
        if (meaningful.Count >= 2)
        {
            foreach (var token in meaningful.Take(8))
                dbQuery = WhereProductMatchesToken(dbQuery, token);
            return dbQuery;
        }

        if (meaningful.Count == 1)
            return WhereProductMatchesToken(dbQuery, meaningful[0]);

        // Hết token (vd. chỉ còn "đặc"/"sản" đã bị lọc): khớp cả cụm search_query
        var phrase = rawQuery.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(phrase) ? dbQuery : WhereProductMatchesToken(dbQuery, phrase);
    }

    /// <summary>Sản phẩm đang bán + ảnh/danh mục/biến thể, lọc theo cùng quy tắc token như <see cref="SearchProductsAsync"/>.</summary>
    private IQueryable<Product> GetFilteredProductsQuery(string searchText)
    {
        var dbQuery = _context.Products
            .Include(p => p.Variants.Where(v => v.IsActive))
            .Include(p => p.Images)
            .Include(p => p.Category)
            .Where(p => p.Status == 1);

        var normalizedTokens = NormalizeSearchTokens(searchText);
        if (normalizedTokens.Count > 0)
            return ApplySearchTokensToQuery(dbQuery, normalizedTokens, searchText);

        var phrase = searchText.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(phrase) ? dbQuery : WhereProductMatchesToken(dbQuery, phrase);
    }

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

        var query = GetFilteredProductsQuery(keyword);

        if (maxPrice.HasValue)
            query = query.Where(p =>
                p.BasePrice <= maxPrice.Value ||
                p.Variants.Any(v => v.IsActive && (v.Price ?? p.BasePrice) <= maxPrice.Value));

        var products = await query.OrderByDescending(p => p.CreatedAt).Take(18).ToListAsync();
        if (!products.Any()) return string.Empty;

        return string.Join("\n", products.Select(p =>
        {
            var price = p.Variants.Any() ? p.Variants.Min(v => v.Price ?? p.BasePrice) : p.BasePrice;
            var imageUrl = p.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.ImageUrl ?? "";
            var variants = string.Join(", ", p.Variants.Where(v => v.IsActive).Select(v => $"{GetVariantSuggestionDisplayName(v)}({v.Id})"));
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
            "giá", "bao nhiêu", "thương hiệu", "shop", "sản phẩm", "local brand",
            "loại", "chọn", "phân", "đơn", "tạo", "checkout", "chốt", "nhờ", "giao", "size",
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

    /// <summary>
    /// Tin nhắn chỉ nhắm checkout / tạo đơn — không được ghi đè bằng reply "không tìm thấy sản phẩm".
    /// </summary>
    private static bool IsCheckoutOrOrderOnlyMessage(string message)
    {
        var lower = message.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(lower)) return false;
        return System.Text.RegularExpressions.Regex.IsMatch(
            lower,
            @"\b(tạo\s*đơn|đặt\s*đơn|checkout|thanh\s*toán|chốt\s*đơn|mua\s*luôn|đặt\s*hàng)\b");
    }

    /// <summary>
    /// Khớp tin nhắn tiếp theo với gợi ý SP vừa lưu (chọn phân loại / trả lời ngắn sau thẻ sản phẩm).
    /// </summary>
    private static List<ProductSuggestionDto> TryMatchRecentSuggestedProducts(string message, IEnumerable<AiChatMessage> messages)
    {
        var result = new List<ProductSuggestionDto>();
        if (string.IsNullOrWhiteSpace(message)) return result;

        var lower = message.Trim().ToLowerInvariant();

        foreach (var msg in messages
                     .Where(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                                 && !string.IsNullOrWhiteSpace(m.SuggestedProductsJson))
                     .OrderByDescending(m => m.CreatedAt ?? DateTime.MinValue)
                     .ThenByDescending(m => m.Id))
        {
            List<ProductSuggestionDto>? list;
            try
            {
                list = JsonSerializer.Deserialize<List<ProductSuggestionDto>>(msg.SuggestedProductsJson!, ProductJsonOptions);
            }
            catch
            {
                continue;
            }

            if (list is not { Count: > 0 }) continue;

            var seen = new HashSet<Guid>();
            foreach (var p in list)
            {
                if (p.Id == Guid.Empty || seen.Contains(p.Id)) continue;

                var pName = (p.Name ?? string.Empty).Trim().ToLowerInvariant();
                var nameHit = pName.Length >= 3 && lower.Contains(pName);
                var variantHit = p.Variants?.Any(v =>
                {
                    var vn = (v.VariantName ?? string.Empty).Trim().ToLowerInvariant();
                    return vn.Length >= 2 && lower.Contains(vn);
                }) ?? false;

                if (nameHit || variantHit)
                {
                    seen.Add(p.Id);
                    result.Add(p);
                }
            }

            if (result.Count > 0)
                break;
        }

        return result;
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
            "đỉnh", "xịn", "sang", "trẻ", "hợp", "thời", "thượng", "lưu",
            // Hội thoại / xưng hô — không phải từ khóa sản phẩm (tránh AND search lỗi)
            "loại", "kiểu", "hàng", "bạn", "dạ", "vâng", "nhờ", "giùm", "lấy", "luôn", "ơi",
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
        var dbQuery = GetFilteredProductsQuery(query);

        if (maxPrice.HasValue)
            dbQuery = dbQuery.Where(p =>
                p.BasePrice <= maxPrice.Value ||
                p.Variants.Any(v => v.IsActive && (v.Price ?? p.BasePrice) <= maxPrice.Value));

        // Đủ SP để hiển thị trong widget (trước đây Take(5) + AND token khiến thường chỉ còn 3–5 món)
        var products = await dbQuery
            .OrderByDescending(p => p.CreatedAt)
            .Take(15)
            .ToListAsync();

        return products.Select(p => new ProductSuggestionDto
        {
            Id = p.Id,
            Name = p.Name,
            BasePrice = GetMinCustomerFacingPrice(p),
            ImageUrl = p.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.ImageUrl,
            CategoryName = p.Category?.Name,
            Variants = p.Variants
                .Where(v => v.IsActive)
                .OrderBy(v => v.CreatedAt)
                .Select(v => new VariantSuggestionDto
                {
                    Id = v.Id,
                    VariantName = GetVariantSuggestionDisplayName(v),
                    Price = v.Price
                }).ToList()
        }).ToList();
    }

    /// <summary>
    /// Nhãn phân loại trên thẻ chat và trong câu «các phân loại: …».
    /// Ưu tiên giá trị trong jsonb <c>attributes</c> để tránh «Size · Size · Size» khi mỗi dòng chỉ khác thuộc tính.
    /// </summary>
    private static string GetVariantSuggestionDisplayName(ProductVariant v)
    {
        var fromJson = TryFormatVariantAttributesLabel(v.Attributes);
        if (!string.IsNullOrWhiteSpace(fromJson))
            return fromJson.Trim();

        var n = (v.VariantName ?? string.Empty).Trim();
        return string.IsNullOrEmpty(n) ? "Mặc định" : n;
    }

    private static string? TryFormatVariantAttributesLabel(JsonDocument? doc)
    {
        if (doc == null) return null;

        try
        {
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                var s = root.GetString();
                return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            }

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var props = root.EnumerateObject().ToList();
            if (props.Count == 0) return null;

            if (props.Count == 1)
            {
                var s = FormatJsonElementAsDisplayValue(props[0].Value);
                return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            }

            var parts = new List<string>();
            foreach (var prop in props)
            {
                var s = FormatJsonElementAsDisplayValue(prop.Value);
                if (string.IsNullOrWhiteSpace(s)) continue;

                var key = prop.Name.Trim();
                parts.Add(string.IsNullOrEmpty(key) ? s : $"{key}: {s}");
            }

            return parts.Count > 0 ? string.Join(" · ", parts) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FormatJsonElementAsDisplayValue(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "Có",
            JsonValueKind.False => "Không",
            _ => null
        };

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
            "nghìn", "ngàn", "triệu", "trăm", "đồng", "vnđ", "vnd",
            "đặc", "sản",
            "loại", "kiểu", "hàng", "bạn", "dạ", "vâng", "nhờ", "giùm", "lấy", "luôn", "ơi","checkout","chọn","chọn loại",
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

    private static string TrimMarkdownCodeFence(string raw)
    {
        var json = raw.Trim();
        if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            json = json[7..].Trim();
        if (json.StartsWith("```"))
            json = json[3..].Trim();
        if (json.EndsWith("```"))
            json = json[..^3].Trim();
        return json;
    }

    /// <summary>
    /// Tìm vị trí <c>}</c> đóng cặp với <c>{</c> tại <paramref name="start"/> (bỏ qua chuỗi trong dấu ngoặc kép).
    /// </summary>
    private static int? IndexOfMatchingJsonObjectEnd(string s, int start)
    {
        if (start < 0 || start >= s.Length || s[start] != '{')
            return null;

        var depth = 0;
        var inString = false;
        var escape = false;
        for (var i = start; i < s.Length; i++)
        {
            var c = s[i];
            if (escape)
            {
                escape = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escape = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return null;
    }

    /// <summary>
    /// Gemini đôi khi trả prose rồi mới tới JSON — cô lập object JSON đầu tiên để parse.
    /// </summary>
    private static string? TryIsolateFirstJsonObject(string trimmedAfterFence)
    {
        if (string.IsNullOrWhiteSpace(trimmedAfterFence))
            return null;

        var s = trimmedAfterFence;
        if (s.StartsWith('{'))
        {
            var end = IndexOfMatchingJsonObjectEnd(s, 0);
            if (!end.HasValue)
                return s;
            return end.Value < s.Length - 1 ? s[..(end.Value + 1)] : s;
        }

        var idx = s.IndexOf('{');
        if (idx < 0)
            return null;

        var end2 = IndexOfMatchingJsonObjectEnd(s, idx);
        return end2.HasValue ? s.Substring(idx, end2.Value - idx + 1) : null;
    }

    /// <summary>
    /// SP một dòng kết quả có ≥2 biến thể: không cho add_to_cart / product_to_add thiếu variant;
    /// khớp variant từ câu user; chuẩn hoá reply nếu model vẫn nói "đã thêm giỏ" khi chưa đủ phân loại.
    /// </summary>
    private static void ApplyMultiVariantSelectionGate(string userMessage, List<ProductSuggestionDto> products, LlmParsedResponse parsed)
    {
        if (products.Count != 1) return;

        var p = products[0];
        var variants = p.Variants ?? new List<VariantSuggestionDto>();
        if (variants.Count <= 1)
        {
            if (variants.Count == 1
                && string.Equals(parsed.Intent, "add_to_cart", StringComparison.OrdinalIgnoreCase)
                && parsed.ProductToAdd?.ProductId == p.Id
                && !parsed.ProductToAdd.VariantId.HasValue)
            {
                parsed.ProductToAdd.VariantId = variants[0].Id;
            }

            return;
        }

        if (TryMatchVariantFromUserMessage(userMessage, p, out var matchedVariantId))
        {
            parsed.ProductToAdd ??= new ProductToAddDto { ProductId = p.Id, Quantity = 1 };
            if (!parsed.ProductToAdd.ProductId.HasValue || parsed.ProductToAdd.ProductId == p.Id)
            {
                parsed.ProductToAdd.ProductId = p.Id;
                parsed.ProductToAdd.VariantId = matchedVariantId;
            }

            return;
        }

        var pta = parsed.ProductToAdd;
        var targetsThisProduct = pta?.ProductId == null || pta.ProductId == p.Id;
        if (string.Equals(parsed.Intent, "add_to_cart", StringComparison.OrdinalIgnoreCase) && targetsThisProduct)
        {
            parsed.Intent = "product_search";
            parsed.ProductToAdd = null;
        }
        else if (pta?.ProductId == p.Id && !pta.VariantId.HasValue)
        {
            parsed.ProductToAdd = null;
        }

        if (ReplyClaimsAddedToCart(parsed.Reply))
            parsed.Reply = BuildAskVariantChoiceReply(p);
        else if (!ReplyAlreadyPromptsVariantChoice(parsed.Reply) && !ReplyListsAllVariantNames(parsed.Reply, p))
        {
            var hint = BuildAskVariantChoiceReply(p);
            var baseReply = (parsed.Reply ?? string.Empty).TrimEnd();
            parsed.Reply = string.IsNullOrEmpty(baseReply) ? hint : $"{baseReply}\n\n{hint}";
        }
    }

    private static bool TryMatchVariantFromUserMessage(string userMessage, ProductSuggestionDto product, out Guid variantId)
    {
        variantId = default;
        var variants = product.Variants ?? new List<VariantSuggestionDto>();
        if (variants.Count == 0) return false;

        if (variants.Count == 1)
        {
            variantId = variants[0].Id;
            return true;
        }

        var lower = userMessage.Trim().ToLowerInvariant();
        foreach (var v in variants)
        {
            var vn = (v.VariantName ?? string.Empty).Trim().ToLowerInvariant();
            if (vn.Length < 2) continue;

            if (lower.Contains(vn))
            {
                variantId = v.Id;
                return true;
            }

            var parts = vn.Split(new[] { ' ', '-', '–', '/', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.Length >= 3 && lower.Contains(part))
                {
                    variantId = v.Id;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ReplyClaimsAddedToCart(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply)) return false;
        var r = reply.Trim().ToLowerInvariant();
        if (!r.Contains("giỏ")) return false;

        return r.Contains("đã thêm")
               || r.Contains("thêm vào")
               || r.Contains("đã cho")
               || r.Contains("vào giỏ")
               || r.Contains("vào giỏ hàng");
    }

    private static bool ReplyAlreadyPromptsVariantChoice(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply)) return false;
        var r = reply.Trim().ToLowerInvariant();
        if ((r.Contains("phân loại") || r.Contains("biến thể") || r.Contains("loại nào"))
            && (r.Contains("chọn") || r.Contains("?") || r.Contains("bạn muốn")))
            return true;

        return r.Contains("bấm chọn") && r.Contains("thẻ");
    }

    private static bool ReplyListsAllVariantNames(string? reply, ProductSuggestionDto p)
    {
        if (string.IsNullOrWhiteSpace(reply)) return false;
        var r = reply.Trim().ToLowerInvariant();
        var names = (p.Variants ?? new List<VariantSuggestionDto>())
            .Select(v => (v.VariantName ?? "").Trim().ToLowerInvariant())
            .Where(n => n.Length >= 2)
            .ToList();
        if (names.Count < 2) return false;
        return names.All(n => r.Contains(n));
    }

    private static string BuildAskVariantChoiceReply(ProductSuggestionDto p)
    {
        var names = string.Join(" · ", (p.Variants ?? new List<VariantSuggestionDto>()).Select(v => v.VariantName));
        return $"«{p.Name}» đang có các phân loại: {names}. Bạn chọn giúp mình một phân loại (gõ đúng tên loại trong chat, hoặc bấm chọn trên thẻ sản phẩm bên dưới), mình sẽ thêm đúng món vào giỏ nhé.";
    }

    private static LlmParsedResponse? TryParseLlmJsonDocument(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            ProductToAddDto? productToAdd = null;
            if (root.TryGetProperty("product_to_add", out var pta) && pta.ValueKind == JsonValueKind.Object)
            {
                productToAdd = new ProductToAddDto
                {
                    ProductId = pta.TryGetProperty("product_id", out var pid) && pid.ValueKind == JsonValueKind.String
                        ? (Guid.TryParse(pid.GetString(), out var g1) ? g1 : null) : null,
                    VariantId = pta.TryGetProperty("variant_id", out var vid) && vid.ValueKind == JsonValueKind.String
                        ? (Guid.TryParse(vid.GetString(), out var g2) ? g2 : null) : null,
                    Quantity = pta.TryGetProperty("quantity", out var qty) && qty.ValueKind == JsonValueKind.Number
                        ? qty.GetInt32()
                        : 1
                };
            }

            return new LlmParsedResponse
            {
                Reply = root.TryGetProperty("reply", out var reply) && reply.ValueKind == JsonValueKind.String
                    ? reply.GetString() ?? ""
                    : json,
                Intent = root.TryGetProperty("intent", out var intent) && intent.ValueKind == JsonValueKind.String
                    ? intent.GetString() ?? "general"
                    : "general",
                SearchQuery = root.TryGetProperty("search_query", out var sq) && sq.ValueKind == JsonValueKind.String
                    ? sq.GetString()
                    : null,
                NeedsConfirmation = root.TryGetProperty("needs_confirmation", out var nc) && nc.ValueKind == JsonValueKind.True,
                ProductToAdd = productToAdd
            };
        }
        catch
        {
            return null;
        }
    }

    private static string TakeProseBeforeFirstJsonLine(string trimmed)
    {
        var idx = trimmed.IndexOf("\n{", StringComparison.Ordinal);
        if (idx <= 0)
            return trimmed;
        return trimmed[..idx].TrimEnd();
    }

    private static LlmParsedResponse ParseLlmResponse(string raw)
    {
        var cleared = TrimMarkdownCodeFence(raw);
        var isolated = TryIsolateFirstJsonObject(cleared);
        if (isolated != null)
        {
            var parsed = TryParseLlmJsonDocument(isolated);
            if (parsed != null)
                return parsed;
        }

        var fallbackDoc = TryParseLlmJsonDocument(cleared);
        if (fallbackDoc != null)
            return fallbackDoc;

        return new LlmParsedResponse
        {
            Reply = TakeProseBeforeFirstJsonLine(cleared),
            Intent = "general"
        };
    }

    private static List<ProductSuggestionDto>? DeserializeSuggestedProducts(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<ProductSuggestionDto>>(json, ProductJsonOptions);
        }
        catch
        {
            return null;
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
                CreatedAt = m.CreatedAt,
                Products = DeserializeSuggestedProducts(m.SuggestedProductsJson)
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
