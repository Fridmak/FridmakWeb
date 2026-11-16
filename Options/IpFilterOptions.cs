namespace TestingAppWeb.Options
{
    public sealed class IpFilterOptions
    {
        public const string SectionName = "IpFilter";

        public bool Enabled { get; set; } = true;

        public List<string> BlockedIps { get; set; } = new();
    }
}
