using System.Text;
using ECommerceAPI.Application.DTOs.User;
using ECommerceAPI.Application.DTOs.Seller;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Application.Services;
using ECommerceAPI.Infrastructure.Configuration;
using ECommerceAPI.Infrastructure.Background;
using ECommerceAPI.Infrastructure.Data;
using ECommerceAPI.Infrastructure.Notifications;
using ECommerceAPI.Infrastructure.Repositories;
using ECommerceAPI.Infrastructure.Services;
using ECommerceAPI.Middleware;
using ECommerceAPI.Hubs;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace ECommerceAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(
                    builder.Configuration.GetConnectionString("DefaultConnection"),
                    npgsql =>
                    {
                        npgsql.EnableRetryOnFailure(3);
                        npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    }));

            builder.Services.Configure<AiServiceSettings>(
                builder.Configuration.GetSection(AiServiceSettings.SectionName));

            builder.Services.Configure<VNPaySettings>(
                builder.Configuration.GetSection(VNPaySettings.SectionName));

            builder.Services.Configure<MoMoSettings>(
                builder.Configuration.GetSection(MoMoSettings.SectionName));

            builder.Services.Configure<PlatformFeeSettings>(
                builder.Configuration.GetSection(PlatformFeeSettings.SectionName));

            builder.Services.Configure<FptAiSettings>(
                builder.Configuration.GetSection(FptAiSettings.SectionName));

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
            builder.Services.AddScoped<IPlatformFeeReportService, PlatformFeeReportService>();
            builder.Services.AddScoped<IPlatformFeeConfigService, PlatformFeeConfigService>();
            builder.Services.AddScoped<IOrderAdminService, OrderAdminService>();
            builder.Services.AddScoped<IUserProfileService, UserProfileService>();
            builder.Services.AddSingleton<IOtpService, OtpService>();
            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddScoped<IUserAuthEmailResolver, SupabaseAuthEmailResolver>();
            builder.Services.AddHttpClient();
            builder.Services.AddScoped<IFptVietnamIdCardOcrService, FptVietnamIdCardOcrService>();
            builder.Services.AddHttpClient("MoMoGateway", client =>
            {
                // Avoid hanging outbound payment calls for too long.
                client.Timeout = TimeSpan.FromSeconds(10);
            });
            builder.Services.AddScoped<IOrderStatusHistoryService, OrderStatusHistoryService>();

            builder.Services.AddScoped<IGhnOrderWebhookService, GhnOrderWebhookService>();
            builder.Services.AddScoped<ISellerService, SellerService>();
            builder.Services.AddScoped<ICustomerOrderService, CustomerOrderService>();
            builder.Services.AddScoped<IReviewService, ReviewService>();
            builder.Services.AddScoped<ICustomerDisputeService, CustomerDisputeService>();
            builder.Services.AddScoped<ISellerDisputeService, SellerDisputeService>();
            builder.Services.AddScoped<IProductStorefrontService, ProductStorefrontService>();
            builder.Services.AddScoped<ICategoryStorefrontService, CategoryStorefrontService>();
            builder.Services.AddScoped<IFavoriteService, FavoriteService>();
            builder.Services.AddScoped<ICartService, CartService>();
            builder.Services.AddScoped<ISellerWalletSettlementService, SellerWalletSettlementService>();
            builder.Services.AddScoped<ISellerWalletReleaseService, SellerWalletReleaseService>();
            builder.Services.AddScoped<ISellerWalletReversalService, SellerWalletReversalService>();
            builder.Services.AddScoped<ICustomerWalletService, CustomerWalletService>();
            builder.Services.AddScoped<IPaymentService, PaymentService>();
            builder.Services.AddScoped<IConversationService, ConversationService>();
            builder.Services.AddScoped<IMaterialAdminService, MaterialAdminService>();
            builder.Services.AddScoped<IShopStorefrontService, ShopStorefrontService>();
            builder.Services.AddScoped<IOrderNotificationEmailComposer, OrderNotificationEmailComposer>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddSingleton<INotificationQueue, NotificationQueue>();
            builder.Services.AddHostedService<NotificationEmailBackgroundService>();
            builder.Services.AddHostedService<PaymentTimeoutBackgroundService>();
            builder.Services.AddHostedService<SellerWalletReleaseBackgroundService>();
            builder.Services.AddHostedService<OrderAutoCompleteBackgroundService>();
            builder.Services.AddHostedService<CancelRequestTimeoutBackgroundService>();
            builder.Services.AddMemoryCache();

            builder.Services.Configure<ForwardedHeadersOptions>(opts =>
            {
                opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                opts.KnownNetworks.Clear();
                opts.KnownProxies.Clear();
            });

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
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrWhiteSpace(accessToken)
                            && path.StartsWithSegments("/hubs/order-tracking"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        // Keep auth failure handling minimal to avoid noisy per-request console logs.
                        return Task.CompletedTask;
                    }
                };
            });

            builder.Services.AddAuthorization();
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = async (context, cancellationToken) =>
                {
                    var retryAfterSeconds = 10;
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                    }

                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json";
                    context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();

                    await context.HttpContext.Response.WriteAsJsonAsync(new
                    {
                        success = false,
                        message = $"Bạn thao tác quá nhanh. Vui lòng thử lại sau {retryAfterSeconds} giây."
                    }, cancellationToken: cancellationToken);
                };

                options.AddPolicy("PaymentCreatePerUser", context =>
                {
                    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? context.User.FindFirstValue("sub")
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "anonymous";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: userId,
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 1,
                            Window = TimeSpan.FromSeconds(10),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        });
                });
            });
            builder.Services.AddControllers();
            builder.Services.AddSignalR();
            builder.Services.AddEndpointsApiExplorer();
            
            builder.Services.AddSwaggerGen(options =>
            {
                // Tránh trùng schemaId (cùng tên class ở namespace khác) — lỗi phổ biến khiến GET /swagger/v1/swagger.json trả 500.
                options.CustomSchemaIds(type => type.FullName!.Replace("+", "."));

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

            var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
            var frontendUrl = builder.Configuration["FrontendUrl"];
            var allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var origin in corsOrigins)
            {
                if (Uri.TryCreate(origin, UriKind.Absolute, out var u)) allowedHosts.Add(u.Host);
            }
            if (!string.IsNullOrEmpty(frontendUrl) && Uri.TryCreate(frontendUrl, UriKind.Absolute, out var fUri))
            {
                allowedHosts.Add(fUri.Host);
            }

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
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

            app.UseForwardedHeaders();

            var enableSwagger = app.Environment.IsDevelopment()
                || app.Configuration.GetValue<bool>("EnableSwagger");
            if (enableSwagger)
            {
                app.UseSwagger();
                app.UseSwaggerUI();
                // Redirect GET / -> Swagger (middleware: reliable hơn MapGet sau MapControllers trên Cloud Run)
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

            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }
            app.UseCors("AllowAll");
            app.UseAuthentication();
            app.UseRateLimiter();
            app.UseMiddleware<UserSyncMiddleware>();
            app.UseAuthorization();

            app.MapControllers();
            app.MapHub<OrderTrackingHub>("/hubs/order-tracking");
            app.Run();
        }
    }
}
