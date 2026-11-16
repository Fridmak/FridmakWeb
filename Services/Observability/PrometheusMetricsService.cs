using System.Globalization;
using System.Text.Json;

namespace TestingAppWeb.Services.Observability
{
    public sealed class PrometheusMetricsSnapshot
    {
        public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.UtcNow;
        public double? HttpRequestsPerSecond { get; init; }
        public double? ChatMessagesPerMinute { get; init; }
        public double? ActiveChatSessions { get; init; }
        public double? WasmGateActiveConnections { get; init; }
        public double? WasmGateRateRejectedPerMinute { get; init; }
        public double? WasmGateRelayedBytesPerSecond { get; init; }
        public double? AppUp { get; init; }
        public double? WasmGateUp { get; init; }

        public bool HasData => HttpRequestsPerSecond.HasValue ||
                               ChatMessagesPerMinute.HasValue ||
                               ActiveChatSessions.HasValue ||
                               WasmGateActiveConnections.HasValue;
    }

    public interface IPrometheusMetricsService
    {
        Task<PrometheusMetricsSnapshot> GetSnapshotAsync(
            CancellationToken cancellationToken = default);
    }

    public sealed class PrometheusMetricsService : IPrometheusMetricsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PrometheusMetricsService> _logger;

        public PrometheusMetricsService(
            HttpClient httpClient,
            ILogger<PrometheusMetricsService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<PrometheusMetricsSnapshot> GetSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            var queries = new
            {
                HttpRequests = QueryAsync(
                    "sum(rate(http_requests_received_total[5m]))",
                    cancellationToken),
                ChatMessages = QueryAsync(
                    "sum(rate(fridmak_chat_messages_total[5m])) * 60",
                    cancellationToken),
                ActiveSessions = QueryAsync(
                    "sum(fridmak_chat_active_sessions)",
                    cancellationToken),
                WasmGateActive = QueryAsync(
                    "sum(wasmgate_connections_active)",
                    cancellationToken),
                WasmGateRejected = QueryAsync(
                    "sum(rate(wasmgate_connections_rate_rejected_total[5m])) * 60",
                    cancellationToken),
                WasmGateBytes = QueryAsync(
                    "sum(rate(wasmgate_relayed_bytes_total[5m]))",
                    cancellationToken),
                AppUp = QueryAsync(
                    "max(up{job=\"fridmakweb\"})",
                    cancellationToken),
                WasmGateUp = QueryAsync(
                    "max(up{job=\"wasmgate\"})",
                    cancellationToken)
            };

            return new PrometheusMetricsSnapshot
            {
                HttpRequestsPerSecond = await queries.HttpRequests,
                ChatMessagesPerMinute = await queries.ChatMessages,
                ActiveChatSessions = await queries.ActiveSessions,
                WasmGateActiveConnections = await queries.WasmGateActive,
                WasmGateRateRejectedPerMinute = await queries.WasmGateRejected,
                WasmGateRelayedBytesPerSecond = await queries.WasmGateBytes,
                AppUp = await queries.AppUp,
                WasmGateUp = await queries.WasmGateUp
            };
        }

        private async Task<double?> QueryAsync(
            string query,
            CancellationToken cancellationToken)
        {
            try
            {
                var url = $"api/v1/query?query={Uri.EscapeDataString(query)}";
                using var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                var root = document.RootElement;
                if (root.GetProperty("status").GetString() != "success")
                {
                    return null;
                }

                var result = root.GetProperty("data").GetProperty("result");
                if (result.GetArrayLength() == 0)
                {
                    return null;
                }

                var value = result[0].GetProperty("value")[1].GetString();
                return double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed)
                    ? parsed
                    : null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Prometheus query failed: {Query}", query);
                return null;
            }
        }
    }
}
