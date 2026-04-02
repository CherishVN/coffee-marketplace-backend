using System.Text.Json;
using System.Text.Json.Serialization;
using ECommerceAI.Data;
using ECommerceAI.Data.Entities;
using ECommerceAI.DTOs.Seller;
using ECommerceAI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAI.Services;

public class AiSellerService : IAiSellerService
{
    private const string DefaultSystemPrompt = "Bạn là AI hỗ trợ seller.";
    private const string ActionPending = "pending";
    private const int MaxPromptCategories = 150;
    private const int MaxPromptTags = 200;
    private const int MaxPromptMaterials = 150;

    /// <summary>Parse JSON từ Gemini: model trả camelCase (categoryId, tagName, …). SnakeCaseLower sẽ không map → toàn 0/rỗng.</summary>
    private static readonly JsonSerializerOptions _jsonReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new SafeNullableGuidConverter() }
    };

    private static readonly Lazy<string> _sellerPrompt = new(() => LoadPromptFromFile("SellerSuggestPrompt.txt", DefaultSystemPrompt));
    private static readonly Lazy<string> _imagePrompt = new(() => LoadPromptFromFile("ImageAnalysisPrompt.txt", DefaultSystemPrompt));
    private static readonly object _analyzeImageSchema = BuildAnalyzeImageSchema();

    private readonly AiDbContext _context;
    private readonly GeminiClientService _gemini;
    private readonly ILogger<AiSellerService> _logger;

    public AiSellerService(AiDbContext context, GeminiClientService gemini, ILogger<AiSellerService> logger)
    {
        _context = context;
        _gemini = gemini;
        _logger = logger;
    }

    // ── Gợi ý Category ───────────────────────────────────────────────────────
    public async Task<SuggestCategoryResponseDto> SuggestCategoryAsync(SuggestCategoryRequestDto request, Guid sellerId)
    {
        // Lấy danh sách categories từ DB
        var categories = await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Level).ThenBy(c => c.Name)
            .Take(MaxPromptCategories)
            .ToListAsync();

        var categoryList = string.Join("\n", categories.Select(c =>
            $"ID:{c.Id} | {new string('-', c.Level)}{c.Name} (Level {c.Level})"));

        var jsonExample = """{"suggestions":[{"categoryId":123,"categoryName":"Tên category","categoryPath":"Cha > Con","confidenceScore":0.95}]}""";
        var userMessage = $"""
            Phân tích sản phẩm sau và gợi ý top 3 category phù hợp nhất:
            
            Tên sản phẩm: {request.Title}
            Mô tả: {request.Description ?? "Không có"}
            
            Danh sách category có trong hệ thống:
            {categoryList}
            
            Trả về JSON theo format: {jsonExample}
            (gồm đúng 3 suggestions với categoryId, categoryName, categoryPath, confidenceScore)
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
        var tags = await _context.Tags.OrderBy(t => t.Name).ToListAsync();
        var tagList = string.Join(", ", tags.Select(t => $"{t.Name}(ID:{t.Id})"));

        var tagJsonExample = """{"suggestions":[{"tagId":1,"tagName":"Tên tag","confidenceScore":0.95}]}""";
        var userMessage = $"""
            Gợi ý tags phù hợp cho sản phẩm sau (chọn tối đa 10 tags):
            
            Tên: {request.Title}
            Mô tả: {request.Description ?? "Không có"}
            
            Tags có trong hệ thống: {tagList}
            
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
                        result.Suggestions.Select(s => new { tagId = s.TagId, tagName = s.TagName, score = s.ConfidenceScore }));

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

    // ── Lưu phản hồi sau khi seller chọn tags ────────────────────────────────
    public async Task<bool> SaveTagSuggestionFeedbackAsync(SaveSuggestionFeedbackDto dto, Guid sellerId)
    {
        var log = await _context.AiTagSuggestions
            .FirstOrDefaultAsync(s => s.Id == dto.LogId && s.SellerId == sellerId);

        if (log == null) return false;

        var chosenJson = JsonSerializer.Serialize(dto.ChosenTagIds ?? new List<long>());
        log.ChosenCategoryId = dto.ChosenCategoryId;
        log.ChosenTags = JsonDocument.Parse(chosenJson);
        log.Action = dto.Action;

        await _context.SaveChangesAsync();
        return true;
    }

    // ── Gợi ý Materials ──────────────────────────────────────────────────────
    public async Task<SuggestMaterialsResponseDto> SuggestMaterialsAsync(SuggestMaterialsRequestDto request, Guid sellerId)
    {
        var materials = await _context.Materials
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .ToListAsync();

        var materialList = string.Join(", ", materials.Select(m => $"{m.Name}(ID:{m.Id})"));

        var matJsonExample = """{"suggestions":[{"materialId":"uuid-here","materialName":"Tên chất liệu","confidenceScore":0.95}]}""";
        var userMessage = $"""
            Gợi ý chất liệu (materials) phù hợp cho sản phẩm sau:
            
            Tên: {request.Title}
            Mô tả: {request.Description ?? "Không có"}
            
            Materials có trong hệ thống: {materialList}
            
            Trả về JSON theo format: {matJsonExample}
            
            BẮT BUỘC: với mỗi gợi ý, materialId phải là đúng GUID trong danh sách (phần sau "ID:"), trùng với materialName — không được bỏ trống hoặc tự bịa UUID.
            """;

        try
        {
            var raw = await _gemini.GenerateAsync(_sellerPrompt.Value, userMessage);
            return ParseJsonResponse<SuggestMaterialsResponseDto>(raw, "SuggestMaterials") ?? new SuggestMaterialsResponseDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Material suggestion failed for seller {SellerId}", sellerId);
            return new SuggestMaterialsResponseDto();
        }
    }

    // ── Phân tích ảnh sản phẩm (Gemini Vision) ───────────────────────────────
    public async Task<AnalyzeImageResponseDto> AnalyzeImageAsync(AnalyzeImageRequestDto request, Guid sellerId)
    {
        if (request.ImageUrls == null || request.ImageUrls.Count == 0)
            return new AnalyzeImageResponseDto { Success = false, ErrorMessage = "Vui lòng cung cấp ít nhất 1 URL ảnh." };

        if (request.ImageUrls.Count > 3)
            request.ImageUrls = request.ImageUrls.Take(3).ToList();

        // Lấy danh sách categories, tags, materials từ DB để AI gợi ý chính xác hơn
        var categories = await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Level).ThenBy(c => c.Name)
            .Take(MaxPromptCategories)
            .Select(c => new { c.Id, c.Name, c.Level })
            .ToListAsync();

        var tags = await _context.Tags
            .OrderBy(t => t.Name)
            .Take(MaxPromptTags)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        var materials = await _context.Materials
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .Take(MaxPromptMaterials)
            .Select(m => new { m.Id, m.Name })
            .ToListAsync();

        var categoryList = string.Join(", ", categories.Select(c => $"{c.Name}(ID:{c.Id})"));
        var tagList = string.Join(", ", tags.Select(t => $"{t.Name}(ID:{t.Id})"));
        var materialList = string.Join(", ", materials.Select(m => $"{m.Name}(ID:{m.Id})"));

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
            
            Danh sách categories có trong hệ thống: {categoryList}
            Danh sách tags có trong hệ thống: {tagList}
            Danh sách materials có trong hệ thống: {materialList}
            
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

            var result = ParseJsonResponse<AnalyzeImageResponseDto>(raw, "AnalyzeImage");
            if (result == null)
            {
                _logger.LogWarning("AnalyzeImage parse failed. Raw snippet: {Raw}", raw.Length > 400 ? raw[..400] : raw);
                return new AnalyzeImageResponseDto { Success = false, ErrorMessage = "Không thể xử lý phản hồi từ AI. Vui lòng thử lại." };
            }

            result = NormalizeAnalyzeImageResult(result);
            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image analysis failed for seller {SellerId}", sellerId);
            return new AnalyzeImageResponseDto { Success = false, ErrorMessage = "Đã xảy ra lỗi khi phân tích ảnh." };
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
