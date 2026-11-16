namespace TestingAppWeb.Options
{
    public sealed class PrometheusOptions
    {
        public const string SectionName = "Prometheus";

        public string BaseUrl { get; set; } = "http://prometheus:9090";

        public string GrafanaUrl { get; set; } = "http://localhost:3000";

        public int TimeoutSeconds { get; set; } = 5;
    }
}
