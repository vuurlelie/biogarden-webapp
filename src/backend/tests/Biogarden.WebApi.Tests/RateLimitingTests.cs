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

            Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
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

            Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orderService = factory.Services.GetRequiredService<CapturingOrderService>();

        Assert.Equal("checkout-key-123", orderService.LastPlaceOrderDto?.IdempotencyKey);
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

    private sealed class SuccessfulCartService : ICartService
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
            CancellationToken cancellationToken = default)
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

        public Task<int> CleanupExpiredCartsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
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
        public virtual Task<OrderDto> CreateOrderFromCartAsync(
            Guid? userId,
            string? sessionId,
            PlaceOrderDto dto,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateOrder());
        }

        public Task<OrderDto> ConfirmOrderPaymentAsync(
            Guid userId,
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateOrder());
        }

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

        public Task<List<OrderDto>> GetMyOrdersAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<OrderDto> { CreateOrder() });
        }

        public Task<OrderDto> GetOrderByIdAsync(
            Guid userId,
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateOrder());
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

        public override Task<OrderDto> CreateOrderFromCartAsync(
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
