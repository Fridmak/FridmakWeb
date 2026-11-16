namespace TestingAppWeb.Options
{
    public sealed class RateLimitOptions
    {
        public const string SectionName = "RateLimit";

        public int Limit { get; set; } = 1000;

        public int WindowSeconds { get; set; } = 60;
    }
}
