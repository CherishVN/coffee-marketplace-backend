using System.Text.Json;
using ECommerceAI.Data;
using ECommerceAI.Data.Entities;
using ECommerceAI.DTOs.Seller;
using ECommerceAI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAI.Services;

public class AiSellerService : IAiSellerService
{
    private readonly AiDbContext _context;
    private readonly GeminiClientService _gemini;
    private readonly ILogger<AiSellerService> _logger;
    private readonly string _systemPrompt;

    public AiSellerService(AiDbContext context, GeminiClientService gemini, ILogger<AiSellerService> logger)
    {
        _context = context;
        _gemini = gemini;
        _logger = logger;

        var promptPath = Path.Combine(AppContext.BaseDirectory, "Prompts", "SellerSuggestPrompt.txt");
        _systemPrompt = File.Exists(promptPath) ? File.ReadAllText(promptPath) : "Bạn là AI hỗ trợ seller.";
    }

    // ── Gợi ý Category ───────────────────────────────────────────────────────
    public async Task<SuggestCategoryResponseDto> SuggestCategoryAsync(SuggestCategoryRequestDto request, Guid sellerId)
    {
        // Lấy danh sách categories từ DB
        var categories = await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Level).ThenBy(c => c.Name)
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
            var raw = await _gemini.GenerateAsync(_systemPrompt, userMessage);
            var result = ParseJsonResponse<SuggestCategoryResponseDto>(raw) ?? new SuggestCategoryResponseDto();
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
            var raw = await _gemini.GenerateAsync(_systemPrompt, userMessage);
            var result = ParseJsonResponse<SuggestTagsResponseDto>(raw) ?? new SuggestTagsResponseDto();

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
                        Action = "pending",
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
            """;

        try
        {
            var raw = await _gemini.GenerateAsync(_systemPrompt, userMessage);
            return ParseJsonResponse<SuggestMaterialsResponseDto>(raw) ?? new SuggestMaterialsResponseDto();
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
            .Select(c => new { c.Id, c.Name, c.Level })
            .ToListAsync();

        var tags = await _context.Tags
            .OrderBy(t => t.Name)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        var materials = await _context.Materials
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .Select(m => new { m.Id, m.Name })
            .ToListAsync();

        var categoryList = string.Join(", ", categories.Select(c => $"{c.Name}(ID:{c.Id})"));
        var tagList = string.Join(", ", tags.Select(t => $"{t.Name}(ID:{t.Id})"));
        var materialList = string.Join(", ", materials.Select(m => $"{m.Name}(ID:{m.Id})"));

        var promptPath = Path.Combine(AppContext.BaseDirectory, "Prompts", "ImageAnalysisPrompt.txt");
        var imagePrompt = File.Exists(promptPath) ? File.ReadAllText(promptPath) : _systemPrompt;

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
            var raw = await _gemini.GenerateWithImagesAsync(imagePrompt, userMessage, request.ImageUrls);

            if (raw.StartsWith("⚠️"))
                return new AnalyzeImageResponseDto { Success = false, ErrorMessage = raw };

            var result = ParseJsonResponse<AnalyzeImageResponseDto>(raw);
            if (result == null)
                return new AnalyzeImageResponseDto { Success = false, ErrorMessage = "Không thể xử lý phản hồi từ AI. Vui lòng thử lại." };

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image analysis failed for seller {SellerId}", sellerId);
            return new AnalyzeImageResponseDto { Success = false, ErrorMessage = "Đã xảy ra lỗi khi phân tích ảnh." };
        }
    }

    private static T? ParseJsonResponse<T>(string raw)
    {
        try
        {
            var json = raw.Trim().TrimStart('`').TrimEnd('`');
            if (json.StartsWith("json")) json = json[4..].Trim();
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return default;
        }
    }
}
