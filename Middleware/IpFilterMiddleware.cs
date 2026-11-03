using Microsoft.AspNetCore.Connections.Features;
using System.Net;

namespace TestingAppWeb.MiddleWare
{
    public class IpFilterMiddleWare
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<IpFilterMiddleWare> _logger;
        private readonly HashSet<string> _blockedIps;

        public IpFilterMiddleWare(RequestDelegate next, ILogger<IpFilterMiddleWare> logger, IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            var blockedIps = configuration.GetSection("BlockedIps").Get<string[]>();
            _blockedIps = new HashSet<string>(blockedIps ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var remoteIp = context.Connection.RemoteIpAddress;

            if (remoteIp == null) {
                HandleNullIpClients(remoteIp, context);
                return;
            }

            var ip = remoteIp.IsIPv4MappedToIPv6 ? remoteIp.MapToIPv4() : remoteIp;

            if (_blockedIps.Contains(ip.ToString()))
            {
                HandleBlockedClients(ip, context);
                return;
            }
            else
            {
                await _next.Invoke(context);
            }
        }

        private void HandleNullIpClients(IPAddress? remoteIp, HttpContext context)
        {
            _logger.LogWarning("Не удалось получить IP-адрес клиента.");
            context.Response.StatusCode = 403;
            context.Response.Redirect("/Error/Forbidden");
            return;
        }

        private void HandleBlockedClients(IPAddress ip, HttpContext context)
        {
            _logger.LogWarning("Заблокированный IP: {RemoteIp}", ip.ToString());
            context.Response.StatusCode = 403;
            context.Response.Redirect("/Error/Forbidden");
            return;
        }
    }
}
