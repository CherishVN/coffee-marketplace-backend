using System.Text;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Application.Services;
using ECommerceAPI.Infrastructure.Configuration;
using ECommerceAPI.Infrastructure.Data;
using ECommerceAPI.Infrastructure.Repositories;
using ECommerceAPI.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace ECommerceAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Database
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Configuration Settings
            builder.Services.Configure<AiServiceSettings>(
                builder.Configuration.GetSection(AiServiceSettings.SectionName));

            // Repositories
            builder.Services.AddScoped<IUserRepository, UserRepository>();

            // Services
            builder.Services.AddScoped<IJwtService, JwtService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IUserAdminService, UserAdminService>();
            builder.Services.AddScoped<IWithdrawAdminService, WithdrawAdminService>();
            builder.Services.AddScoped<ISellerApprovalService, SellerApprovalService>();
            builder.Services.AddScoped<ICategoryAdminService, CategoryAdminService>();
            builder.Services.AddScoped<ITagAdminService, TagAdminService>();
            builder.Services.AddScoped<IProductModerationService, ProductModerationService>();
            builder.Services.AddScoped<IDisputeAdminService, DisputeAdminService>();
            builder.Services.AddScoped<IDashboardService, DashboardService>();

            // AI Service - HTTP Client
            builder.Services.AddHttpClient<IAiSuggestionService, AiSuggestionService>();

            var jwtSettings = builder.Configuration.GetSection("Jwt");
            var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");
            var supabaseJwtSecret = builder.Configuration["Supabase:JwtSecret"] ?? throw new InvalidOperationException("Supabase JwtSecret is not configured");
            var supabaseUrl = builder.Configuration["Supabase:Url"]!;

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "MultiAuth";
                options.DefaultChallengeScheme = "MultiAuth";
            })
            .AddJwtBearer("Backend", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ClockSkew = TimeSpan.Zero
                };
            })
            .AddJwtBearer("Supabase", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = $"{supabaseUrl}/auth/v1",
                    ValidAudience = "authenticated",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(supabaseJwtSecret)),
                    ClockSkew = TimeSpan.Zero
                };
            })
            .AddPolicyScheme("MultiAuth", "Backend or Supabase", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                        return "Backend";

                    var token = authHeader.Substring("Bearer ".Length);
                    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();

                    try
                    {
                        var jwt = handler.ReadJwtToken(token);
                        if (jwt.Issuer.Contains("supabase"))
                            return "Supabase";
                    }
                    catch { }

                    return "Backend";
                };
            });

            builder.Services.AddAuthorization();
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "E-Commerce API",
                    Version = "v1"
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
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
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

            app.UseHttpsRedirection();
            app.UseCors("AllowAll");
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
