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
