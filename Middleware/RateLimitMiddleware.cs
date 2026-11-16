using Microsoft.Extensions.Options;
using TestingAppWeb.Options;

namespace TestingAppWeb.Middleware
{
    public class RateLimitMiddleware
    {
        private readonly ILogger<RateLimitMiddleware> _logger;
        private readonly RequestDelegate _next;
        private static readonly Dictionary<string, (int Count, DateTimeOffset LastReset)> _ipCounts = new();
        private static DateTimeOffset _lastCleanup = DateTimeOffset.UtcNow;
        private readonly int _limit;
        private readonly TimeSpan _interval;
        private readonly bool _enabled;

        public RateLimitMiddleware(
            ILogger<RateLimitMiddleware> logger,
            RequestDelegate next,
            IOptions<RateLimitOptions> options)
        {
            _logger = logger;
            _next = next;
            _limit = options.Value.Limit;
            _interval = TimeSpan.FromSeconds(options.Value.WindowSeconds);
            _enabled = options.Value.Enabled;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!_enabled)
            {
                await _next(context);
                return;
            }

            var remoteIp = context.Connection.RemoteIpAddress;
            if (remoteIp == null)
            {
                // IP-фильтр ниже по конвейеру обработает такой запрос.
                await _next(context);
                return;
            }

            var ip = remoteIp.IsIPv4MappedToIPv6 ? remoteIp.MapToIPv4().ToString() : remoteIp.ToString();
            var now = DateTimeOffset.UtcNow;
            var isLimited = false;

            lock (_ipCounts)
            {
                CleanupExpiredEntries(now);

                if (!_ipCounts.TryGetValue(ip, out var entry))
                {
                    _ipCounts[ip] = (1, now);
                }
                else if (now - entry.LastReset > _interval)
                {
                    _ipCounts[ip] = (1, now);
                }
                else if (entry.Count >= _limit)
                {
                    isLimited = true;
                }
                else
                {
                    _ipCounts[ip] = (entry.Count + 1, entry.LastReset);
                }
            }

            if (isLimited)
            {
                _logger.LogWarning("Rate limit exceeded for IP {RemoteIp}", ip);

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = ((int)_interval.TotalSeconds).ToString();
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync("Too many requests. Please try again later.");
                return;
            }

            await _next(context);
        }

        private void CleanupExpiredEntries(DateTimeOffset now)
        {
            if (now - _lastCleanup < _interval)
            {
                return;
            }

            var expiredKeys = _ipCounts
                .Where(pair => now - pair.Value.LastReset > _interval)
                .Select(pair => pair.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _ipCounts.Remove(key);
            }

            _lastCleanup = now;
        }
    }
}
