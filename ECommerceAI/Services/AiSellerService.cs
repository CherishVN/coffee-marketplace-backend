using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ECommerceAI.Data;
using ECommerceAI.Data.Entities;
using ECommerceAI.DTOs.Seller;
using ECommerceAI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECommerceAI.Services;

public class AiSellerService : IAiSellerService
{
    private const string DefaultSystemPrompt = "Bạn là AI hỗ trợ seller.";
    private const string ActionPending = "pending";
    private const int MaxPromptCategories = 150;
    private const int MaxPromptTags = 200;
    private const int MaxPromptMaterials = 150;
    private const int MaxAnalyzeImageUrls = 2;
    private static readonly TimeSpan CandidateCacheDuration = TimeSpan.FromHours(1);

    /// <summary>Parse JSON từ Gemini khi model trả camelCase (suggest-* endpoints).</summary>
    private static readonly JsonSerializerOptions _jsonReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new SafeNullableGuidConverter() }
    };

    /// <summary>Parse JSON từ Gemini JSON-mode — Gemini trả snake_case theo schema, cần SnakeCaseLower để map đúng property.</summary>
    private static readonly JsonSerializerOptions _jsonSnakeReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new SafeNullableGuidConverter() }
    };

    private static readonly Lazy<string> _sellerPrompt = new(() => LoadPromptFromFile("SellerSuggestPrompt.txt", DefaultSystemPrompt));
    private static readonly Lazy<string> _imagePrompt = new(() => LoadPromptFromFile("ImageAnalysisPrompt.txt", DefaultSystemPrompt));
    private static readonly object _analyzeImageSchema = BuildAnalyzeImageSchema();
    private static readonly object _analyzeProductSchema = BuildAnalyzeProductSchema();

    private const string CandidateCacheKey = "PromptCandidates";

    private readonly AiDbContext _context;
    private readonly GeminiClientService _gemini;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AiSellerService> _logger;

    public AiSellerService(AiDbContext context, GeminiClientService gemini, IMemoryCache cache, ILogger<AiSellerService> logger)
    {
        _context = context;
        _gemini = gemini;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>Đọc suggested_tags jsonb: hỗ trợ cả {tag, confidence} và {tagName, score}.</summary>
    private static List<SuggestedTagJsonItem> ParseSuggestedTagsFromJsonDocument(JsonDocument doc)
    {
        var list = new List<SuggestedTagJsonItem>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            string? tagName = null;
            if (el.TryGetProperty("tag", out var tagProp))
                tagName = tagProp.GetString();
            else if (el.TryGetProperty("tagName", out var nameProp))
                tagName = nameProp.GetString();

            if (string.IsNullOrWhiteSpace(tagName))
                continue;

            decimal confidence = 0m;
            if (el.TryGetProperty("confidence", out var cEl) && cEl.TryGetDecimal(out var c))
                confidence = c;
            else if (el.TryGetProperty("confidenceScore", out var csEl) && csEl.TryGetDecimal(out var cs))
                confidence = cs;
            else if (el.TryGetProperty("score", out var sEl) && sEl.TryGetDecimal(out var sc))
                confidence = sc;

            if (confidence > 1m)
                confidence /= 100m;

            list.Add(new SuggestedTagJsonItem
            {
                Tag = tagName.Trim(),
                Confidence = confidence < 0m ? 0m : confidence
            });
        }

        return list;
    }

    /// <summary>Đọc suggested_materials jsonb: materialId + materialName + confidence | score | confidenceScore.</summary>
    private static List<SuggestedMaterialJsonItem> ParseSuggestedMaterialsFromJsonDocument(JsonDocument doc)
    {
        var list = new List<SuggestedMaterialJsonItem>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            Guid? matId = null;
            if (el.TryGetProperty("materialId", out var idEl))
            {
                if (idEl.ValueKind == JsonValueKind.String && Guid.TryParse(idEl.GetString(), out var g))
                    matId = g;
                else if (idEl.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(idEl.GetString()))
                { /* ignore invalid */ }
            }

            string? matName = null;
            if (el.TryGetProperty("materialName", out var nameProp))
                matName = nameProp.GetString();

            if (string.IsNullOrWhiteSpace(matName))
                continue;

            decimal confidence = 0m;
            if (el.TryGetProperty("confidence", out var cEl) && cEl.TryGetDecimal(out var c))
                confidence = c;
            else if (el.TryGetProperty("confidenceScore", out var csEl) && csEl.TryGetDecimal(out var cs))
                confidence = cs;
            else if (el.TryGetProperty("score", out var sEl) && sEl.TryGetDecimal(out var sc))
                confidence = sc;

            if (confidence > 1m)
                confidence /= 100m;

            list.Add(new SuggestedMaterialJsonItem
            {
                MaterialId = matId,
                MaterialName = matName.Trim(),
                Confidence = confidence < 0m ? 0m : confidence
            });
        }

        return list;
    }

    // ── Candidate loading (full catalog for prompt) ─────────────────────────
    private sealed record CandidateSet(
        Dictionary<long, CategoryCandidate> CatById,
        List<CategoryCandidate> AllCatsOrdered,
        List<TagCandidate> AllTags,
        List<MaterialCandidate> AllMaterials);

    private sealed record PromptCatalogSlice(
        List<CategoryCandidate> Categories,
        List<TagCandidate> Tags,
        List<MaterialCandidate> Materials);

    /// <summary>
    /// Loads full active catalog (categories, tags, materials) để build path và thu hẹp theo từng request.
    /// Cache lâu hơn để giảm tải DB; prompt gửi Gemini chỉ dùng subset qua <see cref="NarrowCatalogForPrompt"/>.
    /// </summary>
    private async Task<CandidateSet> GetPromptCandidatesAsync()
    {
        if (_cache.TryGetValue(CandidateCacheKey, out CandidateSet? cached) && cached != null)
            return cached;

        var allCats = await _context.Categories
            .Where(c => c.IsActive)
            .Select(c => new CategoryCandidate(c.Id, c.Name, c.Level, c.ParentId))
            .ToListAsync();
        var catById = allCats.ToDictionary(c => c.Id);

        var allCatsOrdered = allCats
            .OrderByDescending(c => c.Level)
            .ThenBy(c => c.Name)
            .ToList();

        var allTags = await _context.Tags
            .OrderBy(t => t.Name)
            .Select(t => new TagCandidate(t.Id, t.Name))
            .ToListAsync();

        var allMaterials = await _context.Materials
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .Select(m => new MaterialCandidate(m.Id, m.Name))
            .ToListAsync();

        var result = new CandidateSet(catById, allCatsOrdered, allTags, allMaterials);

        _cache.Set(CandidateCacheKey, result, CandidateCacheDuration);

        return result;
    }

    private static List<string> ExtractSearchTokens(string? title, string? description, int maxTokens = 16)
    {
        var text = $"{title} {description}";
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        return Regex.Split(text.ToLowerInvariant(), @"\W+")
            .Where(t => t.Length >= 2)
            .Distinct()
            .Take(maxTokens)
            .ToList();
    }

    private static bool IsSelfOrDescendantOf(long categoryId, long ancestorId, Dictionary<long, CategoryCandidate> catById)
    {
        var cur = catById.GetValueOrDefault(categoryId);
        while (cur != null)
        {
            if (cur.Id == ancestorId)
                return true;
            cur = cur.ParentId is long p ? catById.GetValueOrDefault(p) : null;
        }
        return false;
    }

    /// <summary>
    /// Thu hẹp catalog đưa vào prompt: ưu tiên nhánh category đã chọn, khớp từ khóa từ title/description, rồi pad còn lại.
    /// </summary>
    private static PromptCatalogSlice NarrowCatalogForPrompt(
        CandidateSet src,
        Func<long, string> buildPath,
        string? title,
        string? description,
        long? preferredCategoryId,
        int maxCategories,
        int maxTags,
        int maxMaterials)
    {
        var tokens = ExtractSearchTokens(title, description);
        var catById = src.CatById;

        var categories = new List<CategoryCandidate>();
        if (maxCategories > 0)
        {
            var seenCat = new HashSet<long>();
            void AddCats(IEnumerable<CategoryCandidate> sequence)
            {
                foreach (var c in sequence)
                {
                    if (categories.Count >= maxCategories)
                        return;
                    if (!seenCat.Add(c.Id))
                        continue;
                    categories.Add(c);
                }
            }

            if (preferredCategoryId.HasValue && catById.ContainsKey(preferredCategoryId.Value))
            {
                var scoped = src.AllCatsOrdered
                    .Where(c => IsSelfOrDescendantOf(c.Id, preferredCategoryId.Value, catById))
                    .OrderByDescending(c => c.Level)
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase);
                AddCats(scoped);
            }

            if (categories.Count < maxCategories && tokens.Count > 0)
            {
                var kwMatches = src.AllCatsOrdered
                    .Where(c => !seenCat.Contains(c.Id))
                    .Where(c => tokens.Any(t =>
                        buildPath(c.Id).Contains(t, StringComparison.OrdinalIgnoreCase) ||
                        c.Name.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(c => c.Level)
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase);
                AddCats(kwMatches);
            }

            if (categories.Count < maxCategories)
            {
                var fallback = src.AllCatsOrdered
                    .Where(c => !seenCat.Contains(c.Id))
                    .OrderByDescending(c => c.Level)
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase);
                AddCats(fallback);
            }
        }

        var tags = new List<TagCandidate>();
        if (maxTags > 0)
        {
            var seenTag = new HashSet<long>();
            void AddTags(IEnumerable<TagCandidate> sequence)
            {
                foreach (var t in sequence)
                {
                    if (tags.Count >= maxTags)
                        return;
                    if (!seenTag.Add(t.Id))
                        continue;
                    tags.Add(t);
                }
            }

            if (tokens.Count > 0)
            {
                var kw = src.AllTags
                    .Where(t => tokens.Any(tok => t.Name.Contains(tok, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase);
                AddTags(kw);
            }

            AddTags(src.AllTags.Where(t => !seenTag.Contains(t.Id)));
        }

        var materials = new List<MaterialCandidate>();
        if (maxMaterials > 0)
        {
            var seenMat = new HashSet<Guid>();
            void AddMats(IEnumerable<MaterialCandidate> sequence)
            {
                foreach (var m in sequence)
                {
                    if (materials.Count >= maxMaterials)
                        return;
                    if (!seenMat.Add(m.Id))
                        continue;
                    materials.Add(m);
                }
            }

            if (tokens.Count > 0)
            {
                var kw = src.AllMaterials
                    .Where(m => tokens.Any(tok => m.Name.Contains(tok, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase);
                AddMats(kw);
            }

            AddMats(src.AllMaterials.Where(m => !seenMat.Contains(m.Id)));
        }

        return new PromptCatalogSlice(categories, tags, materials);
    }

    /// <summary>Ngữ cảnh từ lịch sử tag (accepted/modified) — dùng chung cho suggest-tags và analyze-*.</summary>
    private async Task<string> BuildSellerTagHistoryHintAsync(Guid sellerId)
    {
        var recentChosen = await _context.AiTagSuggestions
            .AsNoTracking()
            .Where(s => s.SellerId == sellerId && (s.Action == "accepted" || s.Action == "modified"))
            .OrderByDescending(s => s.CreatedAt)
            .Take(5)
            .ToListAsync();

        if (!recentChosen.Any())
            return string.Empty;

        var frequentTags = recentChosen
            .SelectMany(s =>
            {
                try { return JsonSerializer.Deserialize<List<string>>(s.ChosenTags.RootElement.GetRawText()) ?? new(); }
                catch { return new List<string>(); }
            })
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToList();

        if (frequentTags.Count == 0)
            return string.Empty;

        return $"\nSeller này thường chọn các tags: {string.Join(", ", frequentTags)}. Ưu tiên gợi ý các tags tương tự nếu phù hợp.";
    }

    /// <summary>Ngữ cảnh từ lịch sử chất liệu — map id → tên từ catalog đang đưa vào prompt.</summary>
    private async Task<string> BuildSellerMaterialHistoryHintAsync(Guid sellerId, IReadOnlyDictionary<Guid, string> materialNameById)
    {
        var recentChosen = await _context.AiMaterialSuggestions
            .AsNoTracking()
            .Where(s => s.SellerId == sellerId && (s.Action == "accepted" || s.Action == "modified")
                        && s.ChosenMaterialIds != null && s.ChosenMaterialIds.Length > 0)
            .OrderByDescending(s => s.CreatedAt)
            .Take(5)
            .ToListAsync();

        if (!recentChosen.Any())
            return string.Empty;

        var frequentNames = recentChosen
            .SelectMany(s => s.ChosenMaterialIds!)
            .GroupBy(id => id)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => materialNameById.TryGetValue(g.Key, out var name) ? name : null)
            .OfType<string>()
            .ToList();

        if (frequentNames.Count == 0)
            return string.Empty;

        return $"\nSeller này thường chọn các chất liệu: {string.Join(", ", frequentNames)}. Ưu tiên gợi ý các chất liệu tương tự nếu phù hợp.";
    }

    // ── Gợi ý Category ─────────────────────────────────────────────────────────────
    public async Task<SuggestCategoryResponseDto> SuggestCategoryAsync(SuggestCategoryRequestDto request, Guid sellerId)
    {
        var candidates = await GetPromptCandidatesAsync();
        var catById = candidates.CatById;

        string BuildPath(long id)
        {
            var parts = new List<string>();
            var cur = catById.GetValueOrDefault(id);
            while (cur != null)
            {
                parts.Insert(0, cur.Name);
                cur = cur.ParentId.HasValue
                    ? catById.GetValueOrDefault(cur.ParentId.Value)
                    : null;
            }
            return string.Join(" > ", parts);
        }

        var promptCats = NarrowCatalogForPrompt(
            candidates,
            BuildPath,
            request.Title,
            request.Description,
            preferredCategoryId: null,
            maxCategories: MaxPromptCategories,
            maxTags: 0,
            maxMaterials: 0).Categories;

        var categoryList = string.Join("\n", promptCats.Select(c =>
            $"ID:{c.Id} | {BuildPath(c.Id)} (Level {c.Level})"));

        var jsonExample = """{"suggestions":[{"categoryId":123,"categoryName":"Tên danh mục","categoryPath":"Đường dẫn đầy đủ","confidenceScore":0.95}]}""";
        var userMessage = $"""
            Phân tích sản phẩm sau và gợi ý top 3 category phù hợp nhất từ danh sách dưới.

            Tên sản phẩm: {request.Title}
            Mô tả: {request.Description ?? "Không có"}

            QUAN TRỌNG:
            - Chỉ được chọn category có trong danh sách (dùng đúng categoryId)
            - Ưu tiên category cấp sâu nhất (leaf) phù hợp, KHÔNG chọn category gốc chung chung nếu có category con phù hợp hơn
            - Ví dụ: sản phẩm là quần jeans thì chọn "Thời Trang Nữ > Quần Jeans" (nếu có), KHÔNG chọn "Thời Trang Nữ" đơn thuần
            - Điền categoryPath đúng theo cột "Đường dẫn đầy đủ" trong danh sách (sao chép nguyên văn)

            Danh sách category (ID | Đường dẫn đầy đủ | Cấp):
            {categoryList}

            Trả về JSON: {jsonExample}
            (3 gợi ý, sắp xếp theo confidenceScore giảm dần)
            """;

        try
        {
            var raw = await _gemini.GenerateAsync(_sellerPrompt.Value, userMessage);
            var result = ParseJsonResponse<SuggestCategoryResponseDto>(raw, "SuggestCategory") ?? new SuggestCategoryResponseDto();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Category suggestion failed for seller {SellerId}", sellerId);
            return new SuggestCategoryResponseDto();
        }
    }

    // ── Gợi ý Tags ───────────────────────────────────────────────────────────
    public async Task<SuggestTagsResponseDto> SuggestTagsAsync(SuggestTagsRequestDto request, Guid sellerId)
    {
        var candidates = await GetPromptCandidatesAsync();
        var promptTags = NarrowCatalogForPrompt(
            candidates,
            _ => "",
            request.Title,
            request.Description,
            preferredCategoryId: null,
            maxCategories: 0,
            maxTags: MaxPromptTags,
            maxMaterials: 0).Tags;

        var tagList = string.Join(", ", promptTags.Select(t => $"{t.Name}(ID:{t.Id})"));
        var historyHint = await BuildSellerTagHistoryHintAsync(sellerId);

        var tagJsonExample = """{"suggestions":[{"tagId":1,"tagName":"Tên tag","confidenceScore":0.95}]}""";
        var userMessage = $"""
            Gợi ý tags phù hợp cho sản phẩm sau (chọn tối đa 10 tags):
            
            Tên: {request.Title}
            Mô tả: {request.Description ?? "Không có"}
            
            Tags có trong hệ thống: {tagList}{historyHint}
            
            Trả về JSON theo format: {tagJsonExample}
            """;

        try
        {
            var raw = await _gemini.GenerateAsync(_sellerPrompt.Value, userMessage);
            var result = ParseJsonResponse<SuggestTagsResponseDto>(raw, "SuggestTags") ?? new SuggestTagsResponseDto();

            // Lưu lịch sử gợi ý nếu seller đã có product (productId được truyền lên)
            if (request.ProductId.HasValue)
            {
                try
                {
                    var suggestedJson = JsonSerializer.Serialize(
                        result.Suggestions.Select(s => new
                        {
                            tag = s.TagName,
                            confidence = s.ConfidenceScore > 1m ? s.ConfidenceScore / 100m : s.ConfidenceScore
                        }));

                    var log = new AiTagSuggestion
                    {
                        Id = Guid.NewGuid(),
                        ProductId = request.ProductId.Value,
                        SellerId = sellerId,
                        InputTitle = request.Title,
                        InputDescription = request.Description,
                        SuggestedCategoryId = request.CategoryId,
                        SuggestedTags = JsonDocument.Parse(suggestedJson),
                        ChosenTags = JsonDocument.Parse("[]"),
                        Action = ActionPending,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.AiTagSuggestions.Add(log);
                    await _context.SaveChangesAsync();
                    result.LogId = log.Id;
                }
                catch (Exception saveEx)
                {
                    _logger.LogWarning(saveEx, "Không thể lưu lịch sử gợi ý tag cho product {ProductId}", request.ProductId);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tag suggestion failed for seller {SellerId}", sellerId);
            return new SuggestTagsResponseDto();
        }
    }

    // ── Lưu phản hồi sau khi seller chọn tags ────────────────────────────────────
    public async Task<bool> SaveTagSuggestionFeedbackAsync(SaveSuggestionFeedbackDto dto, Guid sellerId)
    {
        var log = await _context.AiTagSuggestions
            .FirstOrDefaultAsync(s => s.Id == dto.LogId && s.SellerId == sellerId);

        if (log == null) return false;

        // chosen_tags lưu dưới dạng ["tag1", "tag2"] (tên tag, không phải ID)
        var chosenJson = JsonSerializer.Serialize(dto.ChosenTagNames ?? new List<string>());
        log.ChosenCategoryId = dto.ChosenCategoryId;
        log.ChosenTags = JsonDocument.Parse(chosenJson);
        log.Action = dto.Action;

        await _context.SaveChangesAsync();
        return true;
    }

    // ── Lưu lịch sử sau khi tạo SP kèm AI (analyze-product / analyze-image) ───
    public async Task<bool> CommitProductAiTagSessionAsync(CommitProductAiTagSessionDto dto, Guid sellerId)
    {
        var shopIds = await _context.Shops.AsNoTracking()
            .Where(s => s.OwnerId == sellerId)
            .Select(s => s.Id)
            .ToListAsync();

        var ownsProduct = await _context.Products.AsNoTracking()
            .AnyAsync(p => p.Id == dto.ProductId && shopIds.Contains(p.ShopId));

        if (!ownsProduct)
            return false;

        var anySave = false;

        var suggestedItems = (dto.SuggestedTags ?? new List<CommitAiSuggestedTagDto>())
            .Where(t => !string.IsNullOrWhiteSpace(t.TagName))
            .Select(t =>
            {
                var c = t.ConfidenceScore;
                if (c > 1m) c /= 100m;
                if (c < 0m) c = 0m;
                return new { tag = t.TagName.Trim(), confidence = c };
            })
            .ToList();

        var suggestedNorm = suggestedItems
            .Select(x => x.tag.ToLowerInvariant())
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var chosenList = (dto.ChosenTagNames ?? new List<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var chosenNorm = chosenList
            .Select(t => t.ToLowerInvariant())
            .OrderBy(x => x)
            .ToList();

        if (suggestedNorm.Count > 0 || chosenNorm.Count > 0)
        {
            var action = suggestedNorm.SequenceEqual(chosenNorm) ? "accepted" : "modified";
            var suggestedJson = JsonSerializer.Serialize(suggestedItems);
            var chosenJson = JsonSerializer.Serialize(chosenList);

            _context.AiTagSuggestions.Add(new AiTagSuggestion
            {
                Id = Guid.NewGuid(),
                ProductId = dto.ProductId,
                SellerId = sellerId,
                InputTitle = dto.Title,
                InputDescription = dto.Description,
                SuggestedCategoryId = dto.CategoryId,
                SuggestedTags = JsonDocument.Parse(suggestedJson),
                ChosenCategoryId = dto.CategoryId,
                ChosenTags = JsonDocument.Parse(chosenJson),
                Action = action,
                CreatedAt = DateTime.UtcNow
            });
            anySave = true;
        }

        var matRows = (dto.SuggestedMaterials ?? new List<CommitAiSuggestedMaterialDto>())
            .Select(m =>
            {
                var c = m.ConfidenceScore;
                if (c > 1m) c /= 100m;
                if (c < 0m) c = 0m;
                var name = string.IsNullOrWhiteSpace(m.MaterialName) ? "" : m.MaterialName.Trim();
                return new { materialId = m.MaterialId, materialName = name, confidence = c };
            })
            .Where(x => (x.materialId.HasValue && x.materialId.Value != Guid.Empty) || x.materialName.Length > 0)
            .ToList();

        var suggestedMatIds = matRows
            .Where(x => x.materialId.HasValue && x.materialId.Value != Guid.Empty)
            .Select(x => x.materialId!.Value)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var chosenMatIds = (dto.ChosenMaterialIds ?? new List<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (suggestedMatIds.Count > 0 || chosenMatIds.Count > 0)
        {
            var matAction = suggestedMatIds.Count == chosenMatIds.Count && !suggestedMatIds.Except(chosenMatIds).Any()
                ? "accepted"
                : "modified";

            var matJson = JsonSerializer.Serialize(matRows.Select(x => new
            {
                materialId = x.materialId,
                materialName = x.materialName,
                confidence = x.confidence
            }));

            _context.AiMaterialSuggestions.Add(new AiMaterialSuggestion
            {
                Id = Guid.NewGuid(),
                ProductId = dto.ProductId,
                SellerId = sellerId,
                SuggestedMaterials = JsonDocument.Parse(matJson),
                ChosenMaterialIds = chosenMatIds.ToArray(),
                Action = matAction,
                CreatedAt = DateTime.UtcNow
            });
            anySave = true;
        }

        if (anySave)
            await _context.SaveChangesAsync();

        return true;
    }

    // ── Lấy lịch sử gợi ý tags ──────────────────────────────────────────────
    public async Task<TagSuggestionLogResponse> GetTagSuggestionLogsAsync(Guid sellerId, int page, int pageSize)
    {
        var query = _context.AiTagSuggestions
            .Where(s => s.SellerId == sellerId && s.Action != ActionPending)
            .OrderByDescending(s => s.CreatedAt);

        var total = await query.CountAsync();
        var accepted = await query.CountAsync(s => s.Action == "accepted");
        var modified = await query.CountAsync(s => s.Action == "modified");
        var rejected = await query.CountAsync(s => s.Action == "rejected");

        var pagedRows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = pagedRows
            .Select(s =>
            {
                List<SuggestedTagJsonItem> suggestedTags;
                try
                {
                    suggestedTags = ParseSuggestedTagsFromJsonDocument(s.SuggestedTags);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Parse suggested_tags failed for log {LogId}", s.Id);
                    suggestedTags = new List<SuggestedTagJsonItem>();
                }

                // Parse chosen_tags: ["vải cotton", "tối giản"]
                var chosenTags = new List<string>();
                try
                {
                    chosenTags = JsonSerializer.Deserialize<List<string>>(
                        s.ChosenTags.RootElement.GetRawText()) ?? new();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Parse chosen_tags failed for log {LogId}", s.Id);
                }

                return new TagSuggestionLogItem
                {
                    Id = s.Id,
                    ProductId = s.ProductId,
                    InputTitle = s.InputTitle,
                    SuggestedCategoryId = s.SuggestedCategoryId,
                    SuggestedTags = suggestedTags,
                    ChosenTags = chosenTags,
                    Action = s.Action,
                    CreatedAt = s.CreatedAt
                };
            })
            .ToList();

        return new TagSuggestionLogResponse
        {
            Items = items,
            Total = total,
            Accepted = accepted,
            Modified = modified,
            Rejected = rejected,
        };
    }

    public async Task<MaterialSuggestionLogResponse> GetMaterialSuggestionLogsAsync(Guid sellerId, int page, int pageSize)
    {
        var query = _context.AiMaterialSuggestions
            .Where(s => s.SellerId == sellerId && s.Action != ActionPending)
            .OrderByDescending(s => s.CreatedAt);

        var total = await query.CountAsync();
        var accepted = await query.CountAsync(s => s.Action == "accepted");
        var modified = await query.CountAsync(s => s.Action == "modified");
        var rejected = await query.CountAsync(s => s.Action == "rejected");

        var pagedRows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var productIds = pagedRows.Select(s => s.ProductId).Distinct().ToList();
        var productTitles = await _context.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        var matById = await _context.Materials.AsNoTracking()
            .ToDictionaryAsync(m => m.Id, m => m.Name);

        var items = pagedRows
            .Select(s =>
            {
                List<SuggestedMaterialJsonItem> suggestedMats;
                try
                {
                    suggestedMats = ParseSuggestedMaterialsFromJsonDocument(s.SuggestedMaterials);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Parse suggested_materials failed for log {LogId}", s.Id);
                    suggestedMats = new List<SuggestedMaterialJsonItem>();
                }

                var chosenIds = s.ChosenMaterialIds?.Where(id => id != Guid.Empty).Distinct().ToList() ?? new List<Guid>();
                var chosenNames = chosenIds
                    .Select(id => matById.TryGetValue(id, out var n) ? n : id.ToString())
                    .ToList();

                return new MaterialSuggestionLogItem
                {
                    Id = s.Id,
                    ProductId = s.ProductId,
                    ProductName = productTitles.TryGetValue(s.ProductId, out var pn) ? pn : null,
                    SuggestedMaterials = suggestedMats,
                    ChosenMaterialIds = chosenIds,
                    ChosenMaterialNames = chosenNames,
                    Action = s.Action,
                    CreatedAt = s.CreatedAt
                };
            })
            .ToList();

        return new MaterialSuggestionLogResponse
        {
            Items = items,
            Total = total,
            Accepted = accepted,
            Modified = modified,
            Rejected = rejected,
        };
    }

    // ── Gợi ý Materials ──────────────────────────────────────────────────────
    public async Task<SuggestMaterialsResponseDto> SuggestMaterialsAsync(SuggestMaterialsRequestDto request, Guid sellerId)
    {
        var candidates = await GetPromptCandidatesAsync();
        var promptMats = NarrowCatalogForPrompt(
            candidates,
            _ => "",
            request.Title,
            request.Description,
            preferredCategoryId: null,
            maxCategories: 0,
            maxTags: 0,
            maxMaterials: MaxPromptMaterials).Materials;

        var materialList = string.Join(", ", promptMats.Select(m => $"{m.Name}(ID:{m.Id})"));
        var materialById = candidates.AllMaterials.ToDictionary(m => m.Id, m => m.Name);
        var historyHint = await BuildSellerMaterialHistoryHintAsync(sellerId, materialById);

        var matJsonExample = """{"suggestions":[{"materialId":"uuid-here","materialName":"Tên chất liệu","confidenceScore":0.95}]}""";
        var userMessage = $"""
            Gợi ý chất liệu (materials) phù hợp cho sản phẩm sau:
            
            Tên: {request.Title}
            Mô tả: {request.Description ?? "Không có"}
            
            Materials có trong hệ thống: {materialList}{historyHint}
            
            Trả về JSON theo format: {matJsonExample}
            
            BẮT BUỘC: với mỗi gợi ý, materialId phải là đúng GUID trong danh sách (phần sau "ID:"), trùng với materialName — không được bỏ trống hoặc tự bịa UUID.
            """;

        try
        {
            var raw = await _gemini.GenerateAsync(_sellerPrompt.Value, userMessage);
            var result = ParseJsonResponse<SuggestMaterialsResponseDto>(raw, "SuggestMaterials") ?? new SuggestMaterialsResponseDto();

            // Lưu log gợi ý nếu seller đã có product
            if (request.ProductId.HasValue)
            {
                try
                {
                    var suggestedJson = JsonSerializer.Serialize(
                        result.Suggestions.Select(s => new
                        {
                            materialId = s.MaterialId,
                            materialName = s.MaterialName,
                            confidence = s.ConfidenceScore > 1m ? s.ConfidenceScore / 100m : s.ConfidenceScore
                        }));

                    var log = new AiMaterialSuggestion
                    {
                        Id = Guid.NewGuid(),
                        ProductId = request.ProductId.Value,
                        SellerId = sellerId,
                        SuggestedMaterials = JsonDocument.Parse(suggestedJson),
                        ChosenMaterialIds = Array.Empty<Guid>(),
                        Action = ActionPending,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.AiMaterialSuggestions.Add(log);
                    await _context.SaveChangesAsync();
                    result.LogId = log.Id;
                }
                catch (Exception saveEx)
                {
                    _logger.LogWarning(saveEx, "Không thể lưu lịch sử gợi ý material cho product {ProductId}", request.ProductId);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Material suggestion failed for seller {SellerId}", sellerId);
            return new SuggestMaterialsResponseDto();
        }
    }

    // ── Lưu phản hồi sau khi seller chọn materials ───────────────────────────
    public async Task<bool> SaveMaterialSuggestionFeedbackAsync(SaveMaterialFeedbackDto dto, Guid sellerId)
    {
        var log = await _context.AiMaterialSuggestions
            .FirstOrDefaultAsync(s => s.Id == dto.LogId && s.SellerId == sellerId);

        if (log == null) return false;

        log.ChosenMaterialIds = dto.ChosenMaterialIds?.ToArray() ?? Array.Empty<Guid>();
        log.Action = dto.Action;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<AnalyzeImageResponseDto> AnalyzeImageAsync(AnalyzeImageRequestDto request, Guid sellerId)
    {
        if (request.ImageUrls == null || request.ImageUrls.Count == 0)
            return new AnalyzeImageResponseDto { Success = false, ErrorMessage = "Vui lòng cung cấp ít nhất 1 URL ảnh." };

        if (request.ImageUrls.Count > MaxAnalyzeImageUrls)
            request.ImageUrls = request.ImageUrls.Take(MaxAnalyzeImageUrls).ToList();

        var candidates = await GetPromptCandidatesAsync();
        var catById2 = candidates.CatById;

        string BuildImagePath(long id)
        {
            var parts = new List<string>();
            var curId = (long?)id;
            while (curId.HasValue && catById2.TryGetValue(curId.Value, out var cat))
            {
                parts.Insert(0, cat.Name);
                curId = cat.ParentId;
            }
            return string.Join(" > ", parts);
        }

        var promptSlice = NarrowCatalogForPrompt(
            candidates,
            BuildImagePath,
            request.ProductTitle,
            request.ProductDescription,
            preferredCategoryId: null,
            maxCategories: MaxPromptCategories,
            maxTags: MaxPromptTags,
            maxMaterials: MaxPromptMaterials);

        var categoryList = string.Join("\n", promptSlice.Categories.Select(c => $"ID:{c.Id} | {BuildImagePath(c.Id)} (Level {c.Level})"));
        var tagList = string.Join(", ", promptSlice.Tags.Select(t => $"{t.Name}(ID:{t.Id})"));
        var materialList = string.Join(", ", promptSlice.Materials.Select(m => $"{m.Name}(ID:{m.Id})"));
        var tagHistoryHint = await BuildSellerTagHistoryHintAsync(sellerId);
        var matNameById = candidates.AllMaterials.ToDictionary(m => m.Id, m => m.Name);
        var matHistoryHint = await BuildSellerMaterialHistoryHintAsync(sellerId, matNameById);
        const string sellerHabitBalanceNoteImage = """

            Khi chọn tags và materials: ưu tiên đúng với ảnh và mô tả; có thể ưu tiên tag/chất liệu trùng thói quen shop (nếu có) khi phù hợp — không ép lặp nếu sản phẩm khác loại.
            """;

        var imagePrompt = _imagePrompt.Value;

        var jsonExample = """
            {
              "quality": {
                "score": 8,
                "rating": "good",
                "hasGoodLighting": true,
                "hasCleanBackground": true,
                "isProductCentered": true,
                "hasHighResolution": true
              },
              "suggestedCategories": [
                {"categoryId": 12, "categoryName": "Áo sơ mi", "categoryPath": "Thời trang > Nam > Áo sơ mi", "confidenceScore": 0.95}
              ],
              "suggestedTags": [
                {"tagId": 5, "tagName": "cotton", "confidenceScore": 0.90}
              ],
              "suggestedMaterials": [
                {"materialId": "uuid-here", "materialName": "Cotton", "confidenceScore": 0.88}
              ],
              "improvements": [
                "Nên chụp trên nền trắng để sản phẩm nổi bật hơn",
                "Thêm ảnh chi tiết vải/texture"
              ],
              "summary": "Sản phẩm áo sơ mi nam chất lượng ảnh tốt, màu trắng, chất liệu cotton."
            }
            """;

        var userMessage = $"""
            Phân tích ảnh sản phẩm thương mại điện tử này và trả về kết quả phân tích.
            
            {(string.IsNullOrWhiteSpace(request.ProductTitle) ? "" : $"Tên sản phẩm: {request.ProductTitle}")}
            {(string.IsNullOrWhiteSpace(request.ProductDescription) ? "" : $"Mô tả: {request.ProductDescription}")}
            
            Danh sách categories có trong hệ thống (ID | Đường dẫn đầy đủ | Cấp):
            {categoryList}
            
            QUAN TRỌNG khi chọn category:
            - Chỉ được dùng ID có trong danh sách trên
            - Ưu tiên category cấp sâu nhất (leaf) phù hợp với sản phẩm, KHÔNG chọn category gốc chung chung nếu có category con phù hợp hơn
            - Ví dụ: với quần jeans nữ thì chọn "Thời Trang Nữ > Quần Jeans" (nếu có), KHÔNG chọn "Thời Trang Nữ" đơn thuần
            - Điền categoryPath đúng theo cột "Đường dẫn đầy đủ" trong danh sách (sao chép nguyên văn)
            
            Danh sách tags có trong hệ thống: {tagList}{tagHistoryHint}
            Danh sách materials có trong hệ thống: {materialList}{matHistoryHint}{sellerHabitBalanceNoteImage}
            
            Hãy trả về JSON theo format sau (không thêm markdown, chỉ JSON thuần):
            {jsonExample}
            
            Lưu ý:
            - quality.score: 1-10 (1=rất kém, 10=hoàn hảo)
            - quality.rating: "excellent"(9-10), "good"(7-8), "fair"(5-6), "poor"(1-4)
            - suggestedCategories: top 3 categories phù hợp nhất, dùng đúng ID từ danh sách
            - suggestedTags: tối đa 8 tags phù hợp, dùng đúng ID từ danh sách
            - suggestedMaterials: tối đa 3 materials, dùng đúng ID từ danh sách
            - improvements: 2-4 gợi ý cải thiện ảnh bằng tiếng Việt
            - summary: tóm tắt ngắn về sản phẩm trong ảnh bằng tiếng Việt
            """;

        try
        {
            var raw = await _gemini.GenerateWithImagesJsonAsync(
                imagePrompt,
                userMessage,
                request.ImageUrls,
                _analyzeImageSchema);

            if (raw.StartsWith("⚠️"))
                return new AnalyzeImageResponseDto { Success = false, ErrorMessage = raw };

            // JSON mode trả snake_case theo schema → phải dùng _jsonSnakeReadOptions để map đúng
            var result = ParseJsonResponseSnake<AnalyzeImageResponseDto>(raw, "AnalyzeImage");
            if (result == null)
            {
                _logger.LogWarning("AnalyzeImage parse failed. Raw snippet: {Raw}", raw.Length > 400 ? raw[..400] : raw);
                return new AnalyzeImageResponseDto { Success = false, ErrorMessage = "Không thể xử lý phản hồi từ AI. Vui lòng thử lại." };
            }

            result = NormalizeAnalyzeImageResult(result);

            // Post-validate within the candidate set that was shown to the model
            var validCatIds2 = new HashSet<long>(promptSlice.Categories.Select(c => c.Id));
            var validTagIds2 = new HashSet<long>(promptSlice.Tags.Select(t => t.Id));
            var validMatIds2 = new HashSet<Guid>(promptSlice.Materials.Select(m => m.Id));

            var catByPath2 = promptSlice.Categories
                .GroupBy(c => BuildImagePath(c.Id).Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
            var catByName2 = promptSlice.Categories
                .GroupBy(c => c.Name.Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
            var tagByName2 = promptSlice.Tags
                .GroupBy(t => t.Name.Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
            var matByName2 = promptSlice.Materials
                .GroupBy(m => m.Name.Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            result.SuggestedCategories = RecoverAndValidateCategories(result.SuggestedCategories, validCatIds2, catByPath2, catByName2, 3);
            result.SuggestedTags = RecoverAndValidateTags(result.SuggestedTags, validTagIds2, tagByName2, 8);
            result.SuggestedMaterials = RecoverAndValidateMaterials(result.SuggestedMaterials, validMatIds2, matByName2, 3);

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image analysis failed for seller {SellerId}", sellerId);
            return new AnalyzeImageResponseDto { Success = false, ErrorMessage = "Đã xảy ra lỗi khi phân tích ảnh." };
        }
    }

    // ── Phân tích sản phẩm (text-only, 1 Gemini call) ────────────────────────────
    public async Task<AnalyzeProductResponseDto> AnalyzeProductAsync(AnalyzeProductRequestDto request, Guid sellerId)
    {
        var candidates = await GetPromptCandidatesAsync();
        var catById = candidates.CatById;

        string BuildPath(long id)
        {
            var parts = new List<string>();
            var curId = (long?)id;
            while (curId.HasValue && catById.TryGetValue(curId.Value, out var cat))
            {
                parts.Insert(0, cat.Name);
                curId = cat.ParentId;
            }
            return string.Join(" > ", parts);
        }

        var categoryHint = string.Empty;
        if (request.CategoryId.HasValue && catById.TryGetValue(request.CategoryId.Value, out var hintCat))
            categoryHint = $"\nNgười dùng đã chọn category: {BuildPath(hintCat.Id)} — dùng đây làm ngữ cảnh để chọn tags và materials phù hợp.\n";

        var promptSlice = NarrowCatalogForPrompt(
            candidates,
            BuildPath,
            request.Title,
            request.Description,
            preferredCategoryId: request.CategoryId,
            maxCategories: MaxPromptCategories,
            maxTags: MaxPromptTags,
            maxMaterials: MaxPromptMaterials);

        var catLines = string.Join("\n", promptSlice.Categories.Select(c => $"ID:{c.Id} | {BuildPath(c.Id)} | Cấp {c.Level}"));
        var tagLines = string.Join("\n", promptSlice.Tags.Select(t => $"ID:{t.Id} | {t.Name}"));
        var matLines = string.Join("\n", promptSlice.Materials.Select(m => $"ID:{m.Id} | {m.Name}"));

        var tagHistoryHint = await BuildSellerTagHistoryHintAsync(sellerId);
        var matNameById = candidates.AllMaterials.ToDictionary(m => m.Id, m => m.Name);
        var matHistoryHint = await BuildSellerMaterialHistoryHintAsync(sellerId, matNameById);
        const string sellerHabitBalanceNote = """

            Khi chọn tags và materials: ưu tiên đúng với tên/mô tả sản phẩm; có thể ưu tiên các tag/chất liệu trùng thói quen shop (nếu có ở trên) khi thật sự phù hợp — không ép lặp lại nếu sản phẩm khác loại.
            """;

        var userMessage = $"""
            Phân tích sản phẩm dưới đây và trả về category, tags, materials phù hợp nhất.

            Tên sản phẩm: {request.Title}
            Mô tả: {request.Description ?? "Không có"}
            {categoryHint}
            === DANH SÁCH CATEGORY (sắp theo cấp sâu nhất trước) ===
            {catLines}

            === DANH SÁCH TAGS ===
            {tagLines}{tagHistoryHint}

            === DANH SÁCH MATERIALS ===
            {matLines}{matHistoryHint}{sellerHabitBalanceNote}

            Yêu cầu trả về:
            - categories: top 3 category phù hợp, ưu tiên leaf node (cấp sâu nhất), chỉ dùng ID từ danh sách, confidenceScore 0.0–1.0
            - tags: tối đa 10 tags mô tả chính xác sản phẩm, chỉ dùng ID từ danh sách
            - materials: tối đa 5 chất liệu khi biết chắc chắn sản phẩm có chất liệu đó, chỉ dùng ID từ danh sách (GUID)
            """;

        try
        {
            var raw = await _gemini.GenerateJsonAsync(_sellerPrompt.Value, userMessage, _analyzeProductSchema);

            if (raw.StartsWith("⚠️"))
                return new AnalyzeProductResponseDto { Success = false, ErrorMessage = raw };

            var result = ParseJsonResponseSnake<AnalyzeProductResponseDto>(raw, "AnalyzeProduct");
            if (result == null)
                return new AnalyzeProductResponseDto { Success = false, ErrorMessage = "Không thể xử lý phản hồi từ AI. Vui lòng thử lại." };

            // Post-validate within the candidate set that was shown to the model
            var validCatIds = new HashSet<long>(promptSlice.Categories.Select(c => c.Id));
            var validTagIds = new HashSet<long>(promptSlice.Tags.Select(t => t.Id));
            var validMatIds = new HashSet<Guid>(promptSlice.Materials.Select(m => m.Id));

            var catByPath = promptSlice.Categories
                .GroupBy(c => BuildPath(c.Id).Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
            var catByName = promptSlice.Categories
                .GroupBy(c => c.Name.Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
            var tagByName = promptSlice.Tags
                .GroupBy(t => t.Name.Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
            var matByName = promptSlice.Materials
                .GroupBy(m => m.Name.Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            result.Categories = RecoverAndValidateCategories(result.Categories, validCatIds, catByPath, catByName, 3);
            result.Tags = RecoverAndValidateTags(result.Tags, validTagIds, tagByName, 10);
            result.Materials = RecoverAndValidateMaterials(result.Materials, validMatIds, matByName, 5);

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AnalyzeProduct failed for seller {SellerId}", sellerId);
            return new AnalyzeProductResponseDto { Success = false, ErrorMessage = "Đã xảy ra lỗi khi phân tích. Vui lòng thử lại." };
        }
    }

    private T? ParseJsonResponse<T>(string raw, string operation)
    {
        try
        {
            var json = NormalizeModelJson(raw);
            return JsonSerializer.Deserialize<T>(json, _jsonReadOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Operation} JSON parse failed. Raw snippet: {Raw}", operation, raw.Length > 400 ? raw[..400] : raw);
            return default;
        }
    }

    /// <summary>Parse JSON từ Gemini JSON-mode (snake_case schema).</summary>
    private T? ParseJsonResponseSnake<T>(string raw, string operation)
    {
        try
        {
            // JSON-mode của Gemini trả ra JSON thuần, không cần normalize
            return JsonSerializer.Deserialize<T>(raw, _jsonSnakeReadOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Operation} JSON-mode parse failed. Raw snippet: {Raw}", operation, raw.Length > 400 ? raw[..400] : raw);
            return default;
        }
    }

    private static string NormalizeModelJson(string raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text)) return text;

        // Strip fenced markdown blocks if present.
        if (text.StartsWith("```"))
        {
            var firstNewLine = text.IndexOf('\n');
            if (firstNewLine >= 0)
            {
                text = text[(firstNewLine + 1)..];
            }

            var fenceEnd = text.LastIndexOf("```");
            if (fenceEnd >= 0)
            {
                text = text[..fenceEnd];
            }
        }

        text = text.Trim();
        if (text.StartsWith("json", StringComparison.OrdinalIgnoreCase))
        {
            text = text[4..].Trim();
        }

        // Extract the first JSON object when model wraps JSON with prose.
        var firstBrace = text.IndexOf('{');
        var lastBrace = text.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            text = text[firstBrace..(lastBrace + 1)];
        }

        return text.Trim();
    }

    // ── Post-validation helpers: recover hallucinated IDs by matching on name ────

    private static List<CategorySuggestionItem> RecoverAndValidateCategories(
        IEnumerable<CategorySuggestionItem> raw,
        HashSet<long> validIds,
        Dictionary<string, long> byPath,
        Dictionary<string, long> byName,
        int maxCount = 3)
    {
        var result = new List<CategorySuggestionItem>();
        var seen = new HashSet<long>();

        foreach (var c in raw.OrderByDescending(x => x.ConfidenceScore))
        {
            if (!validIds.Contains(c.CategoryId))
            {
                var path = (c.CategoryPath ?? string.Empty).Trim().ToLowerInvariant();
                var name = (c.CategoryName ?? string.Empty).Trim().ToLowerInvariant();

                if (path.Length > 0 && byPath.TryGetValue(path, out var recoveredByPath))
                    c.CategoryId = recoveredByPath;
                else if (name.Length > 0 && byName.TryGetValue(name, out var recoveredByName))
                    c.CategoryId = recoveredByName;
                else
                    continue;
            }

            if (!seen.Add(c.CategoryId)) continue;
            result.Add(c);
            if (result.Count >= maxCount) break;
        }

        return result;
    }

    private static List<TagSuggestionItem> RecoverAndValidateTags(
        IEnumerable<TagSuggestionItem> raw,
        HashSet<long> validIds,
        Dictionary<string, long> byName,
        int maxCount = 10)
    {
        var result = new List<TagSuggestionItem>();
        var seen = new HashSet<long>();

        foreach (var t in raw.OrderByDescending(x => x.ConfidenceScore))
        {
            var id = t.TagId ?? 0;
            if (!validIds.Contains(id))
            {
                var name = (t.TagName ?? string.Empty).Trim().ToLowerInvariant();
                if (name.Length == 0 || !byName.TryGetValue(name, out id)) continue;
                t.TagId = id;
            }

            if (!seen.Add(id)) continue;
            result.Add(t);
            if (result.Count >= maxCount) break;
        }

        return result;
    }

    private static List<MaterialSuggestionItem> RecoverAndValidateMaterials(
        IEnumerable<MaterialSuggestionItem> raw,
        HashSet<Guid> validIds,
        Dictionary<string, Guid> byName,
        int maxCount = 5)
    {
        var result = new List<MaterialSuggestionItem>();
        var seen = new HashSet<Guid>();

        foreach (var m in raw.OrderByDescending(x => x.ConfidenceScore))
        {
            var id = m.MaterialId ?? Guid.Empty;
            if (id == Guid.Empty || !validIds.Contains(id))
            {
                var name = (m.MaterialName ?? string.Empty).Trim().ToLowerInvariant();
                if (name.Length == 0 || !byName.TryGetValue(name, out id)) continue;
                m.MaterialId = id;
            }

            if (!seen.Add(id)) continue;
            result.Add(m);
            if (result.Count >= maxCount) break;
        }

        return result;
    }

    private static AnalyzeImageResponseDto NormalizeAnalyzeImageResult(AnalyzeImageResponseDto result)
    {
        result.Quality ??= new ImageQualityDto();
        result.SuggestedCategories ??= new List<CategorySuggestionItem>();
        result.SuggestedTags ??= new List<TagSuggestionItem>();
        result.SuggestedMaterials ??= new List<MaterialSuggestionItem>();
        result.Improvements ??= new List<string>();
        result.Summary ??= string.Empty;

        result.Quality.Score = Math.Clamp(result.Quality.Score, 1, 10);
        result.Quality.Rating = string.IsNullOrWhiteSpace(result.Quality.Rating)
            ? "fair"
            : result.Quality.Rating.Trim().ToLowerInvariant();

        result.SuggestedCategories = result.SuggestedCategories
            .Where(x => x.CategoryId > 0)
            .OrderByDescending(x => x.ConfidenceScore)
            .Take(3)
            .ToList();

        result.SuggestedTags = result.SuggestedTags
            .Where(x => !string.IsNullOrWhiteSpace(x.TagName))
            .OrderByDescending(x => x.ConfidenceScore)
            .Take(8)
            .ToList();

        result.SuggestedMaterials = result.SuggestedMaterials
            .Where(x => !string.IsNullOrWhiteSpace(x.MaterialName))
            .OrderByDescending(x => x.ConfidenceScore)
            .Take(3)
            .ToList();

        result.Improvements = result.Improvements
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        if (result.Improvements.Count == 0)
        {
            result.Improvements.Add("Nên bổ sung ảnh rõ nét hơn để tăng độ tin cậy khi phân tích.");
        }

        return result;
    }

    private static object BuildAnalyzeImageSchema()
    {
        return new
        {
            type = "OBJECT",
            properties = new
            {
                quality = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        score = new { type = "INTEGER" },
                        rating = new { type = "STRING", @enum = new[] { "excellent", "good", "fair", "poor" } },
                        has_good_lighting = new { type = "BOOLEAN" },
                        has_clean_background = new { type = "BOOLEAN" },
                        is_product_centered = new { type = "BOOLEAN" },
                        has_high_resolution = new { type = "BOOLEAN" }
                    },
                    required = new[] { "score", "rating", "has_good_lighting", "has_clean_background", "is_product_centered", "has_high_resolution" }
                },
                suggested_categories = new
                {
                    type = "ARRAY",
                    items = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            category_id = new { type = "INTEGER" },
                            category_name = new { type = "STRING" },
                            category_path = new { type = "STRING" },
                            confidence_score = new { type = "NUMBER" }
                        },
                        required = new[] { "category_id", "category_name", "category_path", "confidence_score" }
                    }
                },
                suggested_tags = new
                {
                    type = "ARRAY",
                    items = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            tag_id = new { type = "INTEGER" },
                            tag_name = new { type = "STRING" },
                            confidence_score = new { type = "NUMBER" }
                        },
                        required = new[] { "tag_id", "tag_name", "confidence_score" }
                    }
                },
                suggested_materials = new
                {
                    type = "ARRAY",
                    items = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            material_id = new { type = "STRING" },
                            material_name = new { type = "STRING" },
                            confidence_score = new { type = "NUMBER" }
                        },
                        required = new[] { "material_id", "material_name", "confidence_score" }
                    }
                },
                improvements = new
                {
                    type = "ARRAY",
                    items = new { type = "STRING" }
                },
                summary = new { type = "STRING" }
            },
            required = new[] { "quality", "suggested_categories", "suggested_tags", "suggested_materials", "improvements", "summary" }
        };
    }

    /// <summary>
    /// Schema cho AnalyzeProductAsync (JSON mode, text-only).
    /// Properties dùng PascalCase C# → SnakeCaseLower serializer trong GeminiClientService sẽ serialize thành snake_case khi gửi lên API.
    /// Response được parse với _jsonSnakeReadOptions (SnakeCaseLower).
    /// </summary>
    private static object BuildAnalyzeProductSchema()
    {
        return new
        {
            Type = "OBJECT",
            Properties = new
            {
                Categories = new
                {
                    Type = "ARRAY",
                    Items = new
                    {
                        Type = "OBJECT",
                        Properties = new
                        {
                            CategoryId = new { Type = "INTEGER" },
                            CategoryName = new { Type = "STRING" },
                            CategoryPath = new { Type = "STRING" },
                            ConfidenceScore = new { Type = "NUMBER" }
                        },
                        Required = new[] { "category_id", "category_name", "category_path", "confidence_score" }
                    }
                },
                Tags = new
                {
                    Type = "ARRAY",
                    Items = new
                    {
                        Type = "OBJECT",
                        Properties = new
                        {
                            TagId = new { Type = "INTEGER" },
                            TagName = new { Type = "STRING" },
                            ConfidenceScore = new { Type = "NUMBER" }
                        },
                        Required = new[] { "tag_id", "tag_name", "confidence_score" }
                    }
                },
                Materials = new
                {
                    Type = "ARRAY",
                    Items = new
                    {
                        Type = "OBJECT",
                        Properties = new
                        {
                            MaterialId = new { Type = "STRING" },
                            MaterialName = new { Type = "STRING" },
                            ConfidenceScore = new { Type = "NUMBER" }
                        },
                        Required = new[] { "material_id", "material_name", "confidence_score" }
                    }
                }
            },
            Required = new[] { "categories", "tags", "materials" }
        };
    }

    private static string LoadPromptFromFile(string fileName, string fallback)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Prompts", fileName);
            return File.Exists(path) ? File.ReadAllText(path) : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>
    /// Gemini đôi khi trả material_id không phải GUID hợp lệ; converter này hạ xuống null thay vì ném lỗi.
    /// </summary>
    private sealed class SafeNullableGuidConverter : JsonConverter<Guid?>
    {
        public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;

                case JsonTokenType.String:
                    var raw = reader.GetString();
                    return Guid.TryParse(raw, out var guid) ? guid : null;

                case JsonTokenType.StartArray:
                case JsonTokenType.StartObject:
                    reader.Skip();
                    return null;

                default:
                    return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value);
                return;
            }

            writer.WriteNullValue();
        }
    }
}
