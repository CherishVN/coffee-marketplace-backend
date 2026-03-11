using System.Text;
using ECommerceAPI.Application.DTOs.User;
using ECommerceAPI.Application.DTOs.Seller;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Application.Services;
using ECommerceAPI.Infrastructure.Configuration;
using ECommerceAPI.Infrastructure.Data;
using ECommerceAPI.Infrastructure.Repositories;
using ECommerceAPI.Infrastructure.Services;
using ECommerceAPI.Middleware;
using ECommerceAPI.Hubs;
using FluentValidation;
using FluentValidation.AspNetCore;
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

            builder.Services.Configure<AiServiceSettings>(
                builder.Configuration.GetSection(AiServiceSettings.SectionName));

            builder.Services.AddScoped<IUserRepository, UserRepository>();

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IUserClaimsService, UserClaimsService>();
            builder.Services.AddScoped<IUserAdminService, UserAdminService>();
            builder.Services.AddScoped<IWithdrawAdminService, WithdrawAdminService>();
            builder.Services.AddScoped<ISellerApprovalService, SellerApprovalService>();
            builder.Services.AddScoped<ICategoryAdminService, CategoryAdminService>();
            builder.Services.AddScoped<ITagAdminService, TagAdminService>();
            builder.Services.AddScoped<IProductModerationService, ProductModerationService>();
            builder.Services.AddScoped<IDisputeAdminService, DisputeAdminService>();
            builder.Services.AddScoped<IDashboardService, DashboardService>();
            builder.Services.AddScoped<IOrderAdminService, OrderAdminService>();
            builder.Services.AddScoped<IUserProfileService, UserProfileService>();
            builder.Services.AddSingleton<IOtpService, OtpService>();
            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddHttpClient();
            builder.Services.AddScoped<ISellerService, SellerService>();
            builder.Services.AddScoped<ICustomerOrderService, CustomerOrderService>();
            builder.Services.AddScoped<IReviewService, ReviewService>();
            builder.Services.AddScoped<ICustomerDisputeService, CustomerDisputeService>();
            builder.Services.AddScoped<IProductStorefrontService, ProductStorefrontService>();
            builder.Services.AddScoped<ICategoryStorefrontService, CategoryStorefrontService>();
            builder.Services.AddScoped<IFavoriteService, FavoriteService>();

            builder.Services.AddHttpClient<IAiSuggestionService, AiSuggestionService>();

            // FluentValidation
            builder.Services.AddFluentValidationAutoValidation();
            builder.Services.AddValidatorsFromAssemblyContaining<UpdateProfileDtoValidator>();
            builder.Services.AddValidatorsFromAssemblyContaining<CreateWithdrawalRequestDtoValidator>();

            // Supabase JWT Configuration
            var supabaseUrl = builder.Configuration["Supabase:Url"]!;
            var jwksUrl = $"{supabaseUrl}/auth/v1/.well-known/jwks.json";

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false; // Set to true in production
                
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
                
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                        if (context.Exception.InnerException != null)
                        {
                            Console.WriteLine($"   Inner exception: {context.Exception.InnerException.Message}");
                        }
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        Console.WriteLine("Token validated successfully");
                        var claims = context.Principal?.Claims.Select(c => $"{c.Type}: {c.Value}");
                        if (claims != null)
                        {
                            Console.WriteLine($"   Claims: {string.Join(", ", claims)}");
                        }
                        return Task.CompletedTask;
                    },
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
                        if (!string.IsNullOrEmpty(token))
                        {
                            Console.WriteLine($"Token received (first 50 chars): {token.Substring(0, Math.Min(50, token.Length))}...");
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            builder.Services.AddAuthorization();
            builder.Services.AddControllers();
            builder.Services.AddSignalR();
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
            app.UseMiddleware<UserSyncMiddleware>();
            app.UseAuthorization();

            app.MapControllers();
            app.MapHub<OrderTrackingHub>("/hubs/order-tracking");
            app.Run();
        }
    }
}
