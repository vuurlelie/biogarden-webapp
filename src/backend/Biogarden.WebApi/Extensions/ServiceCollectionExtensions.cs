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
        services.AddFrontendCors(configuration);
        services.AddDataAccess(configuration);
        services.AddAppHealthChecks();
        services.AddAppIdentityAndAuthentication(configuration);

        services.AddAuthorization();

        return services;
    }

    private static IServiceCollection AddWebApiCore(this IServiceCollection services)
    {
        services.AddScoped<Filters.AccountSecurityMutationFilter>();
        foreach (var validator in typeof(Biogarden.BusinessLogic.Validation.PlaceOrderValidator).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && !type.IsGenericTypeDefinition))
        {
            foreach (var contract in validator.GetInterfaces().Where(type => type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(FluentValidation.IValidator<>)))
                services.AddScoped(contract, validator);
        }
        services.AddControllers(options =>
            {
                options.Filters.Add<Biogarden.WebApi.Filters.ApiProblemResultFilter>();
                options.Filters.Add<Biogarden.WebApi.Filters.RequestValidationFilter>();
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = ApiProblems.InvalidModel);
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

    private static IServiceCollection AddFrontendCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var allowedOrigins = GetFrontendAllowedOrigins(configuration);

        services.AddCors(options =>
        {
            options.AddPolicy("FrontendDev", policy =>
            {
                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    private static string[] GetFrontendAllowedOrigins(IConfiguration configuration)
    {
        var configuredOrigins = configuration
            .GetSection("Frontend:AllowedOrigins")
            .Get<string[]>();

        if (configuredOrigins is { Length: > 0 })
        {
            return configuredOrigins
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Select(origin => origin.Trim().TrimEnd('/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var singleValue = configuration["Frontend:AllowedOrigins"];

        if (!string.IsNullOrWhiteSpace(singleValue))
        {
            return singleValue
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(origin => origin.TrimEnd('/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return ["http://localhost:5173"];
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
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
            .AddCheck<OperationalHealthCheck>("operations", tags: ["operations"], timeout: TimeSpan.FromSeconds(10));

        return services;
    }

    private static IServiceCollection AddValidatedOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddValidatedOptions<AbuseProtectionOptions>(
            configuration,
            AbuseProtectionOptions.SectionName,
            options => options.ValidateForStartup());
        services.AddValidatedOptions<JwtSettings>(
            configuration,
            JwtSettings.SectionName,
            options => options.ValidateForStartup());
        services.AddValidatedOptions<EmailOptions>(
            configuration,
            EmailOptions.SectionName,
            options => options.ValidateForStartup());
        services.AddValidatedOptions<AuthEmailOptions>(
            configuration,
            AuthEmailOptions.SectionName,
            options => options.ValidateForStartup());
        services.AddValidatedOptions<EmailBrandingOptions>(
            configuration,
            EmailBrandingOptions.SectionName,
            options => options.ValidateForStartup());
        services.AddValidatedOptions<OperationalMonitoringOptions>(configuration,
            OperationalMonitoringOptions.SectionName, options => options.ValidateForStartup());
        services.AddValidatedOptions<ErrorAlertOptions>(
            configuration,
            ErrorAlertOptions.SectionName,
            options => options.ValidateForStartup());
        services.AddValidatedOptions<SumUpOptions>(
            configuration,
            SumUpOptions.SectionName,
            options => options.ValidateForStartup());
        services.AddValidatedOptions<CheckoutPricingOptions>(configuration,
            CheckoutPricingOptions.SectionName, options => options.ValidateForStartup());
        services.AddValidatedOptions<CartCleanupOptions>(configuration,
            CartCleanupOptions.SectionName, options => options.ValidateForStartup());
        services.AddValidatedOptions<GlsOptions>(configuration,
            GlsOptions.SectionName, options => options.ValidateForStartup());

        return services;
    }

    private static IServiceCollection AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        Action<TOptions> validate)
        where TOptions : class
    {
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .Validate(options =>
            {
                validate(options);
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

            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);

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
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    if (context.Principal is null || !await Authentication.SessionAuthentication.IsCurrentAsync(
                        context.Principal, context.HttpContext.RequestServices, context.HttpContext.RequestAborted))
                        context.Fail("The session is no longer valid.");
                }
            };
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
        services.AddCoreApplicationServices();
        services.AddCommunicationServices();
        services.AddPaymentServices();
        services.AddCheckoutServices();
        services.AddAdminServices();

        services.AddSingleton<TimeProvider>(TimeProvider.System);

        return services;
    }

    private static IServiceCollection AddCoreApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IPostService, PostService>();
        services.AddSingleton<Biogarden.WebApi.Services.ManagedImageStorage>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountLifecycleOperations, AccountLifecycleOperations>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITokenService, JwtTokenService>();

        return services;
    }

    private static IServiceCollection AddCommunicationServices(this IServiceCollection services)
    {
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<INotificationEmailRepository, NotificationEmailRepository>();
        services.AddScoped<NotificationEmailDispatcher>();
        services.AddScoped<NotificationMessageFactory>();
        services.AddScoped<IAccountNotificationQueue, AccountNotificationQueue>();
        services.AddHostedService<Biogarden.WebApi.Services.NotificationEmailWorker>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<IOrderEmailRepository, OrderEmailRepository>();
        services.AddScoped<OrderEmailDispatcher>();
        services.AddHostedService<Biogarden.WebApi.Services.OrderEmailWorker>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<IErrorAlertService, ErrorAlertService>();
        services.AddSingleton<ErrorAlertThrottleStore>();

        return services;
    }

    private static IServiceCollection AddPaymentServices(this IServiceCollection services)
    {
        services.AddHttpClient<ISumUpCheckoutClient, SumUpCheckoutClient>();
        services.AddHttpClient(GlsOptions.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<IGlsDeliveryPointDirectory>(provider => new GlsDeliveryPointDirectory(
            () => provider.GetRequiredService<IHttpClientFactory>().CreateClient(GlsOptions.HttpClientName),
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GlsOptions>>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddScoped<ISumUpCheckoutService, SumUpCheckoutService>();
        services.AddScoped<ISumUpCheckoutStatusService, SumUpCheckoutStatusService>();

        return services;
    }

    private static IServiceCollection AddCheckoutServices(this IServiceCollection services)
    {
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<CartCleanupService>();
        services.AddHostedService<Biogarden.WebApi.Services.CartCleanupWorker>();
        services.AddScoped<CheckoutDraftService>();
        services.AddHostedService<Biogarden.WebApi.Services.CheckoutDraftCleanupWorker>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<CheckoutPricingService>();

        return services;
    }

    private static IServiceCollection AddAdminServices(this IServiceCollection services)
    {
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
