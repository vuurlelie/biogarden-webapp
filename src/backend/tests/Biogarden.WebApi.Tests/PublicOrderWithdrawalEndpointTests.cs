using Biogarden.BusinessLogic.DTOs.Common;
using System.Net;
using System.Net.Http.Json;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.DTOs.Payments;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Biogarden.WebApi.Tests;

public sealed class PublicOrderWithdrawalEndpointTests
{
    [Theory]
    [InlineData("BK-20261008-00000000", HttpStatusCode.NotFound)]
    [InlineData(" bk-20261008-a1b2c3d4 ", HttpStatusCode.NotFound)]
    [InlineData("fake-order", HttpStatusCode.BadRequest)]
    [InlineData("BK-20261008-ABCDEFGH", HttpStatusCode.BadRequest)]
    [InlineData("BK-20261008-A1B2C3D", HttpStatusCode.BadRequest)]
    public async Task RequestWithdrawalByOrderLookup_RejectsMalformedOrMismatchedOrders(string orderNumber, HttpStatusCode expected)
    {
        using var factory = new BiogardenWebApiFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOrderService>();
                    services.AddScoped<IOrderService, MissingPublicOrderService>();
                });
            });

        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/orders/withdrawal-requests",
            new PublicOrderWithdrawalRequestDto
            {
                OrderNumber = orderNumber,
                CustomerEmail = "wrong@example.com",
                Reason = "Szeretnék panaszt rögzíteni a rendeléssel kapcsolatban."
            });

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.BadRequest)
        {
            var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.True(json.GetProperty("errors").TryGetProperty("orderNumber", out var messages));
            Assert.NotEmpty(messages.EnumerateArray());
            return;
        }

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Equal("Resource not found.", problem.Title);
        Assert.Equal(
            "Nem találtunk ilyen rendelést ezzel az email címmel.",
            problem.Detail);
    }

    private sealed class MissingPublicOrderService : IOrderService
    {
        public Task<OrderDto> GetCheckoutOrderAsync(Guid? userId, string? sessionId, Guid orderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CheckoutQuoteDto> GetCheckoutQuoteAsync(Guid? userId, string? sessionId, CheckoutQuoteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<OrderCreationResult> CreateOrderFromCartAsync(
            Guid? userId,
            string? sessionId,
            PlaceOrderDto dto,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OrderDto> RecoverCheckoutOrderAsync(Guid? userId, string? sessionId, string idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task ReconcileSumUpCheckoutAsync(Biogarden.BusinessLogic.Models.Payments.SumUpCheckoutResult checkout, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SumUpCheckoutDto> CreateSumUpCheckoutAsync(
            Guid? userId,
            string? sessionId,
            Guid orderId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OrderDto> ConfirmSumUpCheckoutAsync(
            string sumUpCheckoutId,
            string? sumUpTransactionId,
            decimal? amount = null,
            string? currency = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OrderDto?> MarkSumUpPaymentFailedAsync(
            string sumUpCheckoutId,
            string? sumUpTransactionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OrderDto> CancelOrderAsync(
            Guid userId,
            Guid orderId,
            CancelOrderDto dto,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OrderWithdrawalRequestDto> RequestWithdrawalAsync(
            Guid userId,
            Guid orderId,
            CreateOrderWithdrawalRequestDto dto,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OrderWithdrawalRequestDto> RequestWithdrawalByOrderLookupAsync(
            PublicOrderWithdrawalRequestDto dto,
            CancellationToken cancellationToken = default) =>
            throw new KeyNotFoundException(
                "Nem találtunk ilyen rendelést ezzel az email címmel.");

        public Task<PagedResultDto<OrderDto>> GetMyOrdersAsync(
            Guid userId, CustomerOrderListQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OrderDto> GetOrderByIdAsync(
            Guid userId,
            Guid orderId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PublicOrderPaymentStatusDto> GetPublicOrderPaymentStatusAsync(
            Guid orderId,
            string sumUpCheckoutReference,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
