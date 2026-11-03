namespace TestingAppWeb.Middleware
{
    public class RateLimitMiddleware
    {
        private readonly ILogger<RateLimitMiddleware> _logger;
        private readonly RequestDelegate _next;
        private static readonly Dictionary<string, (int Count, DateTimeOffset LastReset)> _ipCounts = new();
        private readonly int _limit = 1000;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

        public RateLimitMiddleware(ILogger<RateLimitMiddleware> logger, RequestDelegate next)
        {
            _logger = logger;
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            //Используется после проверки в IpFilter на null-ip так что он не null
            var remoteIp = context.Connection.RemoteIpAddress;

            var ip = remoteIp.IsIPv4MappedToIPv6 ? remoteIp.MapToIPv4().ToString() : remoteIp.ToString();
            var now = DateTimeOffset.UtcNow;

            lock (_ipCounts)
            {
                if (!_ipCounts.ContainsKey(ip))
                {
                    _ipCounts[ip] = (1, now);
                }
                else
                {
                    var (count, lastReset) = _ipCounts[ip];
                    if (now - lastReset > _interval)
                    {
                        _ipCounts[ip] = (1, now);
                    }
                    else if (count >= _limit)
                    {
                        context.Response.StatusCode = 429;
                        context.Response.Redirect("Error/TooManyRequests");
                        return;
                    }
                    else
                    {
                        _ipCounts[ip] = (count + 1, lastReset);
                    }
                }
            }

            await _next(context);
        }
    }
}
