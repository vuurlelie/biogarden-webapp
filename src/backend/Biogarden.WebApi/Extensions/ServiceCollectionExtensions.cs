using System.Text;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Repositories;
using Biogarden.WebApi.Constants;
using Biogarden.WebApi.Contracts;
using Biogarden.WebApi.Health;
using Biogarden.WebApi.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Biogarden.WebApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddWebApiCore();
        services.AddForwardedHeaders();
        services.AddValidatedOptions(configuration);
        services.AddAppRateLimiting(configuration);
        services.AddFrontendCors();
        services.AddDataAccess(configuration);
        services.AddAppHealthChecks();
        services.AddAppIdentityAndAuthentication(configuration);

        services.AddAuthorization();

        return services;
    }

    private static IServiceCollection AddWebApiCore(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();

        return services;
    }

    private static IServiceCollection AddForwardedHeaders(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto;
        });

        return services;
    }

    private static IServiceCollection AddAppRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var abuseProtectionOptions = configuration
            .GetSection(AbuseProtectionOptions.SectionName)
            .Get<AbuseProtectionOptions>()
            ?? new AbuseProtectionOptions();

        abuseProtectionOptions.ValidateForStartup();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfter = TimeSpan.FromSeconds(abuseProtectionOptions.RetryAfterSeconds);

                if (context.Lease.TryGetMetadata(
                    MetadataName.RetryAfter,
                    out var metadataRetryAfter))
                {
                    retryAfter = metadataRetryAfter;
                }

                context.HttpContext.Response.Headers.RetryAfter =
                    Math.Ceiling(retryAfter.TotalSeconds).ToString();

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ApiErrorResponse
                    {
                        Title = "Too many requests.",
                        Status = StatusCodes.Status429TooManyRequests,
                        Detail = "Túl sok kérés érkezett. Kérjük, várj egy percet, majd próbáld újra.",
                        TraceId = context.HttpContext.TraceIdentifier
                    },
                    cancellationToken);
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    GetClientPartition(httpContext),
                    _ => CreateFixedWindowOptions(abuseProtectionOptions.Global)));

            options.AddPolicy(
                RateLimitPolicyNames.SensitiveAuth,
                httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    GetClientPartition(httpContext),
                    _ => CreateFixedWindowOptions(abuseProtectionOptions.SensitiveAuth)));

            options.AddPolicy(
                RateLimitPolicyNames.PublicForm,
                httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    GetClientPartition(httpContext),
                    _ => CreateFixedWindowOptions(abuseProtectionOptions.PublicForm)));

            options.AddPolicy(
                RateLimitPolicyNames.CartMutation,
                httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    GetClientPartition(httpContext),
                    _ => CreateFixedWindowOptions(abuseProtectionOptions.CartMutation)));

            options.AddPolicy(
                RateLimitPolicyNames.Payment,
                httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    GetClientPartition(httpContext),
                    _ => CreateFixedWindowOptions(abuseProtectionOptions.Payment)));
        });

        return services;
    }

    private static IServiceCollection AddFrontendCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("FrontendDev", policy =>
            {
                policy
                    .WithOrigins("http://localhost:5173")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    private static IServiceCollection AddDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IAppDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<AppDbContext>());

        return services;
    }

    private static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        return services;
    }

    private static IServiceCollection AddValidatedOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AbuseProtectionOptions>()
            .Bind(configuration.GetSection(AbuseProtectionOptions.SectionName))
            .Validate(options =>
            {
                options.ValidateForStartup();
                return true;
            })
            .ValidateOnStart();

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(options =>
            {
                options.ValidateForStartup();
                return true;
            })
            .ValidateOnStart();

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(options =>
            {
                options.ValidateForStartup();
                return true;
            })
            .ValidateOnStart();

        services.AddOptions<AuthEmailOptions>()
            .Bind(configuration.GetSection(AuthEmailOptions.SectionName))
            .Validate(options =>
            {
                options.ValidateForStartup();
                return true;
            })
            .ValidateOnStart();

        services.AddOptions<EmailBrandingOptions>()
            .Bind(configuration.GetSection(EmailBrandingOptions.SectionName))
            .Validate(options =>
            {
                options.ValidateForStartup();
                return true;
            })
            .ValidateOnStart();

        services.AddOptions<ErrorAlertOptions>()
            .Bind(configuration.GetSection(ErrorAlertOptions.SectionName))
            .Validate(options =>
            {
                options.ValidateForStartup();
                return true;
            })
            .ValidateOnStart();

        services.AddOptions<SumUpOptions>()
            .Bind(configuration.GetSection(SumUpOptions.SectionName))
            .Validate(options =>
            {
                options.ValidateForStartup();
                return true;
            })
            .ValidateOnStart();

        return services;
    }

    private static IServiceCollection AddAppIdentityAndAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration
            .GetSection(JwtSettings.SectionName)
            .Get<JwtSettings>()
            ?? throw new InvalidOperationException(
                "JWT settings are not configured.");

        jwtSettings.ValidateForStartup();

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;

            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme =
                JwtBearerDefaults.AuthenticationScheme;

            options.DefaultChallengeScheme =
                JwtBearerDefaults.AuthenticationScheme;

            options.DefaultScheme =
                JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ClockSkew = TimeSpan.Zero
            };
        });

        return services;
    }

    private static FixedWindowRateLimiterOptions CreateFixedWindowOptions(
        RateLimitRuleOptions rule)
    {
        return new FixedWindowRateLimiterOptions
        {
            PermitLimit = rule.PermitLimit,
            Window = TimeSpan.FromSeconds(rule.WindowSeconds),
            QueueLimit = 0
        };
    }

    private static string GetClientPartition(HttpContext httpContext)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}";
        }

        var remoteIpAddress = httpContext.Connection.RemoteIpAddress?.ToString();

        if (!string.IsNullOrWhiteSpace(remoteIpAddress))
        {
            return $"ip:{remoteIpAddress}";
        }

        return "anonymous";
    }

    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IAddressService, AddressService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<IErrorAlertService, ErrorAlertService>();
        services.AddSingleton<ErrorAlertThrottleStore>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddHttpClient<ISumUpCheckoutClient, SumUpCheckoutClient>();
        services.AddScoped<ISumUpCheckoutService, SumUpCheckoutService>();
        services.AddScoped<ISumUpCheckoutStatusService, SumUpCheckoutStatusService>();

        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IAdminInventoryService, AdminInventoryService>();
        services.AddScoped<IAdminOrderService, AdminOrderService>();
        services.AddScoped<IAdminUserService, AdminUserService>();

        return services;
    }

    public static IServiceCollection AddSwaggerDocumentation(
        this IServiceCollection services)
    {
        services.AddSwaggerGen();

        return services;
    }
}
