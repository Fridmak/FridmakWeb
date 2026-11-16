namespace TestingAppWeb.Options
{
    public sealed class BotOptions
    {
        public const string SectionName = "BotSettings";

        public string DefaultPassword { get; set; } = string.Empty;
    }
}
