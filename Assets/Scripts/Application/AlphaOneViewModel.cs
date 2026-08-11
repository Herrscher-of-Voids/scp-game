namespace Scp.Application
{
    public sealed class AlphaOneViewModel
    {
        public bool IsActive { get; set; }

        public bool IsDeployed { get; set; }

        public int RebuildCycles { get; set; }

        public int Deployments { get; set; }

        public string LastResult { get; set; } = string.Empty;
    }
}
