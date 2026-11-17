using Prometheus;

namespace TestingAppWeb.Services.Observability
{
    public sealed class AppMetrics
    {
        public Counter ChatMessages { get; } = Metrics.CreateCounter(
            "fridmak_chat_messages_total",
            "Chat messages processed by action.",
            new CounterConfiguration { LabelNames = new[] { "action" } });

        public Counter Registrations { get; } = Metrics.CreateCounter(
            "fridmak_users_registered_total",
            "Successfully registered users.");

        public Counter DirectMessages { get; } = Metrics.CreateCounter(
            "fridmak_direct_messages_total",
            "Direct messages sent.");

        public Gauge ActiveChatSessions { get; } = Metrics.CreateGauge(
            "fridmak_chat_active_sessions",
            "Active in-memory chat sessions.");

        public Gauge SignalRConnections { get; } = Metrics.CreateGauge(
            "fridmak_signalr_connections",
            "Active SignalR connections.");
    }
}
