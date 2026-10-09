using System.Net;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Enums;
using Microsoft.Extensions.Options;

namespace Biogarden.UnitTests.Services;

public sealed class GlsDeliveryPointDirectoryTests
{
    private const string Feed = """
        {"items":[
          {"id":"locker-1","type":"parcel-locker","name":"Test Locker","contact":{"countryCode":"HU","postalCode":"8200","city":"Veszprém","address":"Test utca 1"},"features":["delivery","acceptsCard"]},
          {"id":"shop-1","type":"parcel-shop","name":"Test Shop","contact":{"countryCode":"HU","postalCode":"8200","city":"Veszprém","address":"Test utca 2"},"features":["delivery","acceptsCash"]},
          {"id":"foreign","type":"parcel-shop","name":"Foreign","contact":{"countryCode":"SK","postalCode":"1111","city":"Test","address":"Test"},"features":["delivery"]},
          {"id":"dropoff","type":"parcel-shop","name":"Dropoff","contact":{"countryCode":"HU","postalCode":"1111","city":"Test","address":"Test"},"features":["pickup"]}
        ]}
        """;

    [Fact]
    public async Task ParsesOfficialSchemaAndRejectsMissingWrongTypeForeignAndNonDeliveryPoints()
    {
        var handler = new Handler(Feed);
        using var directory = Directory(handler, new Clock());
        var locker = await directory.ResolveAsync("locker-1", FulfillmentMethod.ParcelLocker);
        Assert.True(locker.AcceptsCard);
        Assert.False(locker.AcceptsCash);
        Assert.Equal("Test utca 1", locker.Address);
        Assert.True((await directory.ResolveAsync("shop-1", FulfillmentMethod.ParcelShop)).AcceptsCash);
        foreach (var id in new[] { "missing", "foreign", "dropoff", "locker-1" })
            await Assert.ThrowsAsync<InvalidOperationException>(() => directory.ResolveAsync(id, FulfillmentMethod.ParcelShop));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ExpiredCacheDoesNotValidateThroughOutageAndRefreshCanRecover()
    {
        var handler = new Handler(Feed);
        var clock = new Clock();
        using var directory = Directory(handler, clock);
        await directory.ResolveAsync("locker-1", FulfillmentMethod.ParcelLocker);
        clock.Now = clock.Now.AddMinutes(16);
        handler.Fail = true;
        await Assert.ThrowsAsync<GlsDirectoryUnavailableException>(() => directory.ResolveAsync("locker-1", FulfillmentMethod.ParcelLocker));
        await Assert.ThrowsAsync<GlsDirectoryUnavailableException>(() => directory.ResolveAsync("locker-1", FulfillmentMethod.ParcelLocker));
        Assert.Equal(2, handler.Calls);
        handler.Fail = false;
        handler.Body = Feed.Replace("locker-1", "replacement");
        clock.Now = clock.Now.AddSeconds(31);
        await Assert.ThrowsAsync<InvalidOperationException>(() => directory.ResolveAsync("locker-1", FulfillmentMethod.ParcelLocker));
        Assert.Equal("replacement", (await directory.ResolveAsync("replacement", FulfillmentMethod.ParcelLocker)).Id);
        Assert.Equal(3, handler.Calls);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"items\":[]}")]
    [InlineData("{\"items\":[null,1,{}]}")]
    public async Task UnusableProviderResponseHasIntentionalUnavailableError(string body)
    {
        using var directory = Directory(new Handler(body), new Clock());
        await Assert.ThrowsAsync<GlsDirectoryUnavailableException>(() => directory.ResolveAsync("locker-1", FulfillmentMethod.ParcelLocker));
    }

    [Fact]
    public async Task OversizedAndDuplicateResponsesAreRejected()
    {
        using var oversized = Directory(new Handler(new string(' ', 2048)), new Clock(), 1024);
        await Assert.ThrowsAsync<GlsDirectoryUnavailableException>(() => oversized.ResolveAsync("x", FulfillmentMethod.ParcelLocker));
        using var duplicate = Directory(new Handler(Feed.Replace("shop-1", "locker-1")), new Clock());
        await Assert.ThrowsAsync<GlsDirectoryUnavailableException>(() => duplicate.ResolveAsync("locker-1", FulfillmentMethod.ParcelLocker));
    }

    [Fact]
    public async Task ConcurrentLookupsShareOneRefreshAndHonorCancellation()
    {
        var handler = new Handler(Feed) { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var directory = Directory(handler, new Clock());
        var first = directory.ResolveAsync("locker-1", FulfillmentMethod.ParcelLocker);
        await handler.Started.Task;
        var second = directory.ResolveAsync("shop-1", FulfillmentMethod.ParcelShop);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => directory.ResolveAsync("locker-1", FulfillmentMethod.ParcelLocker, cancelled.Token));
        handler.Gate.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(1, handler.Calls);
    }

    private static GlsDeliveryPointDirectory Directory(Handler handler, Clock clock, int maximumBytes = 8192)
        => new(() => new HttpClient(handler, disposeHandler: false), Options.Create(new GlsOptions { MaximumResponseBytes = maximumBytes }), clock);
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Handler(string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool Fail { get; set; }
        public string Body { get; set; } = body;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? Gate { get; init; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Started.TrySetResult();
            if (Gate is not null)
                await Gate.Task.WaitAsync(cancellationToken);
            return new(Fail ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = new StringContent(Body) };
        }
    }
}
