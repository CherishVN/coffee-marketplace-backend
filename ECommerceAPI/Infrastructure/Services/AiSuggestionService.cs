using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ECommerceAPI.Application.DTOs.AI;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace ECommerceAPI.Infrastructure.Services;

/// <summary>
/// Service gọi AI Microservice để lấy gợi ý category, tags, materials
/// </summary>
public class AiSuggestionService : IAiSuggestionService
{
    private readonly HttpClient _httpClient;
    private readonly AiServiceSettings _settings;
    private readonly ILogger<AiSuggestionService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AiSuggestionService(
        HttpClient httpClient,
        IOptions<AiServiceSettings> settings,
        ILogger<AiSuggestionService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

        // Add API Key if configured
        if (!string.IsNullOrEmpty(_settings.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _settings.ApiKey);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task<AiCategorySuggestionResponse?> SuggestCategoryAsync(
        AiCategorySuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calling AI service for category suggestion: {Title}", request.Title);

            var response = await PostAsync<AiCategorySuggestionRequest, AiCategorySuggestionResponse>(
                _settings.Endpoints.CategorySuggestion,
                request,
                cancellationToken);

            _logger.LogInformation(
                "AI category suggestion completed. Suggested: {CategoryName} with confidence: {Confidence}",
                response?.SuggestedCategoryName,
                response?.ConfidenceScore);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for category suggestion");
            return null;
        }
    }

    public async Task<AiTagSuggestionResponse?> SuggestTagsAsync(
        AiTagSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calling AI service for tag suggestion: {Title}", request.Title);

            var response = await PostAsync<AiTagSuggestionRequest, AiTagSuggestionResponse>(
                _settings.Endpoints.TagSuggestion,
                request,
                cancellationToken);

            _logger.LogInformation(
                "AI tag suggestion completed. {Count} tags suggested",
                response?.SuggestedTags?.Count ?? 0);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for tag suggestion");
            return null;
        }
    }

    public async Task<AiMaterialSuggestionResponse?> SuggestMaterialsAsync(
        AiMaterialSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calling AI service for material suggestion: {Title}", request.Title);

            var response = await PostAsync<AiMaterialSuggestionRequest, AiMaterialSuggestionResponse>(
                _settings.Endpoints.MaterialSuggestion,
                request,
                cancellationToken);

            _logger.LogInformation(
                "AI material suggestion completed. {Count} materials suggested",
                response?.SuggestedMaterials?.Count ?? 0);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service for material suggestion");
            return null;
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "AI service returned status code: {StatusCode} for endpoint: {Endpoint}",
                response.StatusCode,
                endpoint);
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<TResponse>(responseJson, _jsonOptions);
    }
}
