namespace TestingAppWeb.Options
{
    public sealed class IpFilterOptions
    {
        public const string SectionName = "IpFilter";

        public List<string> BlockedIps { get; set; } = new();
    }
}
