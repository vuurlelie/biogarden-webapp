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

    private static Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return Task.CompletedTask;
        }

        // Provider bodies can include sensitive account details; never relay them to customers.
        throw new HttpRequestException("SumUp API request failed.", null, response.StatusCode);
    }

    public async Task<IReadOnlyList<SumUpCheckoutResult>> FindAsync(string reference, string apiKey, Uri apiBaseUrl, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(apiBaseUrl, $"/v0.1/checkouts?checkout_reference={Uri.EscapeDataString(reference)}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var checkouts = await response.Content.ReadFromJsonAsync<SumUpCheckoutApiResponse[]>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("SumUp checkout list was empty.");
        return checkouts.Select(x => x.ToResult()).ToArray();
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
        [JsonPropertyName("checkout_reference")]
        public string? CheckoutReference { get; init; }
        [JsonPropertyName("merchant_code")]
        public string? MerchantCode { get; init; }
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
            if (Status is not ("PENDING" or "FAILED" or "PAID" or "EXPIRED") || Transactions is null)
                throw new PaymentRecoveryException();
            var successful = Transactions.Where(transaction => transaction.Status == "SUCCESSFUL").ToArray();
            // A checkout label alone is not proof of one payment for the expected amount.
            // Pending/refunded or contradictory transaction histories require reconciliation, never another charge.
            if (Transactions.Any(transaction => transaction.Status is not ("SUCCESSFUL" or "FAILED" or "CANCELLED")) ||
                (Status == "PAID" ? successful.Length != 1 : successful.Length != 0))
                throw new PaymentRecoveryException();
            var transaction = successful.SingleOrDefault();
            if (transaction is not null && (string.IsNullOrWhiteSpace(transaction.TransactionCode) ||
                transaction.TransactionCode.Length > 200 || !Amount.HasValue || Amount <= 0 ||
                transaction.Amount != Amount || string.IsNullOrWhiteSpace(Currency) ||
                !string.Equals(transaction.Currency, Currency, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(MerchantCode) || transaction.MerchantCode != MerchantCode))
                throw new PaymentRecoveryException();
            var transactionCode = transaction?.TransactionCode;

            return new SumUpCheckoutResult(
                Id,
                Status,
                HostedCheckoutUrl,
                transactionCode,
                Amount,
                Currency,
                CheckoutReference,
                MerchantCode);
        }
    }

    private sealed class SumUpTransactionApiResponse
    {
        [JsonPropertyName("amount")]
        public decimal? Amount { get; init; }
        [JsonPropertyName("currency")]
        public string? Currency { get; init; }
        [JsonPropertyName("merchant_code")]
        public string? MerchantCode { get; init; }
        [JsonPropertyName("status")]
        public string? Status { get; init; }
        [JsonPropertyName("transaction_code")]
        public string? TransactionCode { get; init; }
    }
}
