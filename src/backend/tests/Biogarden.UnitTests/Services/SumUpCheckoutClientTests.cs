using System.Net;
using Biogarden.BusinessLogic.Services;

namespace Biogarden.UnitTests.Services;

public sealed class SumUpCheckoutClientTests
{
    [Fact]
    public async Task ReferenceLookupUsesEscapedFilterAndSuccessfulTransaction()
    {
        using var handler = new Handler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("?checkout_reference=reference%26other", request.RequestUri!.Query);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""
                [{"id":"checkout-1","status":"PAID","checkout_reference":"reference&other","merchant_code":"merchant",
                  "amount":900,"currency":"HUF","transactions":[
                    {"status":"FAILED","transaction_code":"failed-1"},
                    {"status":"SUCCESSFUL","transaction_code":"paid-1"}]}]
                """) };
        });
        using var http = new HttpClient(handler);
        var client = new SumUpCheckoutClient(http);
        var result = Assert.Single(await client.FindAsync("reference&other", "fake-key", new Uri("https://api.sumup.com")));
        Assert.Equal("paid-1", result.TransactionCode);
        Assert.Equal("merchant", result.MerchantCode);
        Assert.Equal(900, result.Amount);
    }

    [Fact]
    public async Task ProviderErrorBodyIsNotIncludedInException()
    {
        using var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        { Content = new StringContent("private merchant details") });
        using var http = new HttpClient(handler);
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new SumUpCheckoutClient(http).GetAsync("checkout-1", "fake-key", new Uri("https://api.sumup.com")));
        Assert.DoesNotContain("private merchant", exception.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
