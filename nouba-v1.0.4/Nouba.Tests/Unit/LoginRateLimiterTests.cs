using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Nouba.Security;

namespace Nouba.Tests.Unit;

public class LoginRateLimiterTests : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly LoginRateLimiter _limiter;
    private readonly DefaultHttpContext _ctx;

    public LoginRateLimiterTests()
    {
        _limiter = new LoginRateLimiter(_cache, NullLogger<LoginRateLimiter>.Instance);
        _ctx = new DefaultHttpContext();
        _ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
    }

    [Fact]
    public void IsBlocked_InitialState_ReturnsFalse()
    {
        Assert.False(_limiter.IsBlocked(_ctx, "admin", "user", out var remaining));
        Assert.Equal(TimeSpan.Zero, remaining);
    }

    [Fact]
    public void RegisterFailure_BelowThreshold_NotBlocked()
    {
        _limiter.RegisterFailure(_ctx, "admin", "user");
        _limiter.RegisterFailure(_ctx, "admin", "user");
        _limiter.RegisterFailure(_ctx, "admin", "user");
        _limiter.RegisterFailure(_ctx, "admin", "user");

        Assert.False(_limiter.IsBlocked(_ctx, "admin", "user", out _));
    }

    [Fact]
    public void RegisterFailure_AtThreshold_BlocksUser()
    {
        for (int i = 0; i < 5; i++)
            _limiter.RegisterFailure(_ctx, "admin", "lockme");

        Assert.True(_limiter.IsBlocked(_ctx, "admin", "lockme", out var remaining));
        Assert.True(remaining > TimeSpan.Zero);
    }

    [Fact]
    public void RegisterFailure_AtThreshold_RemainingIsAbout15Minutes()
    {
        for (int i = 0; i < 5; i++)
            _limiter.RegisterFailure(_ctx, "admin", "lockme2");

        _limiter.IsBlocked(_ctx, "admin", "lockme2", out var remaining);
        Assert.True(remaining.TotalMinutes > 14 && remaining.TotalMinutes <= 15);
    }

    [Fact]
    public void RegisterSuccess_ClearsFailuresAndUnblocks()
    {
        _limiter.RegisterFailure(_ctx, "admin", "user");
        _limiter.RegisterFailure(_ctx, "admin", "user");
        _limiter.RegisterSuccess(_ctx, "admin", "user");

        Assert.False(_limiter.IsBlocked(_ctx, "admin", "user", out _));
    }

    [Fact]
    public void RegisterSuccess_AfterLockout_Unblocks()
    {
        for (int i = 0; i < 5; i++)
            _limiter.RegisterFailure(_ctx, "admin", "locked");

        Assert.True(_limiter.IsBlocked(_ctx, "admin", "locked", out _));

        _limiter.RegisterSuccess(_ctx, "admin", "locked");

        Assert.False(_limiter.IsBlocked(_ctx, "admin", "locked", out _));
    }

    [Fact]
    public void IsBlocked_DifferentAreas_Independent()
    {
        for (int i = 0; i < 5; i++)
            _limiter.RegisterFailure(_ctx, "admin", "user");

        Assert.True(_limiter.IsBlocked(_ctx, "admin", "user", out _));
        Assert.False(_limiter.IsBlocked(_ctx, "agent", "user", out _));
    }

    [Fact]
    public void IsBlocked_DifferentIPs_Independent()
    {
        for (int i = 0; i < 5; i++)
            _limiter.RegisterFailure(_ctx, "admin", "user");

        Assert.True(_limiter.IsBlocked(_ctx, "admin", "user", out _));

        var ctx2 = new DefaultHttpContext();
        ctx2.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");
        Assert.False(_limiter.IsBlocked(ctx2, "admin", "user", out _));
    }

    [Fact]
    public async Task DelayIfNeededAsync_NoFailures_CompletesImmediately()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _limiter.DelayIfNeededAsync(_ctx, "admin", "noattempts");
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 100);
    }

    [Fact]
    public async Task DelayIfNeededAsync_WithFailures_AddsDelay()
    {
        _limiter.RegisterFailure(_ctx, "admin", "slowdown");
        _limiter.RegisterFailure(_ctx, "admin", "slowdown");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _limiter.DelayIfNeededAsync(_ctx, "admin", "slowdown");
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds >= 200, "Expected at least 200ms delay after 2 failures");
    }

    public void Dispose() => _cache.Dispose();
}
