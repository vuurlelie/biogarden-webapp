using System.Net.Http.Json;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.DataAccess.Enums;

namespace Biogarden.WebApi.Tests.SqlServer;

internal static class CheckoutQuoteHelper
{
    public static async Task<PlaceOrderDto> WithQuoteAsync(this PlaceOrderDto request, HttpClient client)
    {
        request.PaymentMethod ??= OrderPaymentMethod.OnlineCard;
        using var response = await client.PostAsJsonAsync("/api/orders/quote",
            new CheckoutQuoteRequest(request.FulfillmentMethod, request.PaymentMethod.Value));
        response.EnsureSuccessStatusCode();
        request.QuoteVersion = (await response.Content.ReadFromJsonAsync<CheckoutQuoteDto>())!.Version;
        return request;
    }
}
