using ECommerceAI.Data;
using ECommerceAI.Middleware;
using ECommerceAI.Services;
using ECommerceAI.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── AI Services ───────────────────────────────────────────────────────────────
builder.Services.AddSingleton<GeminiClientService>();
builder.Services.AddScoped<IAiChatService, AiChatService>();
builder.Services.AddScoped<IAiSellerService, AiSellerService>();
builder.Services.AddScoped<IAiAdminService, AiAdminService>();

// ── HTTP Client cho Gemini API ────────────────────────────────────────────────
builder.Services.AddHttpClient("GeminiClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(35);
});

// ── HTTP Client để gọi Main API ───────────────────────────────────────────────
builder.Services.AddHttpClient("MainApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["MainApi:BaseUrl"] ?? "http://localhost:5000");
    client.DefaultRequestHeaders.Add("X-Internal-Key", builder.Configuration["InternalAuth:ApiKey"]);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ── Authentication (dùng chung JWT Supabase với Main API) ─────────────────────
var supabaseUrl = builder.Configuration["Supabase:Url"]!;
var jwksUrl = $"{supabaseUrl}/auth/v1/.well-known/jwks.json";

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
                var httpClient = new HttpClient();
                var jwks = httpClient.GetStringAsync(jwksUrl).Result;
                var keys = new Microsoft.IdentityModel.Tokens.JsonWebKeySet(jwks);
                return keys.Keys;
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

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMainApi", policy =>
    {
        policy.WithOrigins(
                builder.Configuration["MainApi:BaseUrl"] ?? "http://localhost:5000",
                "http://localhost:3000")   // Frontend
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<InternalApiKeyMiddleware>();
app.UseCors("AllowMainApi");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
