using ECommerceAI.Data;
using ECommerceAI.Middleware;
using ECommerceAI.Services;
using ECommerceAI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// Npgsql: treat DateTime Kind=Unspecified as UTC (tránh lỗi khi client gửi date không có timezone)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AiDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure(3)));

// ── In-memory cache (dùng cho JWKS keys và catalog candidates) ───────────────
builder.Services.AddMemoryCache();

// ── AI Services ───────────────────────────────────────────────────────────────
// "default" → Admin & Customer dùng model gemini-3.1-flash-lite-preview (section "Gemini")
builder.Services.AddKeyedSingleton<GeminiClientService>("default", (sp, _) =>
    new GeminiClientService(
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<ILogger<GeminiClientService>>(),
        sp.GetRequiredService<IHttpClientFactory>(),
        configSection: "Gemini"));

// "seller" → Seller dùng model gemini-2.5-flash-lite (section "GeminiSeller")
builder.Services.AddKeyedSingleton<GeminiClientService>("seller", (sp, _) =>
    new GeminiClientService(
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<ILogger<GeminiClientService>>(),
        sp.GetRequiredService<IHttpClientFactory>(),
        configSection: "GeminiSeller"));

builder.Services.AddScoped<IAiChatService, AiChatService>();
builder.Services.AddScoped<IAiSellerService, AiSellerService>();
builder.Services.AddScoped<IAiAdminService, AiAdminService>();

// ── HTTP Client cho Gemini API (Timeout >= ImageTimeout * MaxHttpAttempts + dự phòng) ──
builder.Services.AddHttpClient("GeminiClient", (sp, client) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var httpSec = cfg.GetValue("Gemini:HttpClientTimeoutSeconds", 0);
    if (httpSec <= 0)
    {
        var img = cfg.GetValue("Gemini:ImageTimeoutSeconds", 150);
        var maxA = cfg.GetValue("Gemini:MaxHttpAttempts", 3);
        httpSec = img * maxA + 60;
    }
    client.Timeout = TimeSpan.FromSeconds(httpSec);
});

// ── HTTP Client để gọi Main API ───────────────────────────────────────────────
builder.Services.AddHttpClient("MainApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["MainApi:BaseUrl"] ?? "http://localhost:5153");
    client.DefaultRequestHeaders.Add("X-Internal-Key", builder.Configuration["InternalAuth:ApiKey"]);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ── Authentication (dùng chung JWT Supabase với Main API) ─────────────────────
var supabaseUrl = builder.Configuration["Supabase:Url"]!;
var jwksUrl = $"{supabaseUrl}/auth/v1/.well-known/jwks.json";

// Cache JWKS keys 10 phút — tránh gọi Supabase mỗi request
IList<SecurityKey>? cachedJwksKeys = null;
DateTime jwksCachedAt = DateTime.MinValue;
object jwksLock = new();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = $"{supabaseUrl}/auth/v1",
            ValidAudience = "authenticated",
            ClockSkew = TimeSpan.FromMinutes(5),
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
            {
                lock (jwksLock)
                {
                    if (cachedJwksKeys != null && (DateTime.UtcNow - jwksCachedAt).TotalMinutes < 10)
                        return cachedJwksKeys;
                }

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var jwks = httpClient.GetStringAsync(jwksUrl).GetAwaiter().GetResult();
                IList<SecurityKey> keys = new Microsoft.IdentityModel.Tokens.JsonWebKeySet(jwks)
                    .Keys.Cast<SecurityKey>().ToList();

                lock (jwksLock)
                {
                    cachedJwksKeys = keys;
                    jwksCachedAt = DateTime.UtcNow;
                }

                return keys;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ── Swagger ───────────────────────────────────────────────────────────────────
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "E-Commerce AI Service",
        Version = "v1",
        Description = "AI Microservice - Chat Assistant, Seller Suggestions, Admin Analytics"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
var frontendUrl = builder.Configuration["FrontendUrl"];
var mainApiUrl = builder.Configuration["MainApi:BaseUrl"];

var allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

foreach (var origin in corsOrigins)
{
    if (Uri.TryCreate(origin, UriKind.Absolute, out var u)) allowedHosts.Add(u.Host);
}
if (!string.IsNullOrEmpty(frontendUrl) && Uri.TryCreate(frontendUrl, UriKind.Absolute, out var fUri))
{
    allowedHosts.Add(fUri.Host);
}
if (!string.IsNullOrEmpty(mainApiUrl) && Uri.TryCreate(mainApiUrl, UriKind.Absolute, out var mUri))
{
    allowedHosts.Add(mUri.Host);
}

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMainApi", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                string host = uri.Host.ToLowerInvariant();
                
                // Luôn cho phép localhost (dev) và các nhánh preview của Vercel
                if (host == "localhost" || host == "127.0.0.1" || host.EndsWith(".vercel.app")) 
                {
                    return true;
                }

                // Cho phép dựa trên cấu hình lấy từ appsettings.json hoặc Environment Variables
                return allowedHosts.Contains(host);
            })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

var enableSwagger = app.Environment.IsDevelopment()
    || app.Configuration.GetValue<bool>("EnableSwagger");
if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.Use(async (context, next) =>
    {
        if (HttpMethods.IsGet(context.Request.Method))
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (path is "/" or "")
            {
                context.Response.Redirect("/swagger/index.html");
                return;
            }
        }

        await next();
    });
}

app.UseMiddleware<InternalApiKeyMiddleware>();
app.UseCors("AllowMainApi");
app.UseAuthentication();
app.UseMiddleware<UserRoleSyncMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();
