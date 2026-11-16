namespace TestingAppWeb.Options
{
    public sealed class SeedOptions
    {
        public const string SectionName = "Seed";

        public string AdminUsername { get; set; } = string.Empty;

        public string AdminEmail { get; set; } = "admin@example.com";

        public string AdminPassword { get; set; } = string.Empty;
    }
}
