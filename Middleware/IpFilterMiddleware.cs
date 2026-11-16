using Microsoft.Extensions.Options;
using System.Net;
using TestingAppWeb.Options;

namespace TestingAppWeb.MiddleWare
{
    public class IpFilterMiddleWare
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<IpFilterMiddleWare> _logger;
        private readonly HashSet<string> _blockedIps;

        public IpFilterMiddleWare(
            RequestDelegate next,
            ILogger<IpFilterMiddleWare> logger,
            IOptions<IpFilterOptions> options)
        {
            _next = next;
            _logger = logger;
            _blockedIps = new HashSet<string>(
                options.Value.BlockedIps,
                StringComparer.OrdinalIgnoreCase);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var remoteIp = context.Connection.RemoteIpAddress;

            if (remoteIp == null)
            {
                await HandleNullIpClients(context);
                return;
            }

            var ip = remoteIp.IsIPv4MappedToIPv6 ? remoteIp.MapToIPv4() : remoteIp;

            if (_blockedIps.Contains(ip.ToString()))
            {
                await HandleBlockedClients(ip, context);
                return;
            }

            await _next.Invoke(context);
        }

        private async Task HandleNullIpClients(HttpContext context)
        {
            _logger.LogWarning("Не удалось получить IP-адрес клиента.");
            await WriteForbidden(context);
        }

        private async Task HandleBlockedClients(IPAddress ip, HttpContext context)
        {
            _logger.LogWarning("Заблокированный IP: {RemoteIp}", ip.ToString());
            await WriteForbidden(context);
        }

        private static async Task WriteForbidden(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("Forbidden");
        }
    }
}
