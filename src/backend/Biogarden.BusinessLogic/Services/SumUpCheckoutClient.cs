using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Models.Payments;

namespace Biogarden.BusinessLogic.Services;

public sealed class SumUpCheckoutClient : ISumUpCheckoutClient
{
    private readonly HttpClient _httpClient;

    public SumUpCheckoutClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<SumUpCheckoutResult> CreateAsync(
        SumUpCreateCheckoutRequest request,
        string apiKey,
        Uri apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(apiBaseUrl, "/v0.1/checkouts"));

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = JsonContent.Create(new SumUpCreateCheckoutApiRequest
        {
            CheckoutReference = request.CheckoutReference,
            Amount = request.Amount,
            Currency = request.Currency,
            MerchantCode = request.MerchantCode,
            Description = request.Description,
            RedirectUrl = request.RedirectUrl,
            ReturnUrl = request.ReturnUrl,
            HostedCheckout = new SumUpHostedCheckoutApiRequest { Enabled = true }
        });

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var checkout = await response.Content.ReadFromJsonAsync<SumUpCheckoutApiResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("SumUp checkout response was empty.");

        return checkout.ToResult();
    }

    public async Task<SumUpCheckoutResult> GetAsync(
        string checkoutId,
        string apiKey,
        Uri apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(apiBaseUrl, $"/v0.1/checkouts/{Uri.EscapeDataString(checkoutId)}"));

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var checkout = await response.Content.ReadFromJsonAsync<SumUpCheckoutApiResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("SumUp checkout response was empty.");

        return checkout.ToResult();
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        throw new InvalidOperationException(
            $"SumUp API request failed with status {(int)response.StatusCode}: {body}");
    }

    private sealed class SumUpCreateCheckoutApiRequest
    {
        [JsonPropertyName("checkout_reference")]
        public string CheckoutReference { get; init; } = string.Empty;

        [JsonPropertyName("amount")]
        public decimal Amount { get; init; }

        [JsonPropertyName("currency")]
        public string Currency { get; init; } = string.Empty;

        [JsonPropertyName("merchant_code")]
        public string MerchantCode { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("redirect_url")]
        public string RedirectUrl { get; init; } = string.Empty;

        [JsonPropertyName("return_url")]
        public string ReturnUrl { get; init; } = string.Empty;

        [JsonPropertyName("hosted_checkout")]
        public SumUpHostedCheckoutApiRequest HostedCheckout { get; init; } = new();
    }

    private sealed class SumUpHostedCheckoutApiRequest
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; init; }
    }

    private sealed class SumUpCheckoutApiResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;

        [JsonPropertyName("amount")]
        public decimal? Amount { get; init; }

        [JsonPropertyName("currency")]
        public string? Currency { get; init; }

        [JsonPropertyName("hosted_checkout_url")]
        public string? HostedCheckoutUrl { get; init; }

        [JsonPropertyName("transactions")]
        public IReadOnlyList<SumUpTransactionApiResponse> Transactions { get; init; } = [];

        public SumUpCheckoutResult ToResult()
        {
            var transactionCode = Transactions
                .Select(transaction => transaction.TransactionCode)
                .FirstOrDefault(code => !string.IsNullOrWhiteSpace(code));

            return new SumUpCheckoutResult(
                Id,
                Status,
                HostedCheckoutUrl,
                transactionCode,
                Amount,
                Currency);
        }
    }

    private sealed class SumUpTransactionApiResponse
    {
        [JsonPropertyName("transaction_code")]
        public string? TransactionCode { get; init; }
    }
}
