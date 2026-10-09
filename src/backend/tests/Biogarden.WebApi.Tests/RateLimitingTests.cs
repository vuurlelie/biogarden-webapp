using Biogarden.BusinessLogic.DTOs.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.BusinessLogic.DTOs.Contact;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.DTOs.Payments;
using Biogarden.DataAccess.Enums;
using Biogarden.WebApi.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Biogarden.WebApi.Tests;

public sealed class RateLimitingTests
{
    [Theory]
    [InlineData("http://localhost:5173", true)]
    [InlineData("https://untrusted.example", false)]
    public async Task BrowserPreflight_AllowsOnlyConfiguredFrontend(string origin, bool allowed)
    {
        using var factory = new BiogardenWebApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/cart/items");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "x-checkout-request,content-type");
        using var response = await client.SendAsync(request);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        if (allowed) Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Theory]
    [InlineData("/api/cart/items", "POST")]
    [InlineData("/api/cart", "DELETE")]
    [InlineData("/api/orders/11111111-1111-1111-1111-111111111111/sumup-checkout", "POST")]
    public async Task CookieMutations_RequireBrowserHeader(string path, string method)
    {
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<ICartService>();
            services.AddScoped<ICartService, SuccessfulCartService>();
            services.RemoveAll<IOrderService>();
            services.AddScoped<IOrderService, SuccessfulOrderService>();
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Remove("X-Checkout-Request");
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (path == "/api/cart/items") request.Content = CreateAddCartItemRequestContent();
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task ProviderCallback_DoesNotRequireBrowserHeader()
    {
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<ISumUpCheckoutStatusService>();
            services.AddScoped<ISumUpCheckoutStatusService, SuccessfulCallback>();
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Remove("X-Checkout-Request");
        using var response = await client.PostAsync("/api/sumup/checkout-status", new StringContent("{}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class SuccessfulCallback : ISumUpCheckoutStatusService
    {
        public Task HandleAsync(string payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Theory]
    [InlineData(false, null, null, HttpStatusCode.BadRequest)]
    [InlineData(true, "https://untrusted.example", null, HttpStatusCode.Forbidden)]
    [InlineData(true, "http://localhost:5173", null, HttpStatusCode.OK)]
    [InlineData(true, null, "invalid", HttpStatusCode.Unauthorized)]
    public async Task GuestCart_RejectsUntrustedRequests(bool header, string? origin, string? token, HttpStatusCode expected)
    {
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<ICartService>();
            services.AddScoped<ICartService, SuccessfulCartService>();
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Remove("X-Checkout-Request");
        if (header) client.DefaultRequestHeaders.Add("X-Checkout-Request", "1");
        if (origin is not null) client.DefaultRequestHeaders.Add("Origin", origin);
        if (token is not null) client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        using var response = await client.GetAsync("/api/cart");
        Assert.Equal(expected, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        if (expected != HttpStatusCode.OK) Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/orders")]
    [InlineData("/api/addresses")]
    public async Task SensitiveErrors_AreNotCacheable(string path)
    {
        using var factory = new BiogardenWebApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(path, new { });
        Assert.False(response.IsSuccessStatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData("https://localhost", "None", true)]
    [InlineData("http://localhost", "Lax", false)]
    public async Task GuestCartCookie_UsesCrossSitePolicyOnlyOverHttps(string baseUrl, string sameSite, bool secure)
    {
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<ICartService>();
            services.AddScoped<ICartService, SuccessfulCartService>();
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(baseUrl) });
        using var first = await client.GetAsync("/api/cart");
        first.EnsureSuccessStatusCode();
        var cookie = Assert.Single(Microsoft.Net.Http.Headers.SetCookieHeaderValue.ParseList(first.Headers.GetValues("Set-Cookie").ToList()),
            item => item.Name == "biogarden_cart_id");
        Assert.Equal(sameSite, cookie.SameSite.ToString());
        Assert.Equal(secure, cookie.Secure);
        Assert.True(cookie.HttpOnly);
        Assert.Equal("/", cookie.Path.ToString());
        Assert.True(Guid.TryParse(cookie.Value.ToString(), out _));

        using var next = await client.PostAsync("/api/cart/items", CreateAddCartItemRequestContent());
        next.EnsureSuccessStatusCode();
        var renewed = Assert.Single(Microsoft.Net.Http.Headers.SetCookieHeaderValue.ParseList(next.Headers.GetValues("Set-Cookie").ToList()),
            item => item.Name == "biogarden_cart_id");
        Assert.Equal(cookie.Value, renewed.Value);
    }

    [Fact]
    public async Task SensitiveAuthEndpoint_ReturnsTooManyRequests_WhenPolicyLimitIsExceeded()
    {
        using var factory = new BiogardenWebApiFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IAuthService>();
                    services.AddScoped<IAuthService, AlwaysFailingAuthService>();
                });
            });

        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var allowedResponse = await client.PostAsync(
                "/api/auth/login",
                CreateLoginRequestContent());

            Assert.Equal(HttpStatusCode.Unauthorized, allowedResponse.StatusCode);
        }

        using var rejectedResponse = await client.PostAsync(
            "/api/auth/login",
            CreateLoginRequestContent());

        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
        Assert.NotNull(rejectedResponse.Headers.RetryAfter?.Delta);

        var error = await rejectedResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status429TooManyRequests, error.Status);
        Assert.Equal("Too many requests.", error.Title);
        Assert.Contains("Túl sok kérés", error.Detail);
        Assert.False(string.IsNullOrWhiteSpace(error.TraceId));
    }

    [Fact]
    public async Task ContactEndpoint_ReturnsTooManyRequests_WhenPublicFormLimitIsExceeded()
    {
        using var factory = CreateFactory(
            services =>
            {
                services.RemoveAll<IContactService>();
                services.AddScoped<IContactService, SuccessfulContactService>();
            });

        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var allowedResponse = await client.PostAsync(
                "/api/contact",
                CreateContactRequestContent());

            Assert.Equal(HttpStatusCode.Accepted, allowedResponse.StatusCode);
        }

        using var rejectedResponse = await client.PostAsync(
            "/api/contact",
            CreateContactRequestContent());

        await AssertRateLimitedResponse(rejectedResponse);
    }

    [Fact]
    public async Task CartMutationEndpoint_ReturnsTooManyRequests_WhenCartMutationLimitIsExceeded()
    {
        using var factory = CreateFactory(
            services =>
            {
                services.RemoveAll<ICartService>();
                services.AddScoped<ICartService, SuccessfulCartService>();
            });

        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 30; attempt++)
        {
            using var allowedResponse = await client.PostAsync(
                "/api/cart/items",
                CreateAddCartItemRequestContent());

            Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
        }

        using var rejectedResponse = await client.PostAsync(
            "/api/cart/items",
            CreateAddCartItemRequestContent());

        await AssertRateLimitedResponse(rejectedResponse);
    }

    [Fact]
    public async Task CheckoutEndpoint_ReturnsTooManyRequests_WhenPublicFormLimitIsExceeded()
    {
        using var factory = CreateFactory(
            services =>
            {
                services.RemoveAll<IOrderService>();
                services.AddScoped<IOrderService, SuccessfulOrderService>();
            });

        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var allowedResponse = await client.PostAsync(
                "/api/orders",
                CreateOrderRequestContent());

            Assert.Equal(HttpStatusCode.Created, allowedResponse.StatusCode);
        }

        using var rejectedResponse = await client.PostAsync(
            "/api/orders",
            CreateOrderRequestContent());

        await AssertRateLimitedResponse(rejectedResponse);
    }

    [Fact]
    public async Task CheckoutEndpoint_PassesIdempotencyKeyToOrderService()
    {
        using var factory = CreateFactory(
            services =>
            {
                services.RemoveAll<IOrderService>();
                services.AddSingleton<CapturingOrderService>();
                services.AddSingleton<IOrderService>(provider => provider.GetRequiredService<CapturingOrderService>());
            });

        using var client = factory.CreateClient();
        using var response = await client.PostAsync(
            "/api/orders",
            CreateOrderRequestContent("checkout-key-123"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var orderService = factory.Services.GetRequiredService<CapturingOrderService>();

        Assert.Equal("checkout-key-123", orderService.LastPlaceOrderDto?.IdempotencyKey);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Conflict)]
    [InlineData(true, HttpStatusCode.InternalServerError)]
    public async Task CartErrors_UseCentralMapping_WithoutExposingDatabaseDetails(bool databaseFailure, HttpStatusCode expected)
    {
        const string detail = "Internal database name and SQL details";
        var failure = databaseFailure
            ? new InvalidOperationException(detail, new Microsoft.EntityFrameworkCore.DbUpdateException(detail))
            : new InvalidOperationException("Insufficient stock.");
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<ICartService>();
            services.AddSingleton<ICartService>(new SuccessfulCartService(failure));
        });
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/api/cart/items", CreateAddCartItemRequestContent());
        Assert.Equal(expected, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ApiErrorResponse>())!;
        Assert.DoesNotContain(detail, error.Detail);
        Assert.False(string.IsNullOrWhiteSpace(error.TraceId));
        if (!databaseFailure) Assert.Equal("Insufficient stock.", error.Detail);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        Action<IServiceCollection> configureServices)
    {
        return new BiogardenWebApiFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(configureServices);
            });
    }

    private static async Task AssertRateLimitedResponse(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter?.Delta);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status429TooManyRequests, error.Status);
        Assert.Equal("Too many requests.", error.Title);
        Assert.Contains("Túl sok kérés", error.Detail);
        Assert.False(string.IsNullOrWhiteSpace(error.TraceId));
    }

    private static StringContent CreateLoginRequestContent()
    {
        const string loginRequest = """
            {
              "email": "test@example.com",
              "password": "WrongPassword1"
            }
            """;

        return new StringContent(
            loginRequest,
            Encoding.UTF8,
            "application/json");
    }

    private static StringContent CreateContactRequestContent()
    {
        return CreateJsonContent(
            """
            {
              "name": "Test User",
              "email": "test@example.com",
              "phone": "+36301234567",
              "submissionId": "fabff1b3-007b-437f-b03b-34071288d076",
              "message": "This is a long enough test contact message."
            }
            """);
    }

    private static StringContent CreateAddCartItemRequestContent()
    {
        return CreateJsonContent(
            """
            {
              "productId": 1,
              "quantity": 1
            }
            """);
    }

    private static StringContent CreateOrderRequestContent(string idempotencyKey = "checkout-rate-limit-key")
    {
        return CreateJsonContent(
            $$"""
            {
              "idempotencyKey": "{{idempotencyKey}}",
              "acceptedTermsVersion": "{{Biogarden.BusinessLogic.Constants.LegalDocumentVersions.CurrentTermsVersion}}",
              "acceptedPrivacyPolicyVersion": "{{Biogarden.BusinessLogic.Constants.LegalDocumentVersions.CurrentPrivacyPolicyVersion}}",
              "fulfillmentMethod": 0,
              "customerName": "Test User",
              "customerEmail": "test@example.com",
              "customerPhone": "+36301234567",
              "postalCode": "8431",
              "city": "Bakonyszentlászló",
              "street": "Jókai utca",
              "houseNumber": "22"
            }
            """);
    }

    private static StringContent CreateJsonContent(string json)
    {
        return new StringContent(
            json,
            Encoding.UTF8,
            "application/json");
    }

    private sealed class AlwaysFailingAuthService : IAuthService
    {
        public Task<AuthOperationResultDto> RegisterAsync(RegisterRequestDto request)
        {
            return Task.FromResult(AuthOperationResultDto.Failure("Registration failed."));
        }

        public Task<LoginResultDto> LoginAsync(LoginRequestDto loginRequestDto)
        {
            return Task.FromResult(LoginResultDto.Failure("Authentication failed."));
        }

        public Task<AuthOperationResultDto> ConfirmEmailAsync(
            ConfirmEmailRequestDto confirmEmailRequestDto)
        {
            return Task.FromResult(AuthOperationResultDto.Failure("Email confirmation failed."));
        }

        public Task ResendEmailConfirmationAsync(
            ResendEmailConfirmationRequestDto resendEmailConfirmationRequestDto)
        {
            return Task.CompletedTask;
        }

        public Task ForgotPasswordAsync(ForgotPasswordRequestDto forgotPasswordRequestDto)
        {
            return Task.CompletedTask;
        }

        public Task<AuthOperationResultDto> ResetPasswordAsync(
            ResetPasswordRequestDto resetPasswordRequestDto)
        {
            return Task.FromResult(AuthOperationResultDto.Failure("Password reset failed."));
        }
    }

    private sealed class SuccessfulContactService : IContactService
    {
        public Task SendMessageAsync(
            ContactMessageRequestDto contactMessageRequestDto,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class SuccessfulCartService(Exception? failure = null) : ICartService
    {
        public Task<CartDto> GetCartAsync(
            Guid? userId,
            string? sessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateCart());
        }

        public Task<CartDto> AddItemAsync(
            Guid? userId,
            string? sessionId,
            AddCartItemDto addCartItemDto,
            CancellationToken cancellationToken = default)
        {
            if (failure is not null) throw failure;
            return Task.FromResult(CreateCart());
        }

        public Task<int> GetAvailableQuantityToAddAsync(
            Guid? userId,
            string? sessionId,
            int productId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(10);
        }

        public Task<CartDto> UpdateItemQuantityAsync(
            Guid? userId,
            string? sessionId,
            Guid cartItemId,
            UpdateCartItemQuantityDto updateCartItemQuantityDto,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateCart());
        }

        public Task<CartDto> RemoveItemAsync(
            Guid? userId,
            string? sessionId,
            Guid cartItemId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateCart());
        }

        public Task<CartDto> ClearCartAsync(
            Guid? userId,
            string? sessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateCart());
        }

        public Task<CartDto> MergeGuestCartIntoUserCartAsync(
            Guid userId,
            string guestSessionId,
            CancellationToken cancellationToken = default, string? browserSession = null)
        {
            return Task.FromResult(CreateCart());
        }

        public Task<CartValidationResultDto> ValidateCartForCheckoutAsync(
            Guid? userId,
            string? sessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CartValidationResultDto());
        }

        private static CartDto CreateCart()
        {
            return new CartDto
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow,
                LastActivityAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30)
            };
        }
    }

    private class SuccessfulOrderService : IOrderService
    {
        public Task<OrderDto> GetCheckoutOrderAsync(Guid? userId, string? sessionId, Guid orderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CheckoutQuoteDto> GetCheckoutQuoteAsync(Guid? userId, string? sessionId, CheckoutQuoteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public virtual Task<OrderCreationResult> CreateOrderFromCartAsync(
            Guid? userId,
            string? sessionId,
            PlaceOrderDto dto,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new OrderCreationResult(CreateOrder(), Created: true));
        }

        public Task<OrderDto> RecoverCheckoutOrderAsync(Guid? userId, string? sessionId, string idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task ReconcileSumUpCheckoutAsync(Biogarden.BusinessLogic.Models.Payments.SumUpCheckoutResult checkout, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SumUpCheckoutDto> CreateSumUpCheckoutAsync(
            Guid? userId,
            string? sessionId,
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SumUpCheckoutDto
            {
                OrderId = orderId,
                CheckoutId = "checkout-id",
                Url = "https://example.com/checkout"
            });
        }

        public Task<OrderDto> ConfirmSumUpCheckoutAsync(
            string sumUpCheckoutId,
            string? sumUpTransactionId,
            decimal? amount = null,
            string? currency = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateOrder());
        }

        public Task<OrderDto?> MarkSumUpPaymentFailedAsync(
            string sumUpCheckoutId,
            string? sumUpTransactionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<OrderDto?>(CreateOrder());
        }

        public Task<OrderDto> CancelOrderAsync(
            Guid userId,
            Guid orderId,
            CancelOrderDto dto,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateOrder());
        }

        public Task<OrderWithdrawalRequestDto> RequestWithdrawalAsync(
            Guid userId,
            Guid orderId,
            CreateOrderWithdrawalRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new OrderWithdrawalRequestDto
            {
                Id = 1,
                OrderId = orderId,
                CustomerName = "Test User",
                CustomerEmail = "test@example.com",
                Reason = dto.Reason,
                Status = OrderWithdrawalRequestStatus.Pending,
                RequestedAtUtc = DateTime.UtcNow
            });
        }

        public Task<OrderWithdrawalRequestDto> RequestWithdrawalByOrderLookupAsync(
            PublicOrderWithdrawalRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new OrderWithdrawalRequestDto
            {
                Id = 1,
                OrderId = Guid.NewGuid(),
                CustomerName = "Teszt Elek",
                CustomerEmail = dto.CustomerEmail,
                Reason = dto.Reason,
                Status = OrderWithdrawalRequestStatus.Pending,
                RequestedAtUtc = DateTime.UtcNow
            });
        }

        public Task<PagedResultDto<OrderDto>> GetMyOrdersAsync(
            Guid userId, CustomerOrderListQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PagedResultDto<OrderDto>(new List<OrderDto> { CreateOrder() }, 1, query.PageSize, 1));
        }

        public Task<OrderDto> GetOrderByIdAsync(
            Guid userId,
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateOrder());
        }

        public Task<PublicOrderPaymentStatusDto> GetPublicOrderPaymentStatusAsync(
            Guid orderId,
            string sumUpCheckoutReference,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PublicOrderPaymentStatusDto
            {
                OrderId = orderId,
                OrderNumber = "TEST-ORDER",
                Status = OrderStatus.Confirmed,
                PaymentStatus = PaymentStatus.Paid,
                ConfirmedAtUtc = DateTime.UtcNow
            });
        }

        private static OrderDto CreateOrder()
        {
            return new OrderDto
            {
                Id = Guid.NewGuid(),
                OrderNumber = "TEST-ORDER",
                CustomerName = "Test User",
                CustomerEmail = "test@example.com",
                CustomerPhone = "+36301234567",
                CreatedAtUtc = DateTime.UtcNow
            };
        }
    }

    private sealed class CapturingOrderService : SuccessfulOrderService
    {
        public PlaceOrderDto? LastPlaceOrderDto { get; private set; }

        public override Task<OrderCreationResult> CreateOrderFromCartAsync(
            Guid? userId,
            string? sessionId,
            PlaceOrderDto dto,
            CancellationToken cancellationToken = default)
        {
            LastPlaceOrderDto = dto;
            return base.CreateOrderFromCartAsync(userId, sessionId, dto, cancellationToken);
        }
    }
}
