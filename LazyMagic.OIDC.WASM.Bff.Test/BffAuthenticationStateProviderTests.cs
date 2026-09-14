using System.Net;
using System.Text;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace LazyMagic.OIDC.WASM.Bff.Test;

/// <summary>
/// How often <see cref="BffAuthenticationStateProvider"/> sends <c>GET /bff/user</c>, and the behaviours sharing that
/// request must keep: the session poll always asks the server, anonymous is only ever the server's answer, and the boot
/// window still re-announces the state to late subscribers.
///
/// The provider's schedule, which these tests drive through a fake clock: early ticks at 1 s, 3 s, ... 19 s after it is
/// constructed, then one tick every poll interval.
/// </summary>
public class BffAuthenticationStateProviderTests
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LastEarlyTick = TimeSpan.FromSeconds(19);

    [Fact]
    public async Task CallersAskingWhileTheServerIsAnswering_ShareOneRequest()
    {
        using var h = new Harness();
        h.Server.Hold = new TaskCompletionSource();

        var asks = Enumerable.Range(0, 5).Select(_ => h.Provider.GetAuthenticationStateAsync()).ToArray();

        Assert.Equal(1, h.Server.Requests);
        Assert.All(asks, ask => Assert.False(ask.IsCompleted));
        h.Server.Hold.SetResult();
        Assert.All(await Task.WhenAll(asks), state => Assert.True(IsAuthenticated(state)));
        Assert.Equal(1, h.Server.Requests);
    }

    /// <summary>
    /// A console load as measured in SellerApp on 2026-09-14: CascadingAuthenticationState asks at once, two
    /// LoginDisplays and MainLayout ask about 0.4 s later, and each LoginDisplay asks again on every notification.
    /// That was 34 requests; the answer does not change inside the window, so it is one.
    /// </summary>
    [Fact]
    public async Task ABootWindowWithSubscribersThatAskAgain_CostsOneRequest_AndStillReachesLateSubscribers()
    {
        using var h = new Harness();
        var asks = new List<Task<AuthenticationState>> { h.Provider.GetAuthenticationStateAsync() };
        h.Advance(TimeSpan.FromMilliseconds(400));
        h.Provider.AuthenticationStateChanged += _ => asks.Add(h.Provider.GetAuthenticationStateAsync());
        h.Provider.AuthenticationStateChanged += _ => asks.Add(h.Provider.GetAuthenticationStateAsync());
        asks.AddRange(Enumerable.Range(0, 3).Select(_ => h.Provider.GetAuthenticationStateAsync()));
        h.Advance(TimeSpan.FromSeconds(10));
        var late = new List<Task<AuthenticationState>>();
        h.Provider.AuthenticationStateChanged += late.Add;
        h.Advance(TimeSpan.FromSeconds(15));

        Assert.Equal(1, h.Server.Requests);
        Assert.Equal(10, h.Published.Count);
        Assert.Equal(5, late.Count);
        Assert.Equal(1 + 3 + 20, asks.Count);
        Assert.All(h.Published, state => Assert.True(IsAuthenticated(state)));
        Assert.All(late, task => Assert.True(task.IsCompletedSuccessfully && IsAuthenticated(task.Result)));
        Assert.All(await Task.WhenAll(asks), state => Assert.True(IsAuthenticated(state)));
    }

    [Fact]
    public async Task EveryPublishedTaskIsAlreadyComplete()
    {
        using var h = new Harness();
        h.Server.Hold = new TaskCompletionSource();
        _ = h.Provider.GetAuthenticationStateAsync();
        h.Advance(TimeSpan.FromSeconds(2));
        h.Server.Hold.SetResult();
        await h.WaitForPublishedAsync(1);
        h.Advance(LastEarlyTick + PollInterval);

        Assert.True(h.Published.Count >= 10);
        Assert.Equal(0, h.PendingPublishes);
    }

    [Fact]
    public async Task TheSessionPoll_AsksTheServerEvenWithARecentAnswer_SoAKilledSessionTurnsAnonymous()
    {
        using var h = new Harness();
        await h.Provider.GetAuthenticationStateAsync();
        h.Advance(LastEarlyTick + PollInterval - TimeSpan.FromSeconds(5));
        Assert.True(IsAuthenticated(await h.Provider.GetAuthenticationStateAsync()));
        var requestsBeforePoll = h.Server.Requests;
        h.Published.Clear();

        h.Server.Status = HttpStatusCode.Unauthorized;
        h.Advance(TimeSpan.FromSeconds(6));
        await h.WaitForPublishedAsync(1);

        Assert.Equal(requestsBeforePoll + 1, h.Server.Requests);
        Assert.False(IsAuthenticated(Assert.Single(h.Published)));
    }

    [Fact]
    public async Task Anonymous_IsOnlyEverTheServersAnswer_NeverAPlaceholderWhileItIsAsked()
    {
        using var h = new Harness();
        h.Server.Status = HttpStatusCode.Unauthorized;
        h.Server.Hold = new TaskCompletionSource();

        var first = h.Provider.GetAuthenticationStateAsync();
        h.Advance(TimeSpan.FromSeconds(4));

        Assert.False(first.IsCompleted);
        Assert.Empty(h.Published);
        h.Server.Hold.SetResult();
        Assert.False(IsAuthenticated(await first));
        await h.WaitForPublishedAsync(1);
        Assert.All(h.Published, state => Assert.False(IsAuthenticated(state)));
    }

    [Fact]
    public async Task A401_IsReannouncedThroughTheBootWindow_WithoutAskingAgain()
    {
        using var h = new Harness();
        h.Server.Status = HttpStatusCode.Unauthorized;
        Assert.False(IsAuthenticated(await h.Provider.GetAuthenticationStateAsync()));

        h.Advance(LastEarlyTick + TimeSpan.FromSeconds(1));

        Assert.Equal(1, h.Server.Requests);
        Assert.Equal(10, h.Published.Count);
        Assert.All(h.Published, state => Assert.False(IsAuthenticated(state)));
    }

    [Fact]
    public async Task AFailedCheck_IsNeverReused_TheNextCallerAsksAgain()
    {
        using var h = new Harness();
        h.Server.Status = HttpStatusCode.ServiceUnavailable;
        Assert.False(IsAuthenticated(await h.Provider.GetAuthenticationStateAsync()));

        h.Server.Status = HttpStatusCode.OK;

        Assert.True(IsAuthenticated(await h.Provider.GetAuthenticationStateAsync()));
        Assert.Equal(2, h.Server.Requests);
    }

    [Fact]
    public async Task AFailedCheck_IsAskedAgainByTheNextEarlyTick()
    {
        using var h = new Harness();
        h.Server.Status = HttpStatusCode.ServiceUnavailable;
        Assert.False(IsAuthenticated(await h.Provider.GetAuthenticationStateAsync()));

        h.Server.Status = HttpStatusCode.OK;
        h.Advance(TimeSpan.FromSeconds(1.5));
        await h.WaitForPublishedAsync(1);

        Assert.Equal(2, h.Server.Requests);
        Assert.True(IsAuthenticated(Assert.Single(h.Published)));
    }

    [Fact]
    public async Task ADefinitiveAnswer_ServesCallersForThirtySeconds_ThenTheServerIsAskedAgain()
    {
        using var h = new Harness();
        h.Advance(LastEarlyTick + TimeSpan.FromSeconds(1));
        h.Provider.NotifyStateChanged();
        await h.WaitForPublishedAsync(11);
        var requests = h.Server.Requests;

        h.Advance(TimeSpan.FromSeconds(29));
        await h.Provider.GetAuthenticationStateAsync();
        Assert.Equal(requests, h.Server.Requests);

        h.Advance(TimeSpan.FromSeconds(2));
        await h.Provider.GetAuthenticationStateAsync();
        Assert.Equal(requests + 1, h.Server.Requests);
    }

    [Fact]
    public async Task NotifyStateChanged_AsksTheServerEvenWithARecentAnswer()
    {
        using var h = new Harness();
        await h.Provider.GetAuthenticationStateAsync();
        h.Server.Status = HttpStatusCode.Unauthorized;

        h.Provider.NotifyStateChanged();
        await h.WaitForPublishedAsync(1);

        Assert.Equal(2, h.Server.Requests);
        Assert.False(IsAuthenticated(Assert.Single(h.Published)));
    }

    [Fact]
    public async Task WithPollingDisabled_NothingIsSentOrPublishedAfterTheBootWindow()
    {
        using var h = new Harness(pollInterval: TimeSpan.Zero);
        await h.Provider.GetAuthenticationStateAsync();
        h.Advance(LastEarlyTick + TimeSpan.FromSeconds(1));
        var requests = h.Server.Requests;

        h.Advance(TimeSpan.FromMinutes(30));

        Assert.Equal(requests, h.Server.Requests);
        Assert.Equal(10, h.Published.Count);
    }

    private static bool IsAuthenticated(AuthenticationState state) => state.User.Identity?.IsAuthenticated == true;

    private sealed class Harness : IDisposable
    {
        public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 9, 14, 0, 0, 0, TimeSpan.Zero));
        public FakeBffServer Server { get; } = new();
        public BffAuthenticationStateProvider Provider { get; }

        /// <summary>Every state the provider published, in order.</summary>
        public List<AuthenticationState> Published { get; } = new();

        /// <summary>Publishes whose task was still running: AuthorizeView would render "authorizing" for those.</summary>
        public int PendingPublishes;

        public Harness(TimeSpan? pollInterval = null)
        {
            var http = new HttpClient(Server) { BaseAddress = new Uri("https://console.example/seller/") };
            Provider = new BffAuthenticationStateProvider(
                http, NullLogger<BffAuthenticationStateProvider>.Instance, pollInterval ?? PollInterval, "/bff", Time);
            Provider.AuthenticationStateChanged += task =>
            {
                if (!task.IsCompletedSuccessfully)
                {
                    Interlocked.Increment(ref PendingPublishes);
                    return;
                }
                lock (Published) Published.Add(task.Result);
            };
        }

        /// <summary>Moves the clock in 100 ms steps, so every tick fires in order, as it would in a browser.</summary>
        public void Advance(TimeSpan by)
        {
            var step = TimeSpan.FromMilliseconds(100);
            for (var moved = TimeSpan.Zero; moved < by; moved += step)
                Time.Advance(step);
        }

        public async Task WaitForPublishedAsync(int count)
        {
            for (var waited = 0; waited < 5000; waited += 10)
            {
                lock (Published)
                    if (Published.Count >= count) return;
                await Task.Delay(10);
            }
            Assert.Fail($"expected {count} published state(s), saw {Published.Count}");
        }

        public void Dispose() => Provider.Dispose();
    }

    private sealed class FakeBffServer : HttpMessageHandler
    {
        private const string UserEnvelope =
            """{"isAuthenticated":true,"claims":{"sub":"u-1","name":"Test User","email":"user@example.invalid","roles":[]}}""";

        private int _requests;
        public int Requests => Volatile.Read(ref _requests);
        public volatile HttpStatusCode Status = HttpStatusCode.OK;

        /// <summary>While set and incomplete, the server has not answered yet.</summary>
        public TaskCompletionSource? Hold;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/bff/user", request.RequestUri!.AbsolutePath);
            Interlocked.Increment(ref _requests);
            if (Hold is { } hold)
                await hold.Task.ConfigureAwait(false);
            var status = Status;
            return status == HttpStatusCode.OK
                ? new HttpResponseMessage(status) { Content = new StringContent(UserEnvelope, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(status);
        }
    }
}
