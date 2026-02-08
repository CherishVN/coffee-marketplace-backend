using System.Text;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Application.Services;
using ECommerceAPI.Infrastructure.Configuration;
using ECommerceAPI.Infrastructure.Data;
using ECommerceAPI.Infrastructure.Repositories;
using ECommerceAPI.Infrastructure.Services;
using ECommerceAPI.Middleware;
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
            builder.Services.AddHttpContextAccessor(); // Required for UserClaimsService
            builder.Services.AddScoped<IUserClaimsService, UserClaimsService>();
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

            // Supabase JWT Configuration
            var supabaseJwtSecret = builder.Configuration["Supabase:JwtSecret"] ?? throw new InvalidOperationException("Supabase JwtSecret is not configured");
            var supabaseUrl = builder.Configuration["Supabase:Url"]!;

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
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
            app.UseMiddleware<UserSyncMiddleware>(); // Auto-create user on first request
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
