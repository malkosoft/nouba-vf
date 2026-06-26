using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;

namespace Nouba.Security;

/// <summary>
/// Limiteur simple en mémoire contre le brute force des connexions.
/// La clé mélange IP + type d'espace + login normalisé.
/// </summary>
public sealed class LoginRateLimiter
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<LoginRateLimiter> _logger;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan CounterTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MaxPenaltyDelay = TimeSpan.FromSeconds(2);

    public LoginRateLimiter(IMemoryCache cache, ILogger<LoginRateLimiter> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public bool IsBlocked(HttpContext httpContext, string area, string? login, out TimeSpan remaining)
    {
        var key = BuildKey(httpContext, area, login);
        if (_cache.TryGetValue<DateTimeOffset>(LockKey(key), out var until) && until > DateTimeOffset.UtcNow)
        {
            remaining = until - DateTimeOffset.UtcNow;
            return true;
        }

        remaining = TimeSpan.Zero;
        return false;
    }

    public void RegisterFailure(HttpContext httpContext, string area, string? login)
    {
        var key = BuildKey(httpContext, area, login);
        var attemptsKey = AttemptsKey(key);
        var attempts = _cache.Get<int>(attemptsKey) + 1;
        _cache.Set(attemptsKey, attempts, CounterTtl);
        _logger.LogWarning("Tentative de connexion invalide pour {Area}/{LoginHash} depuis {Ip} ({Attempt}/{Max})", area, Hash(login ?? string.Empty), GetIp(httpContext), attempts, MaxFailedAttempts);

        if (attempts >= MaxFailedAttempts)
        {
            var until = DateTimeOffset.UtcNow.Add(LockoutDuration);
            _cache.Set(LockKey(key), until, LockoutDuration);
            _cache.Remove(attemptsKey);
            _logger.LogWarning("Connexion bloquée temporairement pour {Area}/{LoginHash} depuis {Ip}", area, Hash(login ?? string.Empty), GetIp(httpContext));
        }
    }

    public void RegisterSuccess(HttpContext httpContext, string area, string? login)
    {
        var key = BuildKey(httpContext, area, login);
        _cache.Remove(AttemptsKey(key));
        _cache.Remove(LockKey(key));
    }

    public async Task DelayIfNeededAsync(HttpContext httpContext, string area, string? login, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(httpContext, area, login);
        var attempts = _cache.Get<int>(AttemptsKey(key));
        if (attempts <= 0) return;
        var delay = TimeSpan.FromMilliseconds(Math.Min(250 * attempts, MaxPenaltyDelay.TotalMilliseconds));
        if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
    }

    private static string BuildKey(HttpContext httpContext, string area, string? login)
        => $"{area}:{GetIp(httpContext)}:{(login ?? string.Empty).Trim().ToUpperInvariant()}";

    private static string GetIp(HttpContext httpContext)
        => httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string AttemptsKey(string key) => $"login:attempts:{Hash(key)}";
    private static string LockKey(string key) => $"login:lock:{Hash(key)}";

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];
}
